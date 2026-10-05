# Architecture

This document explains how XR Human Anatomy is put together and why. For setup steps, see the [README](../README.md).

## Scene graph

```
HunanAnatomy.unity
├── [BuildingBlock] Camera Rig          OVRCameraRig + OVRManager, tracking origin = Floor Level
│   └── …/LeftControllerAnchor/Menu     Menu.prefab instance (follows the left palm in hand mode)
├── [BuildingBlock] Passthrough         Real-world background
├── [BuildingBlock] Poke Interaction    Poke-able menu buttons
├── MRUK                                Room / floor anchors (Meta MR Utility Kit)
├── MenuFeature                         MenuFeature + MenuRayPointer
├── Reticle                             Placement target shown before the body is placed
└── AnatomyPlacer                       AnatomyPlacer + AnatomyGrabber + QuestPerformance

Spawned at runtime by AnatomyPlacer (pre-loaded at start, shown on Place):
ZAnatomy.prefab                         AnatomyLayers on the root
├── Skeleton    ─┐
├── Muscles      │  "systems" — switched on/off by layer presets
├── Organs       │
└── Nervous     ─┘
      └── <AnatomyPart> groups (75 total), e.g. Skull, Heart, Brain
```

## Runtime flow

```
Start
 └─ AnatomyLayers.Awake
     ├─ per AnatomyPart: CaptureHome() → PrepareGrabbable()
     │     ├─ BoxCollider per mesh piece (min size 4 mm)
     │     ├─ combine pieces into one mesh per material   (fewer draw calls)
     │     │    └─ mirrored pieces (negative scale) get flipped triangle winding
     │     └─ kinematic Rigidbody
     └─ StaticBatchingUtility.Combine(everything that is NOT grabbable)

Every frame
 ├─ AnatomyPlacer.Update      reticle + right-hand pinch → Place()
 └─ AnatomyGrabber.LateUpdate
       ReadInput (hand pinch with hysteresis, or controller grip/trigger)
       → TryGrab / Release / two-hand scale
       → Follow (part keeps its offset relative to the hand)
       → UpdateSnapAnims (smoothstep back to its home pose)
       → hover highlight (MaterialPropertyBlock on _Color)
```

## Key design decisions

| Decision | Why |
|---|---|
| **Custom grabber instead of XR Interaction Toolkit.** | The project already uses the Meta Interaction SDK hands. Adding XRI as well would bring two input stacks into the project. The custom grabber is small (~600 lines), works the same way for hands and controllers, and supports pinch lock-on (it remembers the part highlighted just before the pinch, because fingers occlude each other as they close). |
| **Only 75 "famous" structures are grabbable.** | This keeps colliders, draw calls and cognitive load low. Niche structures (individual veins, bursae and so on) were left out on purpose. |
| **Mesh combining per part at runtime.** | The source model has thousands of small pieces. Combining them per part and per material cuts draw calls while keeping each part movable on its own. |
| **Shortcuts disabled while hands are tracked.** | Meta maps a hand pinch to the A/X buttons. Without this guard, a right pinch would trigger *Place* and a left pinch would trigger *Reset*. |
| **Snap by distance, not by collider.** | A distance check against the stored home pose is cheap and predictable. It also restores rotation and scale exactly. |
| **Ghost outline is optional (`showGhost`, off by default).** | It was tested, then turned off after user feedback. The code and the `Anatomy/Ghost` X-ray shader are kept so it can be switched back on from the Inspector. |

## Performance budget (Quest 3S)

| Item | Value |
|---|---|
| Triangles in view | ≈ 1 M (Z-Anatomy, decimated in Blender) |
| Shading | Standard shader, no real-time shadows on the body |
| Foveation | Fixed foveated rendering, High, dynamic |
| Display refresh | 72 Hz |
| Batching | Static batching for non-grabbable meshes; per-part combining for grabbable ones |

## Extending

- **Make a new structure grabbable:** group its meshes under an empty GameObject inside the right system in `ZAnatomy.prefab`, put the pivot at the centre of its bounds and add `AnatomyPart` (fill in `displayName` and `system`). `AnatomyLayers` handles colliders and mesh combining automatically.
- **Add a layer preset:** add an entry to the presets list on `AnatomyLayers` and choose which systems it shows and which parts it hides.
- **Labels (next milestone):** `Assets/Resources/AnatomyDescriptions.json` already holds 1,898 structure descriptions keyed by name. The plan is to show the name and a short description when a part is grabbed.
