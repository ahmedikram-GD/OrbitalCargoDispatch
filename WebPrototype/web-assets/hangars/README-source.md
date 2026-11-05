# Orbital Cargo Dispatch assets for Claude

Integrate the supplied shuttle and hangar GLB assets into the existing web prototype. Use the current application renderer and controls.

## Models

- models/four-view-shuttle.glb is shuttle A in blue and white.
- models/shuttle-b.glb is shuttle B in teal and white.
- models/shuttle-c.glb is shuttle C in yellow.
- models/hangar-shell.glb is the common hangar shell.
- models/hangar-sign-a.glb, hangar-sign-b.glb and hangar-sign-c.glb are the separate signs.

All GLBs include their textures. They use glTF Y-up coordinates. Both the ships and hangars face positive Z. The shell and sign exports have identical origins. Add them to the same parent with identity transforms.

The A, B and C signs show 1/6, 3/6 and 0/6. These are static reference values. For gameplay, replace the counter glyphs with dynamic text and change the slot materials when cargo changes.

Reuse one loaded shell across bays. Clone scene objects while sharing geometry and textures. Shuttle variants have identical geometry and different baked paint textures. Their GLBs are independently loadable.

## Integration

Copy models into a publicly served asset folder and use the corresponding URL in your existing GLTFLoader. The included integration-example.js provides a reusable loader and a fitted shuttle placement inside each bay. Set assetBase to the actual public URL prefix. Use your existing Three.js imports or adapt the sample imports to your project.

The example fits the shuttle into the current interior envelope and places its feet on the deck at Y 0.64. Check placement against existing gameplay colliders and navigation before adopting it.

The example uses the existing Three.js renderer and lighting. Set output color handling for sRGB textures and add environment lighting as appropriate to that version of Three.js. The supplied previews use bundled legacy Three.js scripts and are standalone visual review pages.

## Local review

Serve this extracted folder over HTTP, then open shuttle.html or hangar.html. Use their selectors to inspect each variant. With Python installed, run python -m http.server 8770 in the extracted folder and open http://127.0.0.1:8770/shuttle.html.

## Asset limits

Each shuttle has 4564 triangles. The shared hangar shell has 10968 triangles. Sign assemblies add 1124 to 2080 triangles. These meshes retain multiple material primitives. Optimize draw calls and textures after checking the complete scene on the target phone. Mobile performance has not been measured.

The hangar exterior sides, roof and rear are inferred from the front reference. Some surface shading is baked into the source reference textures.

## Included sources and previews

The sources folder contains editable Blender files for the blue, teal and yellow shuttles and the modular A/B/C hangars. Blender renders are included as visual checks. The export GLBs are the files used by the browser.
