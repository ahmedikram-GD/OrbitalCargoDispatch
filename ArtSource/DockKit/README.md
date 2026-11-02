# Authored dock environment kit

Direct art reference: Reference_Target.png, the user's approved generated concept image.

The floor follows the broad grey metal surface in that reference, with narrow seams and restrained wear. Wall modules use recessed navy panels, dark structural edges, service plates and lower bumpers. A recessed warm light housing is used at ceiling scale and as a smaller wall practical.

- Dock_Floor_2m.fbx: 2 x 2 m, top at zero. 216 Blender triangles before Unity import optimisation.
- Dock_Wall_2m.fbx: 2 m wide, 3.8 m high. 2,484 Blender triangles.
- Dock_Light_160cm.fbx: 1.66 m housing, mount at zero, hangs downward. 1,728 Blender triangles.
- All three share one 2048 atlas material: colour, metallic R / smoothness A, emission, and AO (green channel sampled by URP).
- DockKit_Authored.blend contains the active kit scene and dependencies. Source materials and the atlas master remain available alongside exported single-material modules.
- build_dock_kit.py rebuilds in Blender; run in a fresh file. It saves a source-only blend library, exports the modules and bakes their atlas maps. Blender Z-up to Unity Y-up conversion is retained by the Unity importer; preserve the imported root rotation when placing modules.

Unity prefabs and importer: Assets/OrbitalCargo/Art/EnvironmentKit and DockEnvironmentArt.cs. See docs/VISUAL_PASS_04.md for assembly, bake instructions, verification and limits.
