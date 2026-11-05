# KICKOFF V2 — Orbital Cargo Dispatch web prototype, stations update

You are helping me (Awais) turn the existing **web prototype** into the **GDD v2** design. Read `docs/GDD.md` first: it is the v2 design, with diagrams in `docs/gdd-images/` (look at them). The v1 design is archived in `docs/GDD_v1.md` for reference only.

Work in phases. **Stop at the end of Phase 0 and wait for my approval** before building.

---

## Rule zero: v1 stays untouched

The current prototype is published as the artifact **"Orbital Cargo Dispatch"** (https://claude.ai/artifact/YRqmkFmkiPNvohaEkWqrPM), built from `WebPrototype/index.html` and `WebPrototype/web-assets/`. It must not change.

1. **Never edit anything in `WebPrototype/`.** Copy it once to `WebPrototype_v2/` (including `web-assets/`) and do all work there.
2. **Never republish `WebPrototype/index.html`, and never pass the v1 artifact URL** to any publish call.
3. **Publish v2 as a new artifact**: publish `WebPrototype_v2/index.html` (with its `web-assets/` files) **without a `url`**, page `<title>` "Orbital Cargo Dispatch v2". After the first publish, write the new artifact URL into the "v2 artifact" line at the bottom of this file and commit it. Every later publish republishes `WebPrototype_v2/index.html` to that v2 URL only.
4. **Git:** create a branch `v2-stations` from the current HEAD before touching anything. The working tree has unrelated uncommitted changes (Unity art files, ProjectSettings): leave them alone. Never stage, commit, revert or stash them. Stage only `WebPrototype_v2/`, `docs/` files you create, and this file.
5. **The Unity project is out of scope** for this pass. Don't touch `Assets/`, `ProjectSettings/`, `Packages/`, `docs/PLAN.md` or `KICKOFF.md`; they belong to the Unity build. The Unity rules in `CLAUDE.md` (C# conventions, ScriptableObjects) don't apply to the web prototype, but its spirit does: data-driven tuning, no magic numbers scattered through code, juice through one presenter, commit after each slice.

---

## What the web prototype already is

A single-page Three.js r128 game (`index.html`, ~260 KB) with authored GLB assets under `web-assets/` (`orbital-assets.js`, `stations/station-assets.js`, GLTFLoader r128). It already has: first-person movement with touch joystick and look, pick up and carry, grav-sled (carry 3), conveyor with kinematic cargo, three bays with slots and a launch flow, scanner booth with X-ray reveal, chute and junk throw, cryo and volatile crates, collection book, shop/upgrades, drone, mock rewarded ad, scripted intro with radio lines, shift summary, haptics and juice.

**Reuse all of it.** v2 changes how the player works at each station, not the room, the assets or the controller.

---

## Phase 0 — Analysis (read-only)

Read `docs/GDD.md` and the images, then read `WebPrototype/index.html` (and the adapters in `web-assets/`). Write `docs/PLAN_V2.md` covering:

1. **Map of the current code:** where each system lives (functions, constants such as `TYPES`, `BAY_DEFS`, `BOOTH`, `CHUTE`, `SHOP`, `RADIO`, the intro script), and how state, input and the frame loop are organised.
2. **Change table:** for every v2 feature below, whether it **reuses**, **changes** or **replaces** existing code, and which functions are affected.
3. **Station view design:** how a camera "station view" mode fits the existing input and frame loop (enter, exit, auto-return, input routing, the world keeps running).
4. **Slices** in build order, each with acceptance criteria I can test in a browser and on a phone.
5. **Risks:** anything in the current code that makes a v2 feature hard, and what you propose.

**Stop here and wait for my approval.**

---

## Phase 1 — Build in playable slices

After each slice: it runs with no console errors, you commit on `v2-stations`, you tell me what to test, and you republish to the **v2 artifact** (rule zero). Suggested order (adjust in PLAN_V2 if the code suggests better):

1. **Setup.** Branch, copy `WebPrototype/` → `WebPrototype_v2/`, set the title to "Orbital Cargo Dispatch v2", first publish as a new artifact, record the URL below.
2. **Station view system.** Context button enters a station; camera eases (~0.3 s) to a fixed framing; inside, input is direct touch/mouse (tap, drag); the camera returns to first person automatically when the job is done; joystick or swipe-down leaves early. The game never pauses.
3. **Packing grid hold** (replaces bay slots). Grid starting at 2 × 3; crate shapes 1 cell (standard, cryo, volatile), 2 cells (long), L of 3 (bulky); carried crates in a bottom tray; drag onto grid with a ghost that snaps (green fits, red doesn't); tap to rotate; cold cells for cryo and a blast cell for volatile; order card per bay (crate icons with counts); "PERFECT LOAD" banner and bonus when the order is complete with no empty cells. See `gdd-images/04-packing-mockup.png` and `05-packing-storyboard.png`.
4. **Launch lever and departure ring.** Lever in the same view as the hold; one needle sweep with a green zone for a Perfect launch; early launch pays for what's loaded; ring on the bay door (start 60–90 s); when it runs out the shuttle leaves anyway. Nothing fails.
5. **Check-in window.** Pilot queue at the window with a patience ring; station view of the counter; manifest card of icons; green stamp releases the crates onto the conveyor, red stamp releases them with red tags; fast service tips. Three pilots for the pitch: Rook, one grumpy regular, Vex. See `gdd-images/03-checkin-storyboard.png`.
6. **Scanner sweep.** Each crate carries a content label icon; in the booth the X-ray shows only under the finger/cursor; the player taps Clear (match) or Seize (mismatch → chute, bonus, collection book). Red-tagged and shaking crates are the ones worth scanning.
7. **Dispatch rating.** HUD meter that rises with good work and dips with mistakes; 1 to 3 stars on the shift summary.
8. **Pitch shift.** Rewrite the scripted shift to the v2 first-10-minutes beats compressed to about 5 minutes: Rook check-in and first pack/launch → small orders → cryo and cold cells with departure rings → Vex and the alien egg (label says fruit) → summary with stars and the mock 2x ad.

**Tuning:** every number in one config block at the top of the script (grid sizes, shapes, ring lengths, cryo timer 45 s, patience, payouts, bonuses, rating weights, camera ease time), so I can tweak without hunting through code.

**Out of scope:** interruptions, chapters, live ops, real ads/IAP, second window/booth upgrades, Unity changes.

---

## Ask me before

- Adding any external library or CDN script beyond what `WebPrototype` already loads.
- Changing the room layout or replacing authored assets.
- Anything that would touch `WebPrototype/`, the v1 artifact, or the Unity project.

---

**v2 artifact:** _(not published yet; write the URL here after the first publish)_
