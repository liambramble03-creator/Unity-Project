# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.6.7f1** project (Built-in render pipeline, target StandaloneWindows64): a single-player 3D platforming/parkour prototype (Celeste-inspired movement; Sifu/Guilty Gear-style combat is planned but not started). The only playable scene is `Assets/Scenes/MovementSandbox.unity`.

There is no build script, test assembly, or linter. Compilation and testing happen inside the Unity Editor; there are no `.asmdef` files, so everything compiles into `Assembly-CSharp`. Generated `*.csproj`/`*.slnx` files are git-ignored.

## What is project-owned vs. vendor

Only these folders are ours — edit freely:
- `Assets/Scripts/Prototype/` — all gameplay code (namespace-free `Prototype*` classes)
- `Assets/Characters/HumanDummy/` — Animator controller and project-made pose clips (`HumanDummy_HangPose.anim`, `HumanDummy_SlidePose.anim`)
- `Assets/Prefabs/Prototype/`, `Assets/Materials/Prototype/`, `Assets/Scenes/`
- `Assets/Plans/` — design/implementation plans

Everything else (`ExplosiveLLC`, `Kevin Iglesias`, `Brawler Animations Sample`, `FightingMotionsVolume1`) is imported asset-store content used as an animation source. **Do not modify vendor assets**, and do not apply scene-specific overrides back to `GrayCylinderPlayer.prefab` — the scene player is an overridden prefab instance.

## Design plans

`Assets/Plans/platforming-fighting-prototype.md` is the source of truth for mechanics and tuning intent. The top section is the current revision; sections marked "Historical … superseded" are kept for context only — do not restore behavior they describe (e.g. gravity-active dash, perpendicular-only sprint wall jumps, forced-forward ledge launch). Read the current section before changing movement code; it specifies exact action priorities, timers, and edge cases.

## Architecture (Assets/Scripts/Prototype)

- **`PrototypePlayerController`** — the core. A `CharacterController`-based kinematic controller (no Rigidbody, by deliberate choice) with an explicit `MovementState` of `Grounded`/`Airborne`/`Hanging`.
  - `Update()` reads Legacy Input Manager input (only when the window is focused and cursor locked) and calls `SimulateMovement(...)`. The public `SimulateMovement` overloads are the deterministic entry point used by tests — preserve existing overloads when adding inputs.
  - `SimulateMovement` subdivides the frame into ≤1/60 s substeps. **Each substep advances all `PrototypeMovingPlatform`s first, then `Physics.SyncTransforms()`, then `StepMovement`.** Platforms are not self-updating; the player drives one shared simulation clock so platform and player stay in lock-step.
  - Velocity is split into `_horizontalVelocity` (self/support-relative), `_inheritedHorizontal` (momentum inherited from leaving a moving support), and `_verticalVelocity`. Riding a platform uses support tracking + displacement, not transform parenting; support velocity is inherited exactly once on departure. Watch for double-counting when touching these terms.
  - Order inside `StepMovement` is load-bearing: support resolution → hanging → slide entry/slide-jump → locomotion acceleration → jump/wall-jump/dash. Momentum captured for slides and reflected wall jumps must be sampled before braking/collision wipes it (see `_landingHorizontal`, `_impactVelocity`/`_impactWall`).
  - Space priority: hanging launch → ground/coyote jump → eligible wall jump (sprint + ≤45° from wall plane reflects velocity) → air dash (one charge, refilled only on real walkable support).
- **`PrototypeParkourProbe`** — physics queries (wall, ledge, support, capsule fit/clearance). Ledge data (anchor, two grip points, normal) is stored collider-local and `Refresh()`ed each step so moving ledges work.
- **`PrototypeLedgeHandIK`** — on the child Animator; places hands on the ledge grips in `OnAnimatorIK` (requires IK Pass on the layer).
- **`PrototypeMovingPlatform`** — deterministic translating platform (`Smooth` or `AbruptReversal`) advanced externally via `Advance(dt)`; exposes velocity for momentum inheritance.
- **`PrototypeThirdPersonCamera`** — mouse orbit + cursor lock; player input is suppressed whenever the cursor is unlocked.

Animation: `HumanDummyController.controller` (Idle/Walk/Run/Jump/Fall/Land/Hanging/Slide) driven by `UpdateAnimation()`; root motion is disabled — physics moves the character, never the Animator. No slide clip exists in the vendor packs; the slide/hang poses are provisional project-made clips.

## Constraints

- Input is the **Legacy Input Manager** (`activeInputHandler: 0`). Don't introduce the new Input System without being asked.
- Controls: WASD/arrows move, Shift sprint, Space jump/wall-jump/dash, Ctrl slide, down to drop from a ledge.
- `MovementSandbox.unity` contains one player, 12 static obstacles, and four moving platforms; don't change course layout unless asked.
- Temporary Edit/Play Mode test harnesses used for verification should be removed afterwards.
