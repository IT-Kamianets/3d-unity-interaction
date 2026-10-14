using System.Collections.Generic;
using UnityEngine;

namespace ITKamianets.Engine.Interaction
{
    /// <summary>Draws a box around each selected object of a <see cref="SceneEditor"/>, sized to its renderers or colliders.</summary>
    [RequireComponent(typeof(SceneEditor))]
    public class SelectionHighlight : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(1f, 0.75f, 0.1f);
        [SerializeField] private float width = 0.03f;

        private static readonly int[] Edges =
        {
            0, 1, 1, 3, 3, 2, 2, 0,
            4, 5, 5, 7, 7, 6, 6, 4,
            0, 4, 1, 5, 2, 6, 3, 7,
        };

        private SceneEditor _editor;
        private Material _material;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();

        private void Awake()
        {
            _editor = GetComponent<SceneEditor>();
            // Builds only keep shaders some material uses; Lit always ships with URP.
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { color = color };
            _material.SetColor("_BaseColor", color);
        }

        private void OnDestroy()
        {
            Destroy(_material);
        }

        private void LateUpdate()
        {
            var selection = _editor.Selection;
            var index = 0;
            foreach (var item in selection)
            {
                if (item == null || !item.gameObject.activeInHierarchy || !TryGetBounds(item.gameObject, out var bounds))
                {
                    continue;
                }

                Draw(Line(index++), bounds);
            }

            for (var i = index; i < _lines.Count; i++)
            {
                _lines[i].enabled = false;
            }
        }

        private LineRenderer Line(int index)
        {
            while (_lines.Count <= index)
            {
                var go = new GameObject("Selection") { hideFlags = HideFlags.HideInHierarchy };
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = true;
                line.positionCount = Edges.Length;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lines.Add(line);
            }

            var result = _lines[index];
            result.enabled = true;
            result.startWidth = result.endWidth = width;
            return result;
        }

        private static void Draw(LineRenderer line, Bounds bounds)
        {
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                corners[i] = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            }

            for (var i = 0; i < Edges.Length; i++)
            {
                line.SetPosition(i, corners[Edges[i]]);
            }
        }

        private static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer is LineRenderer)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (found)
            {
                return true;
            }

            foreach (var collider in target.GetComponentsInChildren<Collider>())
            {
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return found;
        }
    }
}
