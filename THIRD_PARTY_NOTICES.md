# Third-party notices

## Z-Anatomy (3D anatomy models and descriptions)

- Source: https://www.z-anatomy.com/
- Licence: **Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)**. Full text: https://creativecommons.org/licenses/by-sa/4.0/
- Files in this repository derived from Z-Anatomy:
  - `Assets/Models/ZAnatomy_LOD/**` (meshes decimated and re-grouped in Blender, plus `ZA_*` materials)
  - `Assets/Prefab/ZAnatomy.prefab`
  - `Assets/Resources/AnatomyDescriptions.json`
- Changes made: polygon reduction for mobile VR, regrouping into 4 systems and 75 grabbable structure groups, pivots re-centred, and removal of some structures (omentum, pleura, liver segments, bursae).
- These derived files are shared under the same CC BY-SA 4.0 licence.

## Meta XR SDK (`com.meta.xr.sdk.all` 72.0.0)

- © Meta Platforms Technologies, LLC. Used under the Meta Platform Technologies SDK License Agreement: https://developers.meta.com/horizon/licenses/
- Not redistributed in this repository. Unity's Package Manager downloads it.

## Unity packages

- OpenXR Plugin, XR Plugin Management, TextMeshPro, Timeline, uGUI and glTFast are © Unity Technologies, under the Unity Companion License. Unity's Package Manager downloads them.
- `Assets/TextMesh Pro/` contains the TextMeshPro essential resources that Unity imports into every project that uses TMP.

## Icons

- `Assets/Meta_Productivity_App/` contains the menu prefab and icons imported from a Meta MR sample, used under the Meta SDK licence. The icons named `*_FILL0_wght400_*` are Google Material Symbols, licensed under Apache License 2.0.
