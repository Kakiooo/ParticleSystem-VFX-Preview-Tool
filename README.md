# ParticleSystem-VFX-Preview-Tool
# VFX Workbench

A Unity Editor tool for inspecting, isolating, and editing particle-system prefabs from a single dockable window, then exporting your changes to a new prefab, non-destructively.

Unity's Inspector shows a particle system's *modules* but gives you no self-contained way to preview a multi-emitter effect, isolate individual layers, or edit emitters side by side without opening the prefab in a scene. VFX Workbench fills that gap.

<!-- Replace with a GIF of the tool in action -->
![VFX Workbench in action](docs/demo.gif)<img width="400" height="225" alt="VFX_Tool" src="https://github.com/user-attachments/assets/5daf831c-4280-485d-9150-0d8dabfdcec8" />


## What it does

Drop a particle-system prefab into the window and it breaks the effect down into its individual emitters, each one previewed, isolatable, and editable in place.

- **Interactive preview viewport**: a live render of the effect with orbit camera (right-mouse drag), scroll zoom, a timeline scrubber, and replay. Renders the prefab in an isolated preview scene, so it never touches your open scene.
- **Per-emitter solo/mute**: toggle individual child emitters on and off while the effect is playing or being scrubbed, to isolate exactly what each layer contributes.
- **Inline emitter editing**: expand any emitter to its full particle-system inspector, embedded directly in the window, and edit its modules live.
- **Non-destructive prefab export**: save your edits to a new prefab. The original asset is never modified.

## How it works

The preview rebuilds the effect from scratch every frame using `ParticleSystem.Simulate(t, withChildren, restart: true)`, driven by an editor-side clock. Because each frame is an independent, deterministic simulation to an absolute time `t`, the timeline scrubber can seek instantly to any point, and toggling an emitter is reflected on the very next frame, no mid-flight particle state to reconcile.

A few implementation details worth noting:

- Rendering uses `PreviewRenderUtility` to draw the effect into an off-screen texture, which is then composited into the editor window, keeping the preview fully isolated from the user's scene.
- Random seeds are pinned (`useAutoRandomSeed = false`) on every child system so that a given `t` always produces an identical frame; without this, restarting the simulation each frame re-rolls randomness and the effect appears to drift.
- The list/preview split is a custom draggable layout, and the embedded inspectors are created once and cached to avoid per-frame allocation.

## Requirements

- Unity (Editor tooling; tested on the Built-in Render Pipeline and URP)
- No external dependencies

## Installation

Download the latest `VFXWorkbench.unitypackage` from the [Releases](../../releases) page, then either:

- **Drag and drop** the `.unitypackage` file into your open Unity project's Project window, or
- Double-click the file with your project open, or use **Assets → Import Package → Custom Package…**

Unity will show the import dialog, leaving everything checked and click **Import**. Open the tool from the **VFXTool** menu.

## Usage

1. Open the window from the **VFXTool** menu.
2. Drag a particle-system prefab into the object field.
3. Orbit, scrub, and replay in the preview viewport.
4. Use the checkboxes to solo/mute individual emitters.
5. Expand an emitter to edit its modules inline.
6. Click **Export as New Prefab** to save your changes to a new asset.

## Notes & limitations
- The embedded per-emitter inspector renders Unity's real particle inspector, but a couple of Inspector-only conveniences (the floating playback overlay) don't appear when embedded.
