# Changelog

All notable changes to this project. Build numbers match the internal test APKs.

## [Unreleased]
- Labels for grabbed parts (name + short description)

## v12 — Snap, without the ghost outline
- The snap-target ghost outline is now **off by default** (`AnatomyGrabber.showGhost`). Snap-back still works.

## v11 — Snap-back
- Release a part within **10 cm** of its home position and it glides back over 0.25 s, restoring position, rotation and scale.
- Parts released further away stay floating.
- **Reset** also cancels running snap animations.
- New `Anatomy/Ghost` X-ray shader and `ZA_Ghost` material.

## v10 — Polish
- Removed the debug HUD panel.
- The menu's yellow border now fits the buttons.

## v9 — Grab the famous parts
- 75 major structures are grabbable: pinch or grip to grab, two hands to scale (0.5×–3×), with a hover highlight.
- Pinch lock-on: if the fingers occlude each other during the pinch, the grabber uses the part that was highlighted just before.
- Fix: the body disappeared or reset on pinch, because Meta maps a pinch to the A/X buttons. Controller shortcuts now only run in controller mode.

## v6 – v8 — Hand-tracking fixes
- Body placement uses the **right hand only**, so the left hand is free for the menu.
- More reliable pinch detection (Interaction SDK and OVRHand strengths combined, lower thresholds).
- The palm menu follows the left palm in hand-tracking mode.
- A dwell-to-place experiment was added, then removed after feedback.

## v3 – v5 — Performance
- Replaced the 19 M-triangle Sketchfab model with **Z-Anatomy** at about 1 M triangles, with named, separable parts.
- Static batching, fixed foveated rendering and a 72 Hz target.
- Five layer presets.

## v1 – v2 — First MR prototype
- Body placed in passthrough on the real floor.
- Fix: the body was 6.4× too large and spawned around the user. The body now keeps a minimum distance of 1 m.
