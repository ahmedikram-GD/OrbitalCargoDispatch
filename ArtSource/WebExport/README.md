# Web export

export_web_assets.py runs inside Blender through the existing Unity MCP Blender bridge. It imports the current authored FBX exports into a temporary scene, constructs glTF-compatible materials from the baked source atlas maps, exports four GLBs and restores the previously active scene. It does not save or modify the source blend files or Unity scene.

The export is restricted to its active temporary scene so selected objects in other Blender scenes cannot leak into the GLBs. The output is WebAssetHandoff/models with a regenerated manifest.json. Run WebAssetHandoff/verify_assets.py afterward to validate and populate hashes, then open the included browser preview.

Material conversion uses G=roughness and B=metallic, with AO in R for the environment kit. It uses Blender 5.2 Separate Color nodes. Web textures are reduced to at most 1024 pixels and embedded in each GLB. No required glTF extensions or geometry decoders are emitted.

The Three.js r128 preview was verified in the browser, including all four individual asset views and the assembled corner. The existing published Claude prototype still needs source-level integration using WebAssetHandoff/CLAUDE_HANDOFF.md.
