# First Blender cargo-crate benchmark

Created through the Unity MCP Blender bridge after verifying Blender 5.2 connectivity.

- CargoCrate_Benchmark.blend contains a separate benchmark scene; the original default scene is retained.
- build_crate.py reproduces the model and image textures in a new scene.
- Painted shell, inset panels, reinforcement, seals, handles, latches, barcode and cyan status strips.
- 6,864 triangles before any geometry optimisation; eight original material slots.
- Colour and roughness textures are real PNG assets, packed into the blend as well.
- crate-benchmark.png is a Blender Cycles studio render, not Unity gameplay.
- Native FBX import succeeded at Assets/OrbitalCargo/Art/BlenderBenchmark/CargoCrate_Benchmark.fbx. It is not placed in Game.unity and does not replace the gameplay crate.
- The follow-up URP transfer is now integrated for standard cargo: one atlas material, scanner-compatible exterior switching, and a dock reflection cubemap. See docs/VISUAL_PASS_03.md for validation and remaining work. Mobile profiling remains outstanding. Blender studio appearance does not establish Unity appearance.

No package or project setting was changed by this benchmark. No gameplay tests were needed for this isolated asset import.
