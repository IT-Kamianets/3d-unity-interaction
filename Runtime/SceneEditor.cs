using System;
using System.Collections.Generic;
using System.Linq;
using ITKamianets.Engine.Scene;
using UnityEngine;

namespace ITKamianets.Engine.Interaction
{
    /// <summary>
    /// Edits the objects of a loaded scene: selection, move, rotate, scale, duplicate, delete, with
    /// snapping and undo/redo. Generic: the same editor serves level design, room layouts and
    /// track building. It works on <see cref="SceneObject"/>s, so <see cref="SceneSaver"/> turns
    /// the result back into Scene JSON. Input is separate (<see cref="SceneEditorInput"/>), so
    /// tools, VR controllers or tests can drive it directly.
    /// </summary>
    public class SceneEditor : MonoBehaviour
    {
        [Tooltip("The scene being edited. Empty: any SceneObject under the picked collider.")]
        [SerializeField] private LoadedScene scene;

        [Header("Snapping")]
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float gridSize = 0.5f;
        [SerializeField] private bool snapRotation = true;
        [SerializeField] private float rotationStep = 15f;

        [Tooltip("Dragged objects rest on whatever surface is under them.")]
        [SerializeField] private bool snapToSurface = true;

        [SerializeField] private LayerMask pickLayers = ~0;

        private readonly List<SceneObject> _selection = new List<SceneObject>();

        public UndoHistory History { get; } = new UndoHistory();

        public LoadedScene Scene
        {
            get => scene;
            set => scene = value;
        }

        public bool SnapToGrid { get => snapToGrid; set => snapToGrid = value; }
        public float GridSize { get => gridSize; set => gridSize = Mathf.Max(0.001f, value); }
        public bool SnapRotation { get => snapRotation; set => snapRotation = value; }
        public float RotationStep { get => rotationStep; set => rotationStep = Mathf.Max(0.1f, value); }
        public bool SnapToSurface { get => snapToSurface; set => snapToSurface = value; }

        public IReadOnlyList<SceneObject> Selection => _selection;

        public event Action SelectionChanged;

        /// <summary>Raised after any change to the scene (including undo and redo), with the command's name.</summary>
        public event Action<string> Changed;

        // ---- Selection ----

        /// <summary>The scene object under a ray (e.g. from the mouse), or null.</summary>
        public SceneObject Pick(Ray ray, float maxDistance = 500f)
        {
            if (!Physics.Raycast(ray, out var hit, maxDistance, pickLayers, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var picked = hit.collider.GetComponentInParent<SceneObject>();
            return picked != null && (scene == null || picked.transform.IsChildOf(scene.transform)) ? picked : null;
        }

        public void Select(SceneObject target, bool additive = false)
        {
            if (!additive)
            {
                _selection.Clear();
            }

            if (target != null && !_selection.Contains(target))
            {
                _selection.Add(target);
            }

            SelectionChanged?.Invoke();
        }

        public void Toggle(SceneObject target)
        {
            if (!_selection.Remove(target))
            {
                _selection.Add(target);
            }

            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            _selection.Clear();
            SelectionChanged?.Invoke();
        }

        /// <summary>The middle of the selection's bounds, the point rotate and scale turn around.</summary>
        public Vector3 Pivot()
        {
            if (_selection.Count == 0)
            {
                return Vector3.zero;
            }

            var bounds = new Bounds(_selection[0].transform.position, Vector3.zero);
            foreach (var item in _selection)
            {
                bounds.Encapsulate(item.transform.position);
            }

            return bounds.center;
        }

        // ---- Operations ----

        public void Move(Vector3 delta)
        {
            Apply("Move", target => target.position = SnapPosition(target.position + delta));
        }

        /// <summary>Turns the selection around the vertical axis through its pivot.</summary>
        public void Rotate(float degrees)
        {
            if (snapRotation)
            {
                degrees = Mathf.Round(degrees / rotationStep) * rotationStep;
            }

            var pivot = Pivot();
            Apply("Rotate", target =>
            {
                target.RotateAround(pivot, Vector3.up, degrees);
                if (snapRotation)
                {
                    var euler = target.eulerAngles;
                    euler.y = Mathf.Round(euler.y / rotationStep) * rotationStep;
                    target.eulerAngles = euler;
                }
            });
        }

        public void Scale(float factor)
        {
            var pivot = Pivot();
            Apply("Scale", target =>
            {
                target.position = pivot + (target.position - pivot) * factor;
                target.localScale *= factor;
            });
        }

        /// <summary>Copies the selection, offset by one grid step, and selects the copies.</summary>
        public void Duplicate()
        {
            if (_selection.Count == 0)
            {
                return;
            }

            var copies = new List<SceneObject>();
            foreach (var original in _selection)
            {
                var copy = Instantiate(original.gameObject, original.transform.parent);
                copy.name = original.name;
                copy.transform.position = original.transform.position + new Vector3(gridSize, 0f, gridSize);

                var sceneObject = copy.GetComponent<SceneObject>();
                sceneObject.Id = Guid.NewGuid().ToString("N");

                // Boundary, volume and metadata properties are not serialized on the component.
                sceneObject.Data = original.Data;
                copies.Add(sceneObject);
            }

            // Already shown; recording lets undo hide them again.
            History.Record(new ActivateCommand("Duplicate", copies.Select(item => item.gameObject).ToArray(), true));
            _selection.Clear();
            _selection.AddRange(copies);
            SelectionChanged?.Invoke();
            Changed?.Invoke("Duplicate");
        }

        /// <summary>Removes the selection from the scene (hidden, so undo can bring it back).</summary>
        public void Delete()
        {
            if (_selection.Count == 0)
            {
                return;
            }

            History.Run(new ActivateCommand("Delete", _selection.Select(item => item.gameObject).ToArray(), false));
            ClearSelection();
            Changed?.Invoke("Delete");
        }

        public void Undo()
        {
            var command = History.Undo();
            if (command != null)
            {
                DropHiddenFromSelection();
                Changed?.Invoke("Undo " + command.Name);
            }
        }

        public void Redo()
        {
            var command = History.Redo();
            if (command != null)
            {
                DropHiddenFromSelection();
                Changed?.Invoke("Redo " + command.Name);
            }
        }

        // ---- Dragging ----

        private TransformState[] _dragBefore;
        private Vector3 _dragStart;
        private Vector3[] _dragOffsets;
        private Plane _dragPlane;

        public bool IsDragging => _dragBefore != null;

        /// <summary>Starts moving the selection with a ray (e.g. mouse down on a selected object).</summary>
        public bool BeginDrag(Ray ray)
        {
            if (_selection.Count == 0)
            {
                return false;
            }

            _dragPlane = new Plane(Vector3.up, Pivot());
            if (!_dragPlane.Raycast(ray, out var enter))
            {
                return false;
            }

            _dragStart = ray.GetPoint(enter);
            _dragBefore = _selection.Select(item => TransformState.Of(item.transform)).ToArray();
            _dragOffsets = _selection.Select(item => item.transform.position - _dragStart).ToArray();
            return true;
        }

        public void UpdateDrag(Ray ray)
        {
            if (!IsDragging || !_dragPlane.Raycast(ray, out var enter))
            {
                return;
            }

            var point = ray.GetPoint(enter);
            for (var i = 0; i < _selection.Count; i++)
            {
                var target = _selection[i].transform;
                var position = SnapPosition(point + _dragOffsets[i]);
                if (snapToSurface)
                {
                    position.y = SurfaceHeight(target, position);
                }

                target.position = position;
            }
        }

        /// <summary>Finishes a drag as one undo step.</summary>
        public void EndDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            var targets = _selection.Select(item => item.transform).ToArray();
            var after = targets.Select(TransformState.Of).ToArray();
            History.Record(new TransformCommand("Move", targets, _dragBefore, after));
            _dragBefore = null;
            Changed?.Invoke("Move");
        }

        public void CancelDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            for (var i = 0; i < _selection.Count; i++)
            {
                _dragBefore[i].ApplyTo(_selection[i].transform);
            }

            _dragBefore = null;
        }

        // ---- Internals ----

        private void Apply(string name, Action<Transform> change)
        {
            if (_selection.Count == 0)
            {
                return;
            }

            var targets = _selection.Select(item => item.transform).ToArray();
            var before = targets.Select(TransformState.Of).ToArray();
            foreach (var target in targets)
            {
                change(target);
            }

            History.Record(new TransformCommand(name, targets, before, targets.Select(TransformState.Of).ToArray()));
            Changed?.Invoke(name);
        }

        private Vector3 SnapPosition(Vector3 position)
        {
            if (!snapToGrid)
            {
                return position;
            }

            return new Vector3(Mathf.Round(position.x / gridSize) * gridSize, position.y, Mathf.Round(position.z / gridSize) * gridSize);
        }

        // The highest surface under the object's footprint centre, ignoring the object itself.
        private float SurfaceHeight(Transform target, Vector3 position)
        {
            var origin = new Vector3(position.x, position.y + 50f, position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 200f, pickLayers, QueryTriggerInteraction.Ignore);
            var best = float.NegativeInfinity;
            foreach (var hit in hits)
            {
                if (!hit.collider.transform.IsChildOf(target) && hit.point.y > best && _selection.TrueForAll(item => !hit.collider.transform.IsChildOf(item.transform)))
                {
                    best = hit.point.y;
                }
            }

            return float.IsNegativeInfinity(best) ? position.y : best;
        }

        private void DropHiddenFromSelection()
        {
            if (_selection.RemoveAll(item => item == null || !item.gameObject.activeInHierarchy) > 0)
            {
                SelectionChanged?.Invoke();
            }
        }
    }
}
