# Orbital Cargo Dispatch — web station models

Three real Blender models for replacing the web prototype's procedural station visuals. These are optimized interpretations of the supplied standalone references; they do not reproduce every scratch or lighting effect in the generated images. The original web prototype and Unity scene have not been edited.

| Model | Triangles | Mesh draws, before shadows | GLB bytes | Dimensions X/Y/Z, metres |
|---|---:|---:|---:|---|
| Conveyor | 3,426 | 3 | 389,652 | 5.695 / 1.115 / 1.360 |
| Scanner | 2,386 | 3 | 328,260 | 2.525 / 2.830 / 1.829 |
| Disposal chute | 2,524 | 1 | 343,952 | 2.800 / 2.2225 / 2.800 |
| Total | 8,336 | 7 | 1,061,864 | |

Counts are asset geometry, not the whole game. Shadows add passes; glass costs fill rate. Desktop browser validation is complete; performance on a physical phone remains untested.

## Preview

Run START_PREVIEW.cmd, then open http://127.0.0.1:8767/ . Python must be installed. Serve over HTTP, not file://. Inspect each model, orbit, and toggle motion and shadows. Shadows are off by default. The preview uses Three.js r128 to match the inspected Claude artifact. No external CDN is required for this preview.

## Integrating into the existing prototype

Copy this folder into the web app as `station-assets/`. Keep the existing game logic, target volumes, and cargo movement; replace the visible station meshes only. Load the matching GLTFLoader after Three.js and before station-assets.js. This loader exposes `OrbitalStations`, independent of the earlier `OrbitalAssets` crate/environment adapter.

```html
<script src="station-assets/vendor/GLTFLoader.js"></script>
<script src="station-assets/station-assets.js"></script>
```

The supplied vendor loader matches Three.js r128. For a different Three version, use that version's matching loader and adapt its module imports. Do not load a second copy of Three.js into an existing game.

```js
const stations = await OrbitalStations.load('./station-assets/');
const conveyor = stations.create('conveyor');
const scanner = stations.create('scanner');
const chute = stations.create('disposal-chute');
scene.add(conveyor, scanner, chute);
// Set their positions/rotations to the existing gameplay layout.
```

GLBs contain embedded PNG PBR textures. One shared 1024 atlas uses base colour, ORM (R=occlusion, G=roughness, B=metallic), and emission. Conveyor uses an additional repeating 256 belt colour/ORM pair. The loader deduplicates the repeated atlas after loading. Approximately 16.7 MiB of uncompressed RGBA texture storage including mipmaps is expected for those five unique images; actual browser allocation can vary. Embedded duplication remains in download files, keeping each GLB independently loadable. No Draco/KTX decoder, transmission shader, real-time lights, or bloom is required.

The prototype needs balanced image-based environment lighting to make metals readable. See preview.js for a self-contained studio environment. Emissive pixels do not illuminate surrounding surfaces on their own. Match the room lighting separately.

## Stable anchors and moving parts

Coordinates are metres, glTF +Y up. Scanner and chute face +Z. Do not automatically centre the chute's vertical bounds: its correct pivot is the deck plane Y=0, with the throat extending down to -.90.

Conveyor:

- `Conveyor_Belt` — separate mesh/material, repeat UV V along X travel. Native belt surface Y=.85.
- `Conveyor_SurfaceStart` — (-2.5, .85, 0).
- `Conveyor_SurfaceEnd` — (2.5, .85, 0).
- `Conveyor_IntakeGuard` — optional separate guard. Hide it if it obstructs the game's spawning route.
- Belt motion is visual only; keep the game's existing kinematic cargo movement.

```js
const belt = conveyor.getObjectByName('Conveyor_Belt');
// A world speed of .15 metres/s corresponds to .225 UV/s.
belt.material.map.offset.y = -elapsedSeconds * .225;
```

Belt materials/textures are shared between cloned conveyors by the adapter. This synchronizes their visual scrolling. Clone the material and map if independently controlled belts are needed, and dispose those extra clones at app shutdown.

Scanner:

- `Scanner_CargoSlot` — (0, .255, 0), position a crate's bottom here.
- `Scanner_ControlTarget` — (1.2, 1.3, .75).
- `Scanner_ScanBeam` — authored local Y=1.3; animate between .57 and 2.03, then reset/hide when idle.
- `Scanner_Glass` — three outward-facing single-sided panes, alpha .16, no front pane. The adapter disables depth writing to reduce transparent occlusion artifacts. No refractive transmission.

```js
const beam = scanner.getObjectByName('Scanner_ScanBeam');
beam.position.y = 1.3 + Math.sin(elapsedSeconds * 1.8) * .73;
```

The terminal display and signs are baked into the atlas. For dynamic result UI, overlay a separately managed screen/sign without duplicating the whole station material.

Chute:

- `Disposal_DropTarget` — (0, 0, 0), deck-plane centre.
- `Disposal_ThroatBottom` — (0, -.90, 0).
- Opening diameter 1.98 m; mounting flange diameter 2.8 m.
- Cut a matching opening in the visible floor. Placing this model over an intact floor plane will fill the hole. Keep the existing disposal trigger/assisted throw logic.

## Resource lifetime

`create()` shares immutable geometry and station materials. Remove station instances without disposing their shared geometry/materials. Call `stations.dispose()` once when the entire app/scene is permanently shut down. The adapter is suitable for a few stationary stations, not a full instancing or LOD system.

## Included source and validation

`source/Stations_Authored.blend` contains the isolated editable station scene, packed textures, and stations laid out side by side. Native Blender is Z-up. Exported GLBs are Y-up and at local origin. Regeneration scripts are in source; update their project ROOT/OUT paths when running on another machine. They require the Pillow module for texture authoring, already available in the authoring environment; there are no new runtime packages.

`python verify_station_assets.py` checks binary layout, hashes, real triangle counts, index bounds, finite geometry, UV/normals, embedded PNG sizes, required nodes, and absence of required glTF extensions. The browser also checks loaded geometry counts and dimensions. Reference PNGs are included under references; actual web renders are under previews.

Art is ready for integration and visual iteration. It has not been inserted into the published Claude artifact, collision-tested in that prototype, or profiled on a phone.
