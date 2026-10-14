# IT-Kamianets 3D Engine: Unity Interaction

`3d-unity-interaction` provides reusable 3D editing and object manipulation functionality for the **IT-Kamianets 3D Engine**.

It allows applications built on the engine to interactively modify their scenes without implementing editing tools separately for every product.

### Logic

The package operates on objects managed by `3d-unity-scene`.

It should expose generic operations rather than product-specific behavior, allowing the same editing system to be used for interiors, game levels, racing tracks, simulations, and other 3D applications.

### Status

Version 0.1.

* `SceneEditor`: selection (single, additive, toggle) and picking with a ray; move, rotate around the selection's pivot (vertical axis), scale, duplicate (new ids, keeps scene data), delete (hidden so undo can restore it; hidden objects are left out of `SceneSaver`); grid, rotation and surface snapping; dragging on a plane as one undo step; undo/redo (`UndoHistory`, `IEditCommand`); `SelectionChanged` and `Changed` events for saving or syncing.
* `SceneEditorInput`: click selects (Shift adds), drag moves, R / Shift+R rotate, Delete, Ctrl+D, Ctrl+Z, Ctrl+Y, Escape; an "Editor" map in `EngineInput` (rebindable), gamepad shoulders rotate.
* `SelectionHighlight`: a box around each selected object.

Checked in Play mode on the arena example: picking a crate with a ray; a 1.2 m move snapped to the 0.5 m grid; a 20° turn snapped to 15°; duplicate (new id, one grid step away, keeps its volume); delete, undo, undo all, redo restore exactly; a drag rests the crate on the floor; the saved scene leaves out the hidden duplicate.

Works on `SceneObject`s from `3d-unity-scene`, so an edited scene is saved with `SceneSaver.Capture` as Scene JSON.

```csharp
var editor = gameObject.AddComponent<SceneEditor>();
editor.Scene = loadedScene;
gameObject.AddComponent<SceneEditorInput>();
gameObject.AddComponent<SelectionHighlight>();
editor.Changed += _ => save = SceneSaver.CaptureJson(loadedScene);
```

### Roadmap

* Move/rotate/scale gizmos (handles)
* Object snapping (to other objects' edges)
* Constraints
* Measurements
* Copy/paste between scenes
* Touch support
* VR editing through 3d-unity-xr
* Scene synchronization over the network
* Stable v1 editing API
