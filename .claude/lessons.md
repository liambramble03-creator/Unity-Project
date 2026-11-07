# Lessons

A log of problems that were fixed in this project: symptom, cause, fix, and how to spot it quickly next time.
Read this before debugging; append a new entry (newest at the bottom) after every fix.

---

## 2026-11-07: Slide state plays, but the character shows the wrong (previous) pose

**Symptom:** The Animator showed the Slide state lit up in Play mode, and the FBX's Animation-tab preview showed the correct pose. In game, the character froze in an unrelated crouch, which was whatever pose it had before entering Slide.

**Cause:** The imported clip was **empty** (0 curves, `humanMotion = false`). The clip settings in `HumanDummy@Slide.fbx.meta` had been hand-written with `firstFrame: 0` / `lastFrame: 1`, but the Blender take spans frames **1–11**. Unity silently produced an empty clip from the out-of-range frames. The Animation-tab preview was misleading: with an empty clip it just shows the model's default pose, and that default pose *was* the slide pose (Blender writes the current pose into the FBX).

**Fix:** Rebuilt the clip from `ModelImporter.defaultClipAnimations`, so Unity supplies the real take name and frame range. Set loop and bake-into-pose, `SaveAndReimport()`, then reassigned `state.motion` on the Slide state via `AnimatorController`.

**Next time:**
- Don't hand-write `clipAnimations` frame ranges or take names in `.meta` files; derive them from `importer.importedTakeInfos` / `defaultClipAnimations`.
- Don't trust the import preview as proof that a clip works. Verify with a temporary `Assets/Editor` script (`[InitializeOnLoadMethod]` + `EditorApplication.delayCall`) that dumps `AnimationUtility.GetCurveBindings(clip)`, `clip.humanMotion` and each state's `motion` to a text file. Clicking into Unity triggers it. Delete the script afterwards.
- "State is active but the pose doesn't change" = empty or unbound clip. Check the curve count first.

## 2026-11-07: "Copied Avatar Rig Configuration mis-match … Parent for 'B-root' … 'HumanDummy@Slide' was found instead of 'Rig'"

**Symptom:** Rig-import error on a Blender-exported humanoid FBX that reused the Kevin Iglesias female avatar (`Copy From Other Avatar`).

**Cause:** Only the armature was exported (no mesh), so the FBX had a single top-level node, `Rig`. Unity collapses a lone root node into the file's root GameObject, so `Rig` became `HumanDummy@Slide`. The vendor avatar's skeleton expects `Model root → Rig → B-root`.

**Fix:** Switched to `avatarSetup: 1` (Create From This Model), kept the vendor's human bone mapping, and edited the `skeleton:` list: renamed the `Rig` entry to `HumanDummy@Slide` with an empty parent, set `B-root`'s parent to it, and removed the vendor-only `Human_DummyModel_F(Clone)` and `HumanF_BodyMesh` entries.

**Next time:** Export the **armature and the body mesh together** from Blender so the hierarchy matches the vendor files and `Copy From Other Avatar` works. The bone names (`B-hips`, `B-spine`, …) match the Kevin Iglesias rig.

## 2026-11-07: Shell redirect emptied a `.meta` file

**Symptom:** `HumanDummy@Slide.fbx.meta` became 0 bytes after a failed command.

**Cause:** `awk … < input > "$M"`: the shell truncates the output file before the command runs, so when awk failed (missing scratch folder), the meta was left empty. Unity would then have regenerated the meta with a **new GUID**, breaking every reference to the asset.

**Fix:** Restored the original contents (same GUID) immediately, then rebuilt the new version in the scratchpad and copied it over only after it succeeded.

**Next time:** Never redirect straight into a Unity `.meta` / asset file. Write to a temp file, verify it, then copy. Make sure the scratchpad directory exists (`mkdir -p`) first.
