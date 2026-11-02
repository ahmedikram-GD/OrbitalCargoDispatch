# Reference-led environment kit and lighting

## Result
- Broad, subtly worn metal deck replaces the previous bevelled grid visually across the room. Existing floor collider retained; new deck top is raised only 0.012 m.
- Authored navy wall modules replace the original visible shell in the Bay A rear/left corner. Old shell meshes are copied and trimmed only where replaced; original shared mesh assets remain intact.
- Ceiling fixtures and smaller wall practicals have recessed warm diffusers, protective guards, reflectors and end caps.
- Yellow route stripe, Bay A branch/approach markings and dispatch arrow follow the reference's wayfinding direction.
- Three reusable prefabs, FBX sources, shared PBR atlas, source blend, reproducible authoring script and approved reference are saved.

## Lighting
- Scene-specific CornerLighting asset enables baked GI for authored imported meshes only. Legacy shell/stations retain realtime/probe lighting, avoiding baking their unprepared meshes.
- Two rectangular baked area lights, emissive fixtures, 27 local light probes, and two unshadowed realtime fill lights. Existing scene key retained.
- Two lightmaps generated (1024 maximum atlas size); saved LightingData and textures are under Scenes/Game/.
- Dock reflection cubemap updated to include the new room surfaces.
- FXAA saved as an explicit camera prefab-instance override. Pipeline asset, quality settings and colour space unchanged.

## Rebuild
1. Tools > OrbitalCargo > Art > Import Environment Corner.
2. Tools > OrbitalCargo > Art > Bake Environment Corner. Wait until Lightmapping.isRunning is false.
3. Tools > OrbitalCargo > Art > Bake Dock Reflection, then save the scene.
4. The existing dock art generator retains the kit when enabled by DockArtDirection.UseEnvironmentKit. A geometry rebuild changes renderer identities and requires rebaking. Always rebake after regenerating the room; full Setup Game Scene remains the correct way to rebuild gameplay references.

Tune CornerBakedLightIntensity and CornerRealtimeFill in DockArtDirection.asset. Disabling UseEnvironmentKit and rebuilding restores the prior shell appearance, but does not restore previous scene lighting settings; revert the art commit for a full return to the previous state.

## Verification
- 93/93 existing editor tests passed.
- Play API smoke: detailed crate carried at existing 0.55 scale; suspicious crate scanned and X-ray activated; six crates picked/placed; full Bay A launched, paid 60 credits, returned and unlocked. Original measured balance 475 restored.
- Two lightmaps, 27 probes and reflection texture present in Play mode. New environment decoration has zero colliders.
- Approximately 33,447 imported/decorative triangles across 65 renderers. Static batching flags set on modules, atlas shared. These counts are not a phone performance benchmark.
- Actual Game View and positioned Unity screenshots reviewed. No Blender render or generated replacement image is presented as Unity output.

## Remaining differences from the reference
The reference remains the art target; this is not a one-to-one reproduction of the complete image. The existing overhead shuttle arrangement, Bay B/C, scanner exterior, chute, conveyor, ceiling structure, planet window and background dressing still differ. The mobile LDR pipeline limits the lighting presentation compared with the concept. HDR/pipeline changes have not been made. Android build, touch walkthrough and device performance remain unverified.
