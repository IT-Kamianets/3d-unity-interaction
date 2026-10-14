using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ITKamianets.Engine.Interaction
{
    /// <summary>
    /// Mouse and keyboard (and gamepad) for a <see cref="SceneEditor"/>, through 3d-unity-input:
    /// click selects (Shift adds), dragging a selected object moves it, R / Shift+R turn it, Delete
    /// removes, Ctrl+D duplicates, Ctrl+Z / Ctrl+Y undo and redo, Escape clears the selection.
    /// The editing actions are an "Editor" map added to <see cref="EngineInput"/>, so they can be
    /// rebound like any other action.
    /// </summary>
    [RequireComponent(typeof(SceneEditor))]
    public class SceneEditorInput : MonoBehaviour
    {
        public const string MapName = "Editor";

        [Tooltip("Camera the pointer looks through. Empty: the main camera.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Pointer movement (pixels) before a press on a selected object becomes a drag.")]
        [SerializeField] private float dragThreshold = 6f;

        private SceneEditor _editor;
        private InputActionMap _map;
        private InputAction _add;
        private InputAction _rotate;
        private InputAction _rotateBack;
        private InputAction _delete;
        private InputAction _duplicate;
        private InputAction _undo;
        private InputAction _redo;

        private Vector2 _pressPosition;
        private bool _pressed;

        private void Awake()
        {
            _editor = GetComponent<SceneEditor>();
            var input = EngineInput.Default;
            _map = input.Asset.FindActionMap(MapName) ?? input.AddMap(MapName, DefineActions);

            _add = _map.FindAction("AddToSelection", true);
            _rotate = _map.FindAction("Rotate", true);
            _rotateBack = _map.FindAction("RotateBack", true);
            _delete = _map.FindAction("Delete", true);
            _duplicate = _map.FindAction("Duplicate", true);
            _undo = _map.FindAction("Undo", true);
            _redo = _map.FindAction("Redo", true);
        }

        private static void DefineActions(InputActionMap map)
        {
            const string kbm = EngineInputActions.Groups.KeyboardMouse;
            const string pad = EngineInputActions.Groups.Gamepad;

            map.AddAction("AddToSelection", InputActionType.Button, "<Keyboard>/shift", groups: kbm);

            var rotate = map.AddAction("Rotate", InputActionType.Button);
            rotate.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift", kbm).With("Button", "<Keyboard>/r", kbm);
            rotate.AddBinding("<Gamepad>/rightShoulder", groups: pad);

            // Plain R, without Shift: the one-modifier composite above takes Shift+R.
            var rotateBack = map.AddAction("RotateBack", InputActionType.Button);
            rotateBack.AddBinding("<Keyboard>/r", groups: kbm);
            rotateBack.AddBinding("<Gamepad>/leftShoulder", groups: pad);

            map.AddAction("Delete", InputActionType.Button, "<Keyboard>/delete", groups: kbm).AddBinding("<Gamepad>/buttonNorth", groups: pad);

            var duplicate = map.AddAction("Duplicate", InputActionType.Button);
            duplicate.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl", kbm).With("Button", "<Keyboard>/d", kbm);

            var undo = map.AddAction("Undo", InputActionType.Button);
            undo.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl", kbm).With("Button", "<Keyboard>/z", kbm);

            var redo = map.AddAction("Redo", InputActionType.Button);
            redo.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl", kbm).With("Button", "<Keyboard>/y", kbm);
        }

        private void OnEnable()
        {
            _map?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
            _editor.CancelDrag();
            _pressed = false;
        }

        private void Update()
        {
            var ui = EngineInput.Default.UI;
            var camera = viewCamera != null ? viewCamera : Camera.main;

            if (camera != null)
            {
                HandlePointer(ui, camera);
            }

            if (_undo.WasPressedThisFrame())
            {
                _editor.Undo();
            }
            else if (_redo.WasPressedThisFrame())
            {
                _editor.Redo();
            }
            else if (_duplicate.WasPressedThisFrame())
            {
                _editor.Duplicate();
            }
            else if (_delete.WasPressedThisFrame())
            {
                _editor.Delete();
            }
            else if (_rotate.WasPressedThisFrame())
            {
                _editor.Rotate(_editor.RotationStep);
            }
            else if (_rotateBack.WasPressedThisFrame() && !_add.IsPressed())
            {
                _editor.Rotate(-_editor.RotationStep);
            }

            if (ui.Cancel.WasPressedThisFrame())
            {
                _editor.CancelDrag();
                _editor.ClearSelection();
            }
        }

        private void HandlePointer(EngineInput.UIActions ui, Camera camera)
        {
            var pointer = ui.Point.ReadValue<Vector2>();
            var ray = camera.ScreenPointToRay(pointer);

            if (ui.Click.WasPressedThisFrame())
            {
                _pressed = true;
                _pressPosition = pointer;

                var picked = _editor.Pick(ray);
                if (picked == null)
                {
                    if (!_add.IsPressed())
                    {
                        _editor.ClearSelection();
                    }
                }
                else if (_add.IsPressed())
                {
                    _editor.Toggle(picked);
                }
                else if (!Contains(picked))
                {
                    _editor.Select(picked);
                }
            }

            if (_pressed && ui.Click.IsPressed())
            {
                if (!_editor.IsDragging && (pointer - _pressPosition).magnitude > dragThreshold && _editor.Selection.Count > 0)
                {
                    _editor.BeginDrag(camera.ScreenPointToRay(_pressPosition));
                }

                if (_editor.IsDragging)
                {
                    _editor.UpdateDrag(ray);
                }
            }

            if (_pressed && ui.Click.WasReleasedThisFrame())
            {
                _pressed = false;
                _editor.EndDrag();
            }
        }

        private bool Contains(Scene.SceneObject target)
        {
            foreach (var item in _editor.Selection)
            {
                if (item == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
