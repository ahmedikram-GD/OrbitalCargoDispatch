# Integrate these assets into the existing web game

The user wants to keep the functioning Orbital Cargo Dispatch game and replace its prototype visuals with these authored assets. The published artifact was inspected: it uses Three.js r128, MeshStandardMaterial/MeshBasicMaterial and procedural geometry; renderer.shadowMap.enabled is currently false. Do not replace the game with the included asset viewer.

## Scope

Integrate four assets: standard cargo crate, 2 m floor, 2 m wall and ceiling fixture. Preserve the game's controls, UI, tutorial, pooling, timers, station logic, camera, carrying, slot layout, sorting colours, scanner reveal, launch/payout rules and progression. Existing non-standard cargo and stations should continue to function with their current visuals.

## Load once before gameplay

Keep the existing Three.js r128 script. Do not load a second copy of Three.js. Add the matching vendor/GLTFLoader.js and orbital-assets.js after it, using paths available to the game. For a regular static project:

```html
<!-- Existing Three.js r128 script comes first. -->
<script src="web-assets/vendor/GLTFLoader.js"></script>
<script src="web-assets/orbital-assets.js"></script>
```

```js
const assets = await OrbitalAssets.load('./web-assets/');
const visual = assets.create('crate-standard', { size: 0.8, pivot: 'bottom' });
logicalCrateRoot.add(visual);
```

The snippet illustrates attachment; adapt `logicalCrateRoot` to the actual source. Make startup await the load and report errors. Keep Start disabled until models are ready, or deliberately keep the old visuals as a clear fallback. Do not fetch a new model per spawn/frame.

## Gameplay-safe visual replacement

- Attach the authored model under the current logical crate entity. Keep the collider/interaction data and bottom-centre carry pivot. Hide only the old visual meshes. If the entity root itself is a Mesh, do not set its Object3D.visible=false and then add a child beneath it: that would hide the child too. Use a dedicated visual group, or a private invisible material on the retained proxy while leaving the logical root visible.
- Replace standard cargo only. Do not globally tint the entire atlas to create cryo/volatile/junk; that also tints labels, seals and lights. Preserve the existing type-specific looks and reset the correct visual on pool reuse.
- During scanner X-ray, hide the authored exterior and show the existing transparent proxy/hidden item. Restore the exterior for clean scans and when a pooled crate is reconfigured. A bad scanned crate should keep the existing reveal behaviour on pickup.
- Parent crates and the shuttle visual as the game already does for launch. Do not add rigidbody/physics carry behaviour.
- For floors and walls, use native metre scale and retain collision boundaries independently of decorative geometry. The floor top is Y=0. The wall front faces +Z; rotate around Y to face the room. Fixtures hang down from their Y=0 mounting plane.
- Pool/reuse model instances and shared textures. Do not dispose their shared geometry/materials during individual crate cleanup or shift transitions. assets.dispose() is only for whole-app shutdown.

## Rendering

Use MeshStandardMaterial data from GLTFLoader; do not replace imported materials with flat colours. Keep colour/emission maps in sRGB and ORM data linear. The loader handles the glTF maps correctly. Configure renderer outputEncoding=THREE.sRGBEncoding and an intentional tone mapping/exposure for r128. Do not copy newer-version `outputColorSpace` APIs into r128 code.

Provide an environment reflection map for metal. preview.js includes a self-contained PMREM environment that needs no external HDR file. Adapt its lighting to the actual room rather than copying its viewer camera or UI. Add a small number of warm practical lights; emissive fixture surfaces do not illuminate the room by themselves. If performance permits, enable shadows for one important light first, then test. The prototype currently disables shadows.

Do not assume Unity's baked room lighting is inside these GLBs. Use browser lighting or a separately authored room-lightmap workflow. Check exposure so the navy crate stays readable and the deck does not become uniformly white.

## Asset access in Claude artifacts

These are real binary files, not references to the user's D: drive. They must be bundled in an environment that serves them or hosted at URLs accessible to the artifact, with the required cross-origin access. Do not claim the published artifact is updated until its own page successfully loads those files.

If the Claude artifact sandbox cannot access the uploaded models, export the existing game source into a regular static web project, put this folder under web-assets/, and use a normal HTTP server. Preserve the game; do not silently replace it with a static mockup. Avoid embedding megabytes of base64 in prompts/source unless there is no practical alternative and the user accepts that cost.

## Acceptance checks

1. All four models load without console/network errors; compare with the supplied browser preview.
2. Standard crate remains bottom-aligned, fits the existing 0.8 m bounds and is carried without blocking the crosshair.
3. Other crate types retain clear destination colours and mechanics.
4. Suspicious crate scan reveals the hidden item; pickup and reuse reset the correct exterior.
5. Six crates can be placed, launched, paid out and returned without changing game rules.
6. Floor/wall decoration does not block movement or overlap conveyor openings.
7. Test desktop controls and actual mobile/browser performance. Loading the assets in this viewer does not substitute for those gameplay checks.
