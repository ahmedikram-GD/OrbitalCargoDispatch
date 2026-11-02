# Orbital Cargo Dispatch — web asset handoff

This folder contains the authored crate, floor, wall and light fixture, converted for the Three.js r128 prototype at https://claude.ai/artifact/YRqmkFmkiPNvohaEkWqrPM.

## Preview

On Windows, run START_PREVIEW.cmd, then open http://127.0.0.1:8766. Python 3 is needed only for this local preview server. Alternatively, serve this folder with any static HTTP server. Opening index.html with file:// can block model loading.

No build step, npm install, account, API key or internet connection is needed once the folder is downloaded. The required Three.js files are included under vendor/ with their MIT licence.

The preview is an asset viewer, not the complete game. The existing Claude artifact has not been edited or republished. Read CLAUDE_HANDOFF.md to integrate into that game's source.

preview-crate.jpg and preview-room.jpg are actual screenshots of this browser preview, not Blender renders.

## Assets

| File | Geometry | Native pivot / orientation |
| --- | --- | --- |
| models/crate-standard.glb | 6,864 triangles | Bottom centre; use adapter size 0.8 for gameplay |
| models/floor-2m.glb | 216 triangles | 2 x 2 m; top surface at Y=0 |
| models/wall-2m.glb | 2,484 triangles | 2 m wide, 3.8 m high; bottom at Y=0, front toward +Z |
| models/fixture-160cm.glb | 1,728 triangles | 1.66 m wide; ceiling mount at Y=0, body hangs downward |

All files use metres, +Y up, one mesh primitive and one physically based material. Each GLB embeds its colour, metallic/roughness and emission maps. The kit also embeds AO in the shared ORM map. All textures are at most 1024 pixels. No Draco, KTX, Meshopt or external image decoder is required. manifest.json lists sizes and checksums.

Metallic/roughness is converted correctly from source maps: glTF roughness is G and metallic is B. This is not Unity's metallic/smoothness layout. The source albedo is used rather than Unity's Gamma-specific brightness calibration. Kit emission is tinted warm amber.

## Reuse

orbital-assets.js exposes `await OrbitalAssets.load(baseURL)` and `assets.create(id, options)`. It shares the kit material/texture set across floor, wall and fixture models. Instances share geometry/materials; detach/reuse them instead of disposing shared resources per crate. Call assets.dispose() only when the whole game/viewer is finished with those resources.

Do not paste GLB files as base64 into chat prompts. Give Claude the extracted files/folder or attach the ZIP, then provide accessible model URLs if its artifact environment needs them. Binary hosting/access in the published artifact must be verified; local D: paths cannot be read by website visitors.

## Validation and limitations

The four models were loaded and inspected in a real browser with Three.js 0.128.0. Geometry count, required material maps, floor size, wall orientation and module pivots are checked by the preview. The CPU-only structural check is `python verify_assets.py`.

Browser console errors were checked. Individual crate, floor, wall and underside fixture views were inspected. This establishes asset compatibility with that renderer version. It does not establish gameplay integration into the published artifact or phone performance.

The viewer supplies its own lighting, shadows and procedural reflection environment. Unity room lightmaps/cubemap are not automatically transferred into glTF. The web game needs its own matching lighting setup. Only standard cargo is included; preserve the existing cryo/junk/volatile looks until their authored variants exist. Shuttle, scanner, conveyor and chute models are not part of this package.

Models and textures were authored for this project. Three.js, GLTFLoader and OrbitControls are bundled under the MIT licence in vendor/THREE-LICENSE.txt.

Technical references: [glTF material channels](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html), [Blender glTF export](https://docs.blender.org/manual/en/latest/addons/import_export/scene_gltf2.html).
