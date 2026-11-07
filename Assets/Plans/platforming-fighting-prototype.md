# Project Overview
- **Game Title:** Platforming/Fighting Prototype (working title).
- **High-Level Concept:** Celeste-inspired 3D parkour with future Sifu/Guilty Gear-inspired combat. This revision adds a 0.4-second vertical dash suspension, shallow-angle sprint wall reflections, and momentum-preserving ground slides and slide-jumps.
- **Players:** Single local player.
- **Inspiration / Reference Games:** Celeste, Sifu, Guilty Gear.
- **Tone / Art Direction:** Existing white human dummy and graybox practice course.
- **Target Platform:** Windows PC, StandaloneWindows64.
- **Screen Orientation / Resolution:** Landscape, existing 1920×1080 defaults.
- **Render Pipeline:** Built-in, Unity 6000.6.7f1.

# Game Mechanics
## Core Gameplay Loop
Chain sprint jumps, reflected wall jumps, air dashes, fast landings and slide-jumps across the existing course. Emphasize momentum mastery while preserving forgiving controls. No new level geometry, combat, or packages required in this revision.

## Controls and Input Methods
Preserve verified Legacy Input Manager (activeInputHandler = 0), WASD/arrows, Shift sprint, Space jump/wall jump/dash, existing mouse/cursor behavior. Add either Control key for slides. Existing Space priority remains hanging launch, valid ground/coyote jump, eligible wall jump, then dash.

### Air dash vertical suspension
- On accepted dash set vertical velocity to zero immediately, discard previous vertical momentum, and suspend gravity for exactly 0.4 seconds of gameplay simulation time.
- Keep current +10 units/sec horizontal boost, direction selection, 0.15-second horizontal protection and one-charge landing refresh. The new vertical timer is separate from horizontal protection.
- Resume normal falling gravity from zero after the suspension, not from the pre-dash velocity. Do not accidentally trigger an extra apex-float interval on expiry; clear apex data when dashing.
- Split/subdivide simulation at timer expiry if necessary so 30/60/120 Hz duration is consistent, with no gravity applied before the full 0.4 seconds has elapsed.
- Valid landing, hanging acquisition, accepted wall jump/ledge launch, or respawn cancels suspension so it cannot suppress a newly accepted upward impulse or moving-support behavior. Rejected inputs do not extend it. Geometry collision remains active; no noclip.

### Momentum-reflecting sprint wall jump
- User selected **angle measured from wall surface**, not inward wall normal. Reflect only a genuine incoming horizontal velocity whose angle to the wall plane is 45° or less while sprint is held and airborne.
- Use a normalized horizontal wall normal `n` and pre-action total airborne horizontal velocity `v` (including inherited horizontal momentum). Incoming means `dot(v,n) < -epsilon`; zero-speed, departing, or degenerate contacts are ineligible.
- Shallow approach criterion: `asin(abs(dot(normalize(v),n))) <= 45°` with numerical tolerance. Tangential approach still requires genuine inward motion, not simply standing near a wall.
- Reflect `vOut = v - 2 * dot(v,n) * n`. Preserve horizontal magnitude and tangential component; invert only inward normal component. Apply the existing vertical wall-jump impulse and cooldown/protected push-off.
- Non-sprint, steeper-than-45°, outgoing or too-slow approaches keep the ordinary perpendicular wall jump. Clear/reconcile inherited horizontal terms when storing reflected total velocity to avoid double addition.
- Capture the incoming velocity BEFORE this substep's ordinary air braking/steering and before wall collision resolution wipes normal velocity. If a recent real collision has removed inward velocity, retain a short-lived pre-impact sample tied to that same collider/contact, not a stale sample usable on arbitrary walls.
- During existing 0.12-second push-off protection, avoid inherited drag or steering destroying the reflected speed. Afterwards preserve the existing responsive air-control behavior; no perpetual constant-speed airborne movement implied.

### Ground slide and slide-jump
- User selected **hold Control until landing**: a fresh ground Control press OR held Control on a new valid landing can enter a slide at run speed (currently 10 units/sec) or faster. No indefinite queued slide after releasing Control.
- Determine eligibility and entry velocity before ground braking, air steering on the landing substep, or support acquisition clears inherited momentum. Use actual support-relative horizontal arrival velocity; on static ground this equals world horizontal arrival velocity. Subtract moving-support velocity when converting world arrival velocity to relative slide velocity.
- Capture velocity direction and magnitude. Keep entry horizontal speed for 0.25 seconds on an unobstructed flat surface, then decelerate with a separate strong tunable slide braking rate (initial 60 units/sec²) to rest. Collision can still reduce speed; never force through walls or restore speed lost to obstruction.
- Slide locks normal acceleration and direction steering; face slide travel. No continued sprint acceleration or renewed 0.25-second protection from holding Control. Once a slide ends, another on the same support requires releasing and pressing Control again; a new eligible landing may trigger held-Control slide.
- Space during slide immediately jumps using existing vertical impulse while preserving CURRENT horizontal slide momentum, including above-run speed. Do not let ordinary ground braking execute first or let buffered landing jump bypass momentum capture. If Control and Space are both held/pressed at a fast landing, capture slide momentum before consuming the jump.
- Brief approximately 0.12-second slide-jump momentum protection prevents the very next air-control step from immediately erasing the retained speed. Afterward normal air acceleration resumes. Inherit moving-platform departure momentum once, not twice.
- Leaving support while sliding ends grounded slide but retains departure horizontal momentum; do not apply slide braking in midair. Hanging/respawn clears slide/timers/input latch. Released cursor/focus cannot start slide.
- This revision does not add low-clearance crawling or change the collision capsule: slide is a movement mode with a visual pose, not permission to pass under obstacles. This avoids changing established ledge/support capsule assumptions.

# UI
No new HUD/menu. Document Control entry, held-Control landing activation and Space slide-jump in completion response. Add readable simulation diagnostics for slide state, timers and total/relative velocity if helpful.

# Key Asset & Context
## Verified state and integration risks
- `Assets/Scenes/MovementSandbox.unity`: one player, 12 static obstacles, four moving platforms; preserve all.
- Player: CharacterController height 2/radius 0.5, valid root-motion-disabled child Animator; turn setting 2880, run speed 10 and dash boost 10 in the saved scene. Scene player is an overridden instance of original cylinder prefab; preserve original prefab/vendor assets.
- `PrototypePlayerController.cs` already has shared substep stepping for platforms/player, explicit Grounded/Airborne/Hanging states, world/inherited/support-relative velocity terms, wall-jump priority, support query/recharge, coyote/buffer and hand-grip data.
- Current dash leaves gravity active. Current wall jump overwrites only `_horizontalVelocity` with perpendicular 7-unit push while inherited velocity may remain. `ResolveSupport` clears inherited horizontal momentum, and normal acceleration occurs BEFORE jump action processing. These orderings must be addressed to meet the user's momentum goals.
- `HumanDummyController.controller`: Idle/Walk/Run/Jump/Fall/Land/Hanging with IK-enabled hanging. No slide/crouch clip found in inspected Human Animations pack; do not silently generate or import one.
- Dependency queries through AssetDatabase confirmed the scene depends on player/probe/platform/camera/IK scripts and project controller; controller references vendor locomotion clips and project-owned hang pose. Recheck exact dependents immediately before changes using available dependency tools or read-only AssetDatabase queries.

## Assets to modify/create
- Modify `Assets/Scripts/Prototype/PrototypePlayerController.cs`: independent dash vertical timer, reflective wall-jump eligibility and pre-impact velocity tracking, slide mode/latch/timers and momentum-safe action/landing ordering. Preserve existing public simulation overloads and add a Control/slide-input overload for deterministic tests.
- Modify `Assets/Characters/HumanDummy/HumanDummyController.controller`: add Sliding boolean and a slide state with high priority when supported; correctly exit to Jump/Fall or locomotion and preserve hanging IK. If required, add explicit dash-hover visual state using existing valid Humanoid motion/held pose; never replay landing while suspended.
- Create `Assets/Characters/HumanDummy/HumanDummy_SlidePose.anim`: project-owned deterministic provisional low slide pose derived from compatible existing rig/clip, root motion off. No AI generation or vendor edits. Clearly report that a polished slide animation is deferred.
- Modify `Assets/Scenes/MovementSandbox.unity` only for serialized references/tuning if needed; do not add objects/platforms or alter course layout.
- Add temporary Edit/Play Mode test harnesses and remove them after verification. Probe/platform/IK scripts should remain unchanged unless testing proves a specific integration defect requiring a minimal fix.

## Approach
Extend the existing CharacterController velocity/state system rather than adding Rigidbody physics. Implement orthogonal dash/slide timers and explicit action/landing priorities so overlapping abilities cannot accidentally erase or duplicate momentum. A force-driven Rigidbody approach could approximate physical sliding but would replace working wall/ledge/moving-support behavior and is not justified here.

# Implementation Steps
1. **Verify context and impact.** Re-read player, support/probe configuration, saved scene and Animator; check compile health/input/dependencies. Record baseline run/walk speeds, dash charge behavior and moving-platform reversal. **Dependencies:** approval of this revision.
2. **Dash suspension.** Add 0.4-second vertical cancellation, gravity gating, correct expiry integration and interruptions; keep horizontal boost and charge behavior. Test upward and downward dash starts and post-expiry fall. **Dependencies:** Step 1.
3. **Reflected sprint wall jump.** Capture incoming world horizontal velocity safely, classify shallow angle/inward approach with user-selected surface convention, reflect total velocity and preserve speed during push-off. Reconcile inherited terms; retain ordinary fallback. **Dependencies:** Steps 1–2.
4. **Sliding and momentum-safe landing.** Add Control input and compatible simulation overload, slide entry/latch/hold/braking, ordering before locomotion deceleration, actual landing velocity capture, moving-support relative conversion and slide-jump departure protection. Avoid duplicated world/support momentum and stale buffers. **Dependencies:** Step 3 and existing support system.
5. **Animation wiring.** Add provisional slide pose/state and Sliding updates, verify transitions on slide start/stop/jump/ledge/respawn. Preserve Avatar, root-motion-disabled physics, IK pass and existing clips. **Dependencies:** Step 4.
6. **Runtime regression and persistence.** Test all three mechanics together on static and moving surfaces; check frame-rate timing, collision and action priorities, animation state and velocity measurements. Reopen/save scene, remove temporary test assets, stop Play Mode and disclose any unverified checks. **Dependencies:** Steps 2–5.

# Verification & Testing
- Dash at positive/negative vertical velocity: y velocity becomes exactly zero and unobstructed y remains constant for 0.4 seconds at 30/60/120 Hz; gravity resumes from zero afterward. Verify landing/wall jump/ledge launch interrupts freeze; second dash cannot extend it, respawn resets it, no apex-float double extension.
- Reflection angles 0-ish/15/30/45/46/60/90° from wall plane on both sides; inward versus outward/zero velocity; Shift held/released. Magnitude before/after reflection matches within numerical tolerance, tangential component unchanged, normal component inverted, vertical impulse present. Same-contact pre-impact sample expires, ordinary fallback/cooldown remain correct.
- Slide at speeds 9.9/10/15/20: only valid run-speed-or-faster grounded contact enters; fresh Control starts on ground, held Control starts on fast landing, released Control does not queue. Constant speed for 0.25 seconds then rapid monotonic stop; no retrigger during hold. Collision prevents penetrating/restoring blocked velocity.
- Fast airborne landing with Control: captured slide speed equals actual pre-braking arrival velocity, inherited momentum not discarded. Test immediate slide-jump and buffered Space at landing; jumping preserves current speed, including speeds above run target. No normal acceleration/friction before capture or on the launch frame.
- Moving platforms: slide relative speed versus support travel, jump inheritance exactly once, upward reversal preserves velocity, descending platforms can catch rider, stepping off during slide retains momentum. Passive platform motion alone must not count as self-running to enable a slide.
- Animation: Sliding only when actual slide mode active; exit at stop/jump/support loss; hanging wins when active, no root-motion drift. Verify provisional pose actually bends the visible dummy rather than moving collider underground.
- Regression: walk/sprint acceleration, rapid turning, one-charge refill, ordinary wall jump, ledge hand IK, vertical ledge jump, coyote/buffer/cursor/focus/reset and camera unchanged.
- No new Console errors; scene retains one player/12 static obstacles/four moving platforms and valid controller/references. State explicitly which tests passed versus unavailable automated or manual feel checks.

---
# Historical dash and moving-platform revision (completed; superseded above)
Retained as history only; do not restore gravity-active dash or perpendicular-only sprint wall jumps.

# Project Overview
- **Game Title:** Platforming/Fighting Prototype (working title).
- **High-Level Concept:** Responsive Celeste-inspired 3D platforming, with future Sifu/Guilty Gear-inspired combat. This revision fixes hanging alignment, sharpens turning, changes ledge jumps to vertical launches, adds a single-charge air dash, and adds moving platforms with momentum-preserving departures.
- **Players:** Single local player.
- **Inspiration / Reference Games:** Celeste, Sifu, Guilty Gear.
- **Tone / Art Direction:** Existing white humanoid dummy and white/gray primitive practice environment; no new generated artwork.
- **Target Platform:** Windows PC, StandaloneWindows64.
- **Screen Orientation / Resolution:** Landscape, existing 1920×1080 defaults.
- **Render Pipeline:** Built-in; Unity 6000.6.7f1.

# Game Mechanics
## Core Gameplay Loop
Practice precision jumps, wall jumps, hanging and vertical ledge launches; use a limited air dash to correct trajectories and moving-platform momentum to reach additional platforms. Keep the original static course and clear spawn area. These mechanics emphasize player agency and mastery (SDT competence) without introducing combat or stamina yet.

## Controls and Input Methods
- Keep Legacy Input Manager, verified activeInputHandler = 0, existing WASD/arrows, Shift sprint, Space and mouse orbit/cursor controls. Preserve walking acceleration and existing floaty gravity tuning.
- **Faster turning:** initial 2880 degrees/sec (four times current 720). Orient toward intended movement when input is present, rather than waiting for velocity to reverse; keep the existing movement acceleration. Preserve wall-push protection and fixed hanging orientation. Separate facing from inherited platform velocity, so riding a sideways platform does not turn the character.
- **Space priority (user selected):** hanging vertical launch → valid ground/coyote jump → eligible airborne wall jump → airborne dash. An exhausted/cooldown-invalid wall jump may fall back to dash. Ensure one press cannot trigger two actions across substeps; consume buffered jump input appropriately.
- **Vertical ledge launch:** remove forced forward motion, its timer and obsolete top-destination requirement for launching. Set launch horizontal velocity to zero and apply the existing upward jump impulse; preserve normal air steering on subsequent frames so the player can deliberately move onto the top. Check the swept overhead capsule corridor, not just its endpoint. Blocked launches remain hanging; retain regrab cooldown. On moving ledges, the launch impulse is vertical relative to the support, with support momentum inherited on release.
- **Air dash (user selected movement direction):** one charge available at spawn; fresh airborne Space press adds a horizontal velocity impulse in camera-relative movement direction, or current facing when input is neutral. Starting values: +10 units/sec impulse and approximately 0.15 seconds of reduced steering/braking so it is not immediately erased by current 24 units/sec² air acceleration. Keep vertical momentum and gravity active. Collision-safe capsule movement, no teleport, no ground dash, no auto-repeat from holding Space. Expose tuning and charge state for verification.
- Restore the charge only on genuine walkable support contact after landing (static ground/platform tops and moving platforms count). Do not refill on coyote time, wall hits, hanging or ledge launches. Respawn restores exactly one charge and clears all dash timers.
- **Moving platforms:** add four translating practice platforms: two lateral shuttles, a smoothly oscillating elevator and a constant-speed elevator with abrupt endpoint reversal to demonstrate launch momentum. No rotation, scaling, crush hazards or dynamic-rigidbody simulation in this revision.
- While supported, the player rides the platform using collision-aware support displacement plus relative movement, not Transform parenting. At jump, walk-off, or loss of support, inherit its instantaneous world velocity exactly once.
- When an upward-moving elevator abruptly reverses downward, the player cannot be glued to it: retain the previous upward world velocity, detach, and integrate gravity until landing again. Detect loss of unilateral support when a descending platform accelerates away faster than gravity can keep contact, including smooth downward accelerations when appropriate. A platform pushing upward can carry the player, but cannot pull them downward.
- Horizontal reversals may carry a supported rider as a gameplay simplification; departing momentum still inherits the departure velocity. Keep the user's upward-reversal example as the explicit acceptance criterion rather than claiming a full physical friction simulation.

# UI
No new UI assets or packages. Explain Space priority, dash recharge and moving-platform controls in the completion message. Expose dash charge, support and world velocity for tests/Inspector diagnostics where practical.

Top-view layout:
```
[existing static course, unchanged]  [shuttle -> landing]
[clear spawn]                       [elevator -> landing]
                         [abrupt-reversal elevator]
```
Keep all travel envelopes inside the floor and away from existing obstacles, with broad landing tops and gray contrast. Adjust proposed positions against actual scene bounds before placement.

# Key Asset & Context
## Verified diagnosis and project context
- Scene: `Assets/Scenes/MovementSandbox.unity`, one player, existing 12 static practice blocks, no Rigidbody or moving-platform system found in the scene.
- Player has height-2/radius-0.5 CharacterController, humanoid child Animator at approximately 1.02 world scale, root motion disabled, and scene overrides on `GrayCylinderPlayer.prefab`.
- `PrototypePlayerController.cs` turns toward horizontal velocity at 720 degrees/sec. It forces 6-unit forward movement for 0.65 seconds after ledge launch. Hanging returns early and stores a world-space anchor; these paths must change.
- `PrototypeParkourProbe.cs` currently computes anchor as edge minus capsule height, and uses a 0.65-unit bounded snap. It has no hand calibration, grip targets, support query or moving-anchor updates. This directly explains why capsule correctness does not ensure visual hand alignment.
- `HumanDummy_HangPose.anim` is a project-owned approximate muscle pose, not a finished grip animation. Existing Animator has Idle/Walk/Run/Jump/Fall/Land/Hanging states.
- Dependencies: MovementSandbox references the player prefab, probe, camera, animation controller and gray materials. Animation controller references the hang-pose asset and vendor clips. Preserve imported source animations/models and the original cylinder prefab; do not apply scene-specific overrides to vendor assets.
- Before implementation recheck dependency/dependent graph via available dependency tools (or AssetDatabase dependency queries if no dedicated tool is exposed), scene wiring, compilation and active input.

## Assets to modify/create
- Modify `Assets/Scripts/Prototype/PrototypePlayerController.cs`: input/facing, dash charge and impulse, straight-up ledge launch, support-relative motion versus inherited world momentum, reset logic and readable deterministic simulation hooks.
- Modify `Assets/Scripts/Prototype/PrototypeParkourProbe.cs`: geometric lip and two grip points, rig-calibrated anchor, swept clearance, support normal/distance checks and local-space ledge data for moving colliders.
- Create `Assets/Scripts/Prototype/PrototypeLedgeHandIK.cs` on the humanoid Animator object: solve both hand targets in OnAnimatorIK using the existing valid Avatar; orient wrists to grip the top, blend IK weight on entry/exit, and enable IK pass on the relevant Animator layer. No Animation Rigging package required.
- Modify `Assets/Characters/HumanDummy/HumanDummy_HangPose.anim` and `HumanDummyController.controller`: tune project-owned hanging posture if required, enable IK pass; no need for a new dash clip. Prevent pose blending from leaving hands off-target during established hang.
- Create `Assets/Scripts/Prototype/PrototypeMovingPlatform.cs`: deterministic endpoints, speed/period and motion mode, current/previous position and velocity, supported fixed-step advancement and no velocity spikes from initialization/reset.
- Modify `Assets/Scenes/MovementSandbox.unity`: wire hand-IK helper, faster turn speed and four moving box platforms under `Moving Platform Practice`. Reuse gray materials. Preserve original course and one player.
- Add temporary or project-owned deterministic tests for platform and dash state; remove temporary bootstrap files after testing.

## Approach and limits
Retain CharacterController: it fits the current architecture and gives controlled collision-safe movement. Use explicit support tracking plus world momentum rather than parenting, which would incorrectly drag the player downward after reversal. A Rigidbody player would model forces differently but would require replacing tested parkour and animation wiring, so is not recommended now.

For hanging, combine a calibrated pose-specific root offset with Humanoid hand IK. Pose-only fixed offsets are simpler but cannot reliably align both hands as animation blends or ledge geometry varies. Derive root-to-grip height and horizontal reach from the actual evaluated hanging pose/rig, not the capsule height or the current arbitrary airborne pose. Check both arms are within reach and the capsule remains outside the wall; reject unreachable grips rather than stretching or moving through geometry. Store grip/anchor coordinates in collider-local space and transform them each simulation step for moving ledges; detach safely if the collider disappears or the follow movement is obstructed.

Use one coherent simulation clock: move platforms before player steps, publish platform displacement/velocity, then move player, then evaluate animation/IK and camera. Prefer a fixed-step platform/player coordinator or an equally deterministic shared stepping path; do not mix independent Update transforms with FixedUpdate sampling or apply the same platform displacement per player substep. Expose deterministic Advance/Simulate methods so tests advance both systems together. Preserve existing player simulation overloads.

# Implementation Steps
1. **Context and measured hanging diagnosis.** Recheck files, dependencies, input and scene. Evaluate the real hanging pose, log left/right hand positions relative to player, capsule and ledge. Confirm current visual offsets and document baseline. **Dependencies:** approval of this revision.
2. **Facing and vertical launch.** Apply faster turning; face desired input independently of horizontal velocity except during protected push-off/hanging. Remove forced ledge-forward velocity, precheck swept overhead path, retain cooldown and reset behavior. **Dependencies:** Step 1.
3. **Fix grip geometry and visual alignment.** Extend ledge data with local-space edge/grip/anchor values; calibrate root offset from evaluated hanging pose. Implement Animator-side hand IK, enable layer IK pass, tune held pose and targets. Update moving hang anchors collision-safely; verify both hands and body placement, not just state names. **Dependencies:** Steps 1–2.
4. **Air dash.** Add fresh-press priority, charge and impulse logic with a short protection interval. Use camera-relative input/facing fallback. Restore only on validated top-surface landing, reset on respawn, and keep gravity active. Ensure dash does not double-fire with buffered jump, wall jump or hang launch. **Dependencies:** Step 2; support-aware recharge finalized in Step 5.
5. **Moving-support simulation.** Implement translating platform component and coherent shared stepping order. Track stable top contacts with walkable normals and relative vertical separation velocity, not CharacterController.isGrounded alone. Carry relative motion while supported; inherit last support velocity once on departure; retain upward inertia and detach on elevator downward reversal. Reset support and inherited terms on respawn/teleport. Integrate moving ledge anchors and landing recharge. **Dependencies:** Steps 3–4.
6. **Extend practice scene.** Add four moving platforms with nonintersecting travel paths and reachable landings; include an abrupt-reversal elevator demonstration. Reuse materials, wire new components without duplicate player/collider systems. Confirm camera obstruction behavior around moving geometry. **Dependencies:** Step 5 and measured movement/dash envelope.
7. **Runtime and visual verification.** Advance actual shared platform/player simulation; capture runtime hanging and moving-platform behavior. Correct specific issues, remeasure and retest, then reopen/save scene and remove temporary test files. Leave Play Mode stopped. If automatic editor reload stalls, report blocked tests rather than marking the whole suite passed. **Dependencies:** Step 6.

# Verification & Testing
- Turning: 90/180-degree input changes converge much faster; test 30/60/120 Hz, walk/sprint/airborne input, and neutral input on lateral platforms. Velocity acceleration must remain unchanged.
- Grip alignment: valid ledges from multiple directions/heights; both hand grip points within approximately 0.05–0.08 world units of targets after blending, elbows within plausible reach, capsule outside geometry, no large snap or hanging drift. Inspect actual rendered hands in Game view, including entry/exit blends.
- Vertical ledge jump: no horizontal launch injection on static ledge; player rises at current x/z if input is neutral, can steer intentionally afterward; overhead obstacles prevent launch; no instant regrab.
- Dash: no ground dash; one sudden horizontal boost; neutral input uses facing; camera-relative diagonal direction normalized; vertical velocity/gravity retained; charge spent once, second airborne press rejected, wall-jump priority intact. Walls and hanging do not refill; top-surface landing, moving-platform landing and respawn do. No same-press buffered action replay on landing or during substeps.
- Momentum: idle rider matches lateral/vertical platform motion without parenting or cumulative drift. Jump/walk-off inherits measured support velocity once. Upward elevator abruptly reversing downward leaves player still moving upward initially; gravity then reduces velocity and brings them down. Test on multiple phases and frame rates.
- Contact safety: falling faster than a descending platform can land; platform moving away faster than player cannot fake grounding/recharge. Ceiling and wall collisions clamp obstructed inherited motion; support deletion/disable and blocked hang follow detach safely. No duplicate support carry per substep or re-inheritance every frame.
- Regression: existing walk ramps, sprint, wall jumps, coyote/buffer, gravity, drop and respawn remain valid. Animator locomotion uses relative player speed, not passive platform travel. Camera excludes self and avoids moving geometry.
- Scene: original 12 blocks preserved; four new platform motion envelopes are inside the floor, spawn stays clear, no unintended path overlaps. Fresh multi-angle scene captures and runtime Game-view captures; limit visual correction rounds to three before reporting outstanding issues.
- Persistence: one player, references/IK pass/pose/materials/platform settings survive scene reopen; no vendor source changes, no new Console errors or temporary bootstrap left behind.

---
# Historical parkour revision (completed; superseded by the revision above)
Retained for history only; do not restore old ledge-forward launch or remove the new dash/moving platforms.

# Project Overview
- **Game Title:** Platforming/Fighting Prototype (working title).
- **High-Level Concept:** A responsive 3D parkour platformer inspired by Celeste, with future combat drawing from Sifu and Guilty Gear. This milestone adds accelerated walking, sprinting, forgiving airborne movement, wall jumps, ledge hanging and a graybox practice course; no combat, enemies, dash or stamina system yet.
- **Players:** Single local player.
- **Inspiration / Reference Games:** Celeste for precision platforming and forgiving controls, Sifu and Guilty Gear for future combat.
- **Tone / Art Direction:** White/gray checkerboard graybox; existing white Human Character Dummy, imported Human Basic Motions animations.
- **Target Platform:** Windows PC (StandaloneWindows64).
- **Screen Orientation / Resolution:** Landscape, existing 1920×1080 defaults.
- **Render Pipeline:** Built-in, Unity 6000.6.7f1.

# Game Mechanics
## Core Gameplay Loop
Build speed, sprint between obstacles, jump and steer in the air, wall-jump to gain height, grab ledges and jump onto platforms, then repeat or respawn to practice. Keep generous landing surfaces and an open starting area so each mechanic can be learned independently before combining them. This supports the MDA goal of mastery and SDT competence without making the first course punitive.

## Controls and Input Methods
User selected **responsive and floaty airborne motion** and **Space jumps up onto a grabbed platform**.
- Preserve camera-relative WASD/arrows, Space, mouse orbit and Escape/click cursor behavior. Input backend verified Legacy Input Manager (activeInputHandler = 0); no migration or dependency installation.
- **Walking:** accelerate from rest to existing 6 units/sec in approximately 0.30 seconds using a frame-rate-independent horizontal velocity target (about 20 units/sec² acceleration). Braking and direction-change rates are separately tunable; initial stopping time approximately 0.15 seconds. Clamp diagonal input.
- **Sprint:** hold either Shift key for a proposed 10 units/sec target; rise from walk to run in approximately 0.15 seconds, return to walk in approximately 0.20 seconds on release while movement is held. Releasing all movement brakes to rest rather than retaining a walk target. Values are Inspector-tunable starting points, not fixed game-design commitments.
- **Floaty jump:** proposed 2.5-unit jump height with approximately -12 units/sec² rising gravity, -15 falling gravity and brief reduced gravity near the apex (approximately 0.10 seconds). Preserve strong air steering and takeoff velocity; clamp terminal fall speed. This extends airtime compared with current 1.5-unit/-20 gravity jump. Tune against the course rather than scaling animations to drive physics.
- Add approximately 0.10-second coyote time and jump-input buffering for forgiveness. Space press is edge-triggered; holding it does not repeatedly jump. Do not add variable-height jumping or an air dash in this milestone.
- **Wall jump:** when airborne and within approximately 0.15–0.25 units beyond capsule radius of a near-vertical wall, a fresh/buffered Space press launches up and along its outward horizontal normal (initial approximately 7 units/sec away, vertical impulse based on configured jump height). Probe physical proximity, not just facing, so side contacts work. Short approximately 0.12-second steering protection preserves push-off before air control resumes. Reject floor/ceiling normals, triggers and stale contacts. Repeated jumps require real renewed proximity and a short cooldown; switching walls is allowed, no arbitrary one-jump-per-airtime restriction. No wall-running or wall-slide feature implied.
- **Ledge grab:** automatic only while airborne and moving/facing toward a nearby valid ledge, not while grounded or jumping away. Chest/hand-height wall probe plus downward top-surface probe finds the edge; confirm a suitably level top, hand reach, and clear head/capsule space. Reject overhangs, thin/invalid tops and obstructed hanging positions. Snap only a short bounded distance to a collision-safe hanging anchor facing the wall. Freeze horizontal velocity and gravity while hanging; do not let ordinary movement override it.
- **Ledge launch:** a NEW Space press after grabbing launches upward and toward the top, using collision-aware movement, never teleporting through the ledge. Precheck the destination capsule and head clearance. Stay hanging if blocked. Brief regrab cooldown prevents instant relatching; reset buffered input on grab so the jump that led to the grab does not automatically launch.
- **Drop:** S/down while hanging releases the ledge and applies a brief regrab cooldown, preventing softlocks and allowing retries. Document this secondary control.
- Explicit Grounded/Airborne/Hanging movement states; wall-jump push-off is a timed airborne modifier. Input priority: hanging actions, valid ground jump, nearby wall jump, then ledge acquisition. Clear velocity, contact data, input buffer, coyote time and hanging state on respawn below y = -10. Suppress gameplay actions when cursor is released or app unfocused; gravity can still settle airborne characters.

# UI
No new UI package, menus or HUD. Document controls in the completion message and organize the Hierarchy with descriptive obstacle names. Preserve the existing game framing.

Practice-course top-view concept (not a final art layout):
```
+---------------- existing 40 x 40 floor ----------------+
| Open spawn --> low steps --> gap platforms             |
|                       wall-jump lane --> high landing |
| Ledge practice: reachable tops --> combined route      |
+-------------------------------------------------------+
```
Use plain light/dark gray surfaces for readable silhouettes and clear platform edges. No generated art required.

# Key Asset & Context
## Verified current state
- `Assets/Scenes/MovementSandbox.unity`: 40×40 plane, camera, light, and one `Player - Human Dummy` at (0, 0.02, 0).
- Root remains an instance of `Assets/Prefabs/Prototype/GrayCylinderPlayer.prefab` with scene overrides: inactive cylinder, nested white human visual and Animator. Preserve these overrides and do not apply character-specific changes to the old cylinder prefab inadvertently.
- CharacterController: height 2, radius 0.5, center (0,1,0), step offset 0.3, skin width 0.02. Physics is code-driven; Animator root motion is off.
- `PrototypePlayerController.cs`: instantaneous 6-unit movement, 1.5-unit jump, -20 gravity, respawn and Animator Speed/Grounded/Jump integration. Keep its public deterministic simulation entry point compatible; add an overload accepting sprint state for tests and new controls.
- `PrototypeThirdPersonCamera.cs`: mouse orbit and direct 6-unit follow, no collision avoidance yet.
- `HumanDummyController.controller`: Idle, Walk, Jump (Begin), Land; Speed(float), Grounded(bool), Jump(trigger). Existing loops and events were previously inspected.
- Compatible installed clips: `Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx` / `HumanM@Run01_Forward` (Humanoid, 0.6 seconds, looping, no events) and `.../Movement/Jump/HumanM@Fall01.fbx` / `HumanM@Fall01` (Humanoid, looping, no events). No dedicated hanging/climbing clip was found in the inspected pack; do not generate or import more assets without approval.
- Dependency analysis: player script is used by the old cylinder prefab; camera and animation controller are used by MovementSandbox. Imported run/fall assets are also used by vendor demo scenes. Preserve vendor source clips, prefabs, models and import settings; reference or create project-owned copies when adjustments are needed.

## Files to modify/create
- Modify `Assets/Scripts/Prototype/PrototypePlayerController.cs`: acceleration, sprint, vertical tuning, jump forgiveness, state machine, animation parameter updates and robust respawn reset.
- Create `Assets/Scripts/Prototype/PrototypeParkourProbe.cs`: reusable wall/ledge queries, masks, top/clearance checks and optional Inspector gizmos; use nonalloc queries where practical and exclude the player's own colliders and triggers.
- Modify `Assets/Scripts/Prototype/PrototypeThirdPersonCamera.cs`: spherecast from player pivot toward desired camera position, pull forward before obstacles, restore distance as clear; exclude self/trigger colliders and retain pitch/cursor/respawn behavior. This is required because the scene now contains tall walls.
- Modify `Assets/Characters/HumanDummy/HumanDummyController.controller`: Idle/Walk/Run based on measured movement speed, Jump/Fall/Land governed by actual airborne/grounded state, and Hanging state that overrides locomotion. Reuse valid Humanoid Avatar. Preserve existing clip sources and root-motion-disabled setup. Use a clearly documented temporary held pose for Hanging; the grip animation is not finished artwork and will need a dedicated clip later. Avoid replaying landing midair or allowing grounded grace to masquerade as actual contact.
- Create `Assets/Materials/Prototype/ParkourLight.mat` and `ParkourDark.mat`, Built-in Standard matte materials.
- Modify `Assets/Scenes/MovementSandbox.unity`: add one `Parkour Practice Course` root with roughly 10–14 box-collider primitive blocks/walls, assign probe and camera masks, and wire existing player components. Keep spawn area clear and preserve floor/light/other objects.
- No package updates, vendor script changes, new character generation or combat functionality planned.

## Implementation approach
Retain CharacterController and use explicit velocity/state management with physics probes. A Rigidbody implementation is viable for physically reactive combat later, but would replace tested movement and introduce unwanted tipping/physics coupling. Geometric ledge detection is preferred over manually authored grab triggers: works on ordinary platforms and scales to future levels, at the cost of more clearance tests. Limit this milestone to static box-like level geometry; moving platforms, arbitrary meshes and corner shimmying are deferred.

# Implementation Steps
1. **Recheck context and impact.** Read the current source/scene/Animator and inspect dependencies of every asset to change. Verify Legacy input, compile health and player references. Retain original tuning as rollback values. **Dependencies:** approval of this revision.
2. **Accelerated locomotion and floaty jumps.** Extend player with tunable walk/run acceleration and braking, sprint input, horizontal velocity, air steering, gravity phases, terminal speed, coyote/buffer logic and complete respawn reset. Preserve the existing simulation API via an overload. Test speed ramps and jump envelope before building the course. **Dependencies:** Step 1.
3. **Wall and ledge detection.** Implement `PrototypeParkourProbe.cs` with near-vertical wall normals, top finding, safe capsule fit, supported ledge geometry, collision masks and optional gizmos. Integrate wall-jump cooldown and steering protection, Hanging state, drop and collision-safe ledge launch. Use freshly verified contacts, never stale collision callbacks alone. Test on temporary test geometry before final placement. **Dependencies:** Step 2.
4. **Animation integration.** Wire installed Run/Fall alongside Idle/Walk/Jump/Land, using actual horizontal speed rather than raw input. Add Hanging parameter/state and documented prototype pose; root motion stays disabled. Handle wall jump and ledge launch as jump animation events; landing follows actual contact. Validate state transitions and event compatibility without modifying vendor assets. **Dependencies:** Steps 2–3.
5. **Practice course and camera.** Create matte gray materials and static primitives under one course root. Include (a) three low steps with roughly 0.6–0.8-unit rises, (b) three broad platforms with roughly 2–3-unit gaps, (c) opposing near-vertical walls approximately 2.5–3 units apart and 5–6 units tall with a landing platform, and (d) two ledge-grab faces approximately 2.8–3.2 units tall with deep clear tops. These are initial values; verify against the measured capsule/jump envelope and adjust for accessible routes. Add obstruction-aware camera follow and ensure launch/landing clearances. All course geometry stays within the floor bounds with comfortable margins. **Dependencies:** Steps 2–4 and tested movement envelope.
6. **Verification and polish.** Run deterministic Play Mode tests, fresh scene/game-view visual review, and manual feel checks. Verify saved scene references after reopen, no missing scripts or duplicate players, no new Console errors. Remove temporary testing assets and leave editor outside Play Mode. Explain remaining temporary hanging-pose limitation. **Dependencies:** Step 5.

# Verification & Testing
- At 30/60/120 Hz, reach walk speed in approximately 0.30 seconds (one frame tolerance), accelerate to run and decelerate on Shift release, stop on input release, and keep diagonal speed equal to cardinal speed. Turn/reverse without unexplained acceleration spikes.
- Validate accepted jump is single-press, expected height/airtime is within the chosen envelope, apex float is bounded, ceiling hits cancel upward velocity, terminal fall speed is bounded, and steering remains responsive without cancelling initial wall push-off immediately.
- Coyote/buffer edge cases: jump just after a ledge departure, jump just before landing, no stale buffered repeated launch, coyote time not restored by side-wall contacts.
- Wall jumps: left/right/front/back proximity; upward and outward velocity; no jump from floor/ceiling/far wall; short same-wall cooldown; can alternate between walls; low-frame-rate push-off remains collision-safe.
- Ledges: grab valid front lip, hang without gravity drift, launch onto clear top, drop with S, no immediate regrab, no acquisition while grounded, blocked headroom/top capsule rejected, thin/overhanging invalid surfaces rejected, no clipping through walls or changing anchor to an unrelated collider. Fresh Space is required after grab.
- Respawn during fall/hanging/wall push clears all parkour state and animation triggers; camera follows correctly; no control input while released/unfocused.
- Animation: Idle/Walk/Run agree with velocity ramps; airborne Jump/Fall persists until physical landing; Hang is not overridden by movement; no root-motion drift or missing-event warnings. Inspect actual bone motion, not just state names.
- Camera: walls cannot enclose the camera; full distance restores when unobstructed, player remains framed during hanging and jumping; exclude self colliders.
- Complete each practice station and one combined route; confirm platforms are reachable under walk/run tuning without unexpected auto-step bypass of jump challenges.
- Fresh multi-angle captures verify course bounds, clear spawn, readable edges and correctly sized player. Capture gameplay during Play Mode when possible; distinguish automated checks from human feel checks not yet performed. Limit visual corrections to three rounds before reporting issues.
- Reopen `MovementSandbox.unity`, confirm all references/materials/course colliders persist and legacy cylinder prefab is not unintentionally altered. Tests may use a temporary editor bootstrap, cleaned up on completion.

---
# Historical Milestone 1 (completed; superseded by the revision above)
The following original plan is retained as a record, not instructions to recreate or revert the current project.

# Project Overview
- **Game Title:** Working label: Platforming/Fighting Prototype (final title deferred).
- **High-Level Concept:** A 3D platforming/fighting game, beginning with an intentionally minimal movement sandbox: a white-and-gray checkerboard floor and a gray cylinder player. Combat, enemies, and platform layouts are not part of this first milestone.
- **Players:** Single local player for this prototype.
- **Inspiration / Reference Games:** Not specified; defer until designing gameplay beyond the sandbox.
- **Tone / Art Direction:** Neutral graybox, simple primitives, no finished character artwork.
- **Target Platform:** Windows PC; existing StandaloneWindows64 target.
- **Screen Orientation / Resolution:** Landscape; existing 1920×1080 defaults, no project-setting changes.
- **Render Pipeline:** Built-in, Unity 6000.6.7f1.

# Game Mechanics
## Core Gameplay Loop
Walk around the test floor, orbit the camera, jump and land, and repeat to evaluate basic movement responsiveness. The checkerboard supplies spatial reference and visible motion cues; this foundation prioritizes player control and competence before adding fighting mechanics.

## Controls and Input Methods
User selected third-person movement plus jumping and a checkerboard floor.
- WASD / existing Horizontal and Vertical axes: camera-relative movement, normalized to prevent faster diagonals.
- Space / existing Jump binding: grounded jump; gravity and grounded downward bias handled by a CharacterController.
- Mouse: third-person orbit with clamped pitch and a following camera, updated after player motion.
- Escape: release cursor; click the Game view to recapture it. Release cursor on disabling the camera/controller and on focus loss.
- Preserve **Legacy Input Manager**, verified activeInputHandler = 0. Horizontal, Vertical, Jump, Mouse X and Mouse Y exist. No package installation or input migration.
- Initial tunable values: approximately 6 units/sec movement, 1.5-unit jump height, -20 units/sec² gravity, 6-unit camera distance.
- Walking off the floor triggers a simple respawn at the starting position below y = -10; clear fall velocity and reset camera follow state.

# UI
No menu, HUD, Canvas, or UI assets in this milestone. Controls will be documented in the completion message.

Game-view composition:
```
+-----------------------------------+
|                                   |
|       white / gray checkerboard   |
|                [gray cylinder]    |
|                                   |
+-----------------------------------+
```
The camera starts behind and above the cylinder, keeping both the player and a useful expanse of floor visible.

# Key Asset & Context
## Existing context
- Current scene is unsaved and contains only Main Camera (Camera, AudioListener) and Directional Light (Light).
- No existing project scenes, prefabs, scripts, or Markdown plans were found in the initial inventory.
- Retain and configure the existing camera and light; do not delete unrelated content if the scene changes before implementation.
- No existing gameplay asset requires modification. Recheck scene contents before implementing and inspect dependencies before overwriting any pre-existing destination asset; avoid naming collisions.

## Assets to create
- `Assets/Scenes/MovementSandbox.unity`: saved minimal sandbox.
- `Assets/Scripts/Prototype/PrototypePlayerController.cs`: CharacterController movement, camera-relative heading, grounded jumping, gravity, respawn. Expose movement and jump tuning in Inspector.
- `Assets/Scripts/Prototype/PrototypeThirdPersonCamera.cs`: target reference, yaw/pitch orbit, follow, cursor handling; reset follow on respawn.
- `Assets/Materials/Prototype/CheckerFloor.mat`: Built-in Standard material with checker texture.
- `Assets/Materials/Prototype/GrayPlayer.mat`: matte medium-gray Standard material.
- `Assets/Textures/Prototype/CheckerFloor.png`: tiny generated white/gray checker texture, repeat wrapping and point filtering, tiled into roughly 2-unit squares on a 40×40-unit plane.
- `Assets/Prefabs/Prototype/GrayCylinderPlayer.prefab`: root with CharacterController and movement script; child Unity cylinder mesh, 1-unit diameter and 2-unit height, with primitive collider removed to avoid overlapping collision systems. Root is at foot level; visual and CharacterController center are 1 unit above it.
- Scene objects: Ground (single flat plane with collision), Player, existing Main Camera, existing Directional Light. No decorative additions or obstacles.

## Approach
Use CharacterController rather than Rigidbody movement: straightforward grounded collision and direct, tunable platformer movement without cylinder tipping. Rigidbody is viable for physics-driven combat later, but adds unnecessary dynamics now. Use a repeatable checker texture on one floor collider rather than many tile objects, avoiding seams and unnecessary scene objects. No character model generation or animation required for a primitive stand-in.

# Implementation Steps
1. **Recheck and establish assets.** Verify the scene is still the empty starting scene and destination paths are unused. Create prototype folders, checker texture, and the two Standard materials. Preserve unrelated assets and current project settings. **Dependencies:** approval; context verification.
2. **Implement movement.** Create `PrototypePlayerController.cs` using the existing legacy axes and CharacterController. Implement normalized camera-relative motion, gravity, grounded jump, heading changes and below-floor respawn with cleared velocity. **Dependencies:** Step 1; verified input axes.
3. **Implement camera.** Create `PrototypeThirdPersonCamera.cs` with serialized target and tuning, pitch limits, LateUpdate follow and mouse orbit. Add cursor release/recapture and focus handling; follow respawn immediately without sweeping across the map. No obstacle-avoidance system needed on the empty floor. **Dependencies:** Step 2.
4. **Assemble and save.** Create the 40×40 checkerboard plane at y = 0. Assemble and save the cylinder prefab with a foot-level root and a single CharacterController. Place it at the center just above the surface; wire camera and movement references explicitly. Configure the existing Main Camera for an angled follow view and reuse the existing light without baking. Save as `MovementSandbox.unity`. Do not alter build profiles/settings for this Play Mode milestone. **Dependencies:** Steps 1–3, successful script compilation.
5. **Verify and visually review.** Run the checks below, inspect Console logs, capture the scene from multiple angles and the gameplay view if tools permit. Check cylinder contact, checker visibility and readable framing. Correct issues using the same assembly logic and repeat fresh captures; maximum three visual correction rounds. Save final assets and leave the editor out of Play Mode. **Dependencies:** Step 4.

# Verification & Testing
- Compilation: both scripts compile with no new errors; no new dependencies or compatibility defines.
- Wiring: one active main camera and AudioListener; camera target and player camera references assigned; exactly one movement collider on player; floor collider enabled.
- Placement: cylinder is upright, about 2 units tall, centered at spawn, and rests on the plane without floating or sinking.
- Movement: WASD directions follow camera yaw; diagonal speed equals straight-line speed; player does not tip; motion remains consistent at different frame rates.
- Jump: Space while grounded launches; holding Space does not repeatedly launch; no midair repeat jump; lands reliably and stops accumulating downward velocity on the floor.
- Camera: yaw orbit works; pitch clamps prevent flipping; following does not visibly lag behind jumps or respawn; initial view shows player and floor.
- Cursor: Escape releases, click recaptures, focus loss and Play Mode exit release; no uncontrolled movement/look while cursor is released.
- Bounds: walk beyond plane edge; fall and respawn without retaining fall speed or leaving the camera behind.
- Persistence: reopen the saved scene and confirm materials, prefab and serialized references persist.
- Visual review: use fresh multi-angle captures to confirm a white-and-gray checker floor, plain gray cylinder, no overlaps or extra objects. Report any checks unavailable through tools as manual follow-up rather than claiming they passed.
