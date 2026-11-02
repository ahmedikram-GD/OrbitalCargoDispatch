# First Blender cargo-crate benchmark

Created through the Unity MCP Blender bridge after verifying Blender 5.2 connectivity.

- CargoCrate_Benchmark.blend contains a separate benchmark scene; the original default scene is retained.
- build_crate.py reproduces the model and image textures in a new scene.
- Painted shell, inset panels, reinforcement, seals, handles, latches, barcode and cyan status strips.
- 6,864 triangles before any mobile optimisation; nine material slots. A visual benchmark, not the final shipping budget.
- Colour and roughness textures are real PNG assets, packed into the blend as well.
- crate-benchmark.png is a Blender Cycles studio render, not Unity gameplay.
- Native FBX import succeeded at Assets/OrbitalCargo/Art/BlenderBenchmark/CargoCrate_Benchmark.fbx. It is not placed in Game.unity and does not replace the gameplay crate.
- URP material transfer, in-game lighting review, scanner compatibility and mobile profiling remain to be done before integration. Blender studio appearance does not establish Unity appearance.

No package or project setting was changed by this benchmark. No gameplay tests were needed for this isolated asset import.
