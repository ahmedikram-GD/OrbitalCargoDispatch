# Blender crate transferred to URP

## Shipped in the playable prefab
- Standard cargo now uses the authored Blender mesh, normalised into the existing 0.8 m casing bounds. Other types retain their existing colour coding.
- Eight imported material slots are replaced with one URP/Lit atlas material. Maps: colour, packed metallic R / smoothness A, and emission. Source geometry remains 6,864 triangles; no new crate colliders.
- Navy albedo is calibrated brighter for the current Gamma-space dock. It is still a dark painted-metal finish.
- An editor-baked 128 px HDR dock cubemap supplies local reflections. The probe uses an explicit Custom texture binding, verified across entering Play mode; no runtime cubemap updates. This is not baked diffuse/global illumination.
- The detailed exterior hides during scanning; the original transparent casing and hidden-item anchor provide the reveal. Pool reuse restores the correct type and exterior.
- Existing hold point, carry scale, pooling, slot references and launch system retained.

## Rebuild
1. Source: ArtSource/CargoCrate/CargoCrate_Benchmark.blend. Original authored objects and atlas-export object are retained.
2. In a fresh Blender scene, build_crate.py then bake_crate.py then calibrate_crate.py reproduce the source/export maps.
3. Unity: Tools > OrbitalCargo > Art > Apply Blender Crate copies/imports the assets and wires the gameplay prefab.
4. Tools > OrbitalCargo > Art > Bake Dock Reflection updates the environment cubemap after room art changes.
5. Existing dock/crate factories retain the authored variant when its assets exist. The dock factory restores the saved reflection binding.

## Verified
- 93 editor tests passed: original 90 plus three focused tests for detailed/X-ray visibility and pooled type switching.
- Play-mode API checks: picked up detailed standard crate at existing 0.55 carry scale; six standard crates placed, bay full, launched, returned, unlocked; 60-credit payout. Original measured balance 475 restored.
- Actual scanner: suspicious standard crate completed scan, detailed exterior hidden, transparent casing visible, one hidden egg model present.
- Reflection cubemap persisted into Play mode after explicit custom binding.
- Actual Game View carry screenshot and positioned Unity loaded-bay capture reviewed. These are game renders, not Blender previews.
- Final editor console checked; Unity left outside Play mode.

## Remaining
Room-wide surface textures, baked diffuse lighting, better fixture illumination, anti-aliasing and remaining station art still need work to reach the reference. This pass establishes the crate material/mesh pipeline. It does not establish phone performance; Android build, touch testing and profiling remain outstanding. No package or project-wide renderer setting was changed.
