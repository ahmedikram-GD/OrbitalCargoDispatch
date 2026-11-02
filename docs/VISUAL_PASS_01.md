# First dock visual pass

Implemented in the existing Unity project, 2 November 2026.

## Changes
- Modular navy walls, pillars, a quieter floor, ceiling fixtures and enclosed upper launch shafts.
- Bay A entrance frame, signage, cyan accents, rear panels and short safety rails.
- Conveyor slats, edge framing, supports and intake portal.
- Bevelled crate casing, reinforced corners, lid braces and barcode plates; cyan standard-crate stripe.
- Existing gameplay colliders, cargo slots, pooling, interaction scripts and UI retained.

This is the first procedural art pass. Bays B/C, scanner body, chute, planet window, background props and UI still need dedicated passes. No new package was installed or project-wide render setting deliberately changed.

## Use
Unity menu: Tools > OrbitalCargo > Art > Apply First Dock Pass.
Palette/light tuning: Assets/OrbitalCargo/ScriptableObjects/Room/DockArtDirection.asset.
Generated meshes/materials: Assets/OrbitalCargo/Art/Dock/.
Existing room/crate factories preserve the art pass when rebuilding. Use the full existing Setup Game Scene tool when regenerating the complete scene, so systems reference the rebuilt stations.

To restore the exact previous state, revert the art-pass commit. Clearing the layout Art Direction field and rebuilding removes the room overlay, but does not by itself restore saved lighting, crate prefab or standard-crate colour.

## Verification
- 90 existing editor tests passed; 0 failed/skipped.
- Final scripts compiled; final console check returned no errors or warnings.
- Runtime API smoke check: six pooled crates picked up and placed; full Bay A launched, paid 60 credits, emptied, returned its shuttle and unlocked.
- Scanner check: suspicious crate scanned, X-ray enabled, alien egg revealed, crate picked up again and booth freed.
- Carried crate inspected in actual Game View with HUD.
- New room decoration: 19 renderers, approximately 13,816 triangles, no colliders. This is a geometry count, not a performance benchmark.
- Credits restored to original balance of 295. Unity left outside Play mode, scene saved.
- Existing ProjectSettings.asset change preserved and excluded from the art-pass commit.

Android build, physical touch controls, phone performance and a complete manual shift remain unverified. Runtime checks used gameplay APIs rather than simulated player inputs.

Project screenshots: Captures/ArtPass/.
Pre-pass scene/prefab copies: Captures/ArtPass/Baseline/, outside Assets and not imported.
