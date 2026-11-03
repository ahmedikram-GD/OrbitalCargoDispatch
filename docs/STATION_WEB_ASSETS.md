# Web station assets — 3 November 2026

User requested low-poly Blender models of the generated conveyor, scanner, and disposal chute references for incorporation into Claude's web prototype.

Source: ArtSource/StationKit/Stations_Authored.blend, packed textures and regeneration scripts. Web exports and self-contained r128 viewer: StationWebHandoff/. Each GLB embeds PBR textures and named integration anchors. The Three.js adapter deduplicates the shared 1024 atlas; the belt uses a 256 repeating texture pair. Fronts +Z, +Y up, metres.

Conveyor 3,426 triangles / 3 mesh draws; scanner 2,386 / 3; disposal 2,524 / 1. Total 8,336 triangles and 7 mesh draws before lighting passes. Model download total 1,061,864 bytes. No real-time lights or shader transmission embedded. Scanner glass is alpha blend, single-sided, depthWrite disabled by the adapter. No LOD/mesh compression required for this small set.

Actual browser renders saved in StationWebHandoff/previews. All three individually viewed, motion and optional shadows exercised. No browser errors/warnings. Python verifier passes GLB counts, index bounds, finite geometry, UV/normals, PNG resolution, hashes, named anchors, no required extensions. JavaScript syntax checks pass. Physical-phone performance and integration with the game remain untested. No Unity scene, packages, project settings, or existing WebPrototype files changed. Existing modified Cargo_CargoLabel, Cargo_CargoTrim and ProjectSettings files left untouched.

These are simplified reference-led assets, not exact replicas of the generated renders. CLAUDE_HANDOFF.md describes placement, native dimensions, animation and disposal floor-opening requirements. Latest user task supersedes the previous proposed ramp/shuttle priority; those remain future art work.
