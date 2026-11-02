# Cargo tug and Bay A mesh pass

Replaces the first Bay A overlay with a custom mesh cargo tug and angled dock surround.

## Result
- Tapered, chamfered hull sections, forward cockpit, split windshield, circular engine intakes, recessed exhausts, vent slots and navigation lamps.
- Open lifting cradle fits all six existing cargo slots. The visual attaches to the original Shuttle transform and follows its existing launch/return animation.
- Angled dock shoulders, recessed rear panels, smaller signage, safety rails, threshold markings and sloped launch console. The original button collider and animated lamp renderer remain wired.
- Scene key light now casts contact shadows. The room shell does not cast shadows from that broad interior fill; practical lights remain unshadowed. This is an inexpensive art-directed arrangement, not baked global illumination.
- Palette stays navy, ivory, amber and cyan. Bays B/C and other stations retain their previous art.

## Assets and regeneration
- `Assets/OrbitalCargo/Art/Dock/CargoTug.prefab`: reusable visual asset, no gameplay scripts or colliders.
- `Assets/OrbitalCargo/Editor/Tools/HeroDockArt.cs`: mesh authoring and Bay A assembly.
- Existing `Tools > OrbitalCargo > Art > Apply First Dock Pass` rebuilds this pass too.
- `DockArtDirection.asset`: ContactShadows / ShadowStrength control the scene key.
- Combined mesh regeneration now invalidates the GPU vertex buffer explicitly; otherwise Unity could display an older silhouette after updating an existing mesh asset.
- No packages or render-pipeline settings added/changed. The user's pre-existing ProjectSettings.asset edit is excluded.

## Verification
- 90/90 existing EditMode tests passed.
- Runtime API smoke: six pooled standard crates picked up and placed; full bay launched, paid 60 credits, returned to its home position and unlocked.
- Captured the fully loaded bay from player height in Play mode; cargo has clearance beneath the hull and between its rails.
- Original measured credit balance (355) restored after the launch check.
- Tug: 2,068 triangles, seven combined mesh materials plus a text renderer, no colliders. Rebuilt dock decoration also adds no colliders.
- Final console check: no errors or warnings. Unity left outside Play mode.

## Limits
This is a custom low-poly mesh improvement, not a match for the concept render's finished surfacing and baked lighting. The existing gameplay uses an overhead cargo cradle; it has not been redesigned into the ground-level parked ships shown in the concepts. Phone performance, manual touch interaction and a full Android build remain unverified. Shadows may differ between quality profiles.

Actual Unity captures: `Captures/ArtPass/hero-loaded.png` and the final hero preview. No generated or retouched image substitutes for the game captures.
