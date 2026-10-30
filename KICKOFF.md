# KICKOFF — Orbital Cargo Dispatch pitch prototype

You are helping me (Awais, Unity developer) build a playable Unity prototype of **Orbital Cargo Dispatch: Graveyard Shift** for a pitch on **Monday 2 November 2026, evening**. Read `docs/GDD.md` first for the full design. This file defines what to build for the pitch and how to work.

Work in phases. **Stop at the end of Phase 0 and Phase 1 and wait for my approval** before continuing.

---

## Context

- **Engine:** Unity 6, **Universal Render Pipeline (URP)** — create the project from the Universal 3D (URP) template. Target: Android, landscape. Use the mobile-friendly URP renderer/quality settings.
- **New project:** `D:\Projects\OrbitalCargoDispatch` (create it if I haven't; ask me if the path should differ).
- **Source projects (READ-ONLY — never modify, move or delete anything in them):**
  - `D:\Projects\Silly-Justice` — has a working **first-person player controller** and other 3D simulation systems.
  - `D:\Projects\NumberixPuzzle` — has a **game kit with very good architecture**: reusable systems such as vibration/haptics, object pooling, audio, and clean coding patterns.
- The goal is to **reuse** what exists: build on the NumberixPuzzle kit's architecture and systems, port the first-person controller from Silly-Justice into the kit's conventions, then build the core loop on top. Do not rewrite systems that already exist in the kit.
- **Art for now:** crates and props are **greybox primitives with coloured materials**. No Blender, no asset packs yet. Art pass comes last (see "Art pass" below).

---

## Phase 0 — Analysis (read-only, about 1 hour)

Read both source projects and write `docs/ANALYSIS.md` covering:

1. **Kit inventory (NumberixPuzzle):** every reusable system (pooling, audio, haptics/vibration, events/messaging, UI/screens, save/data, scene flow, tweening, singletons/services, ScriptableObject config, anything else). For each: what it does, its entry point, and a short usage example.
2. **Architecture conventions:** folder structure, namespaces, naming, how systems talk to each other (events, service locator, DI, singletons), how config/data is stored, assembly definitions.
3. **First-person controller (Silly-Justice):** which scripts, their dependencies, input system used (old Input Manager vs new Input System), whether it already supports **mobile touch** (joystick + look), and what else in Silly-Justice is useful for a 3D sim (interaction raycasts, hold points, UI prompts, etc.).
4. **Compatibility risks:** Unity versions, packages, render pipeline, input system and plugin differences between the two projects, and anything that will break when combined. The new project is **URP**: if either source project uses the Built-in pipeline, list the materials, shaders and post-processing that must be converted (Render Pipeline Converter) or replaced when copied over.
5. **Recommendation:** exactly what to copy into the new project, in what order, and what to adapt.

**Stop here and wait for my approval.**

---

## Phase 1 — Plan

After approval, write:

- `docs/PLAN.md` — map every prototype feature (scope below) to either an **existing kit system** or a **new script**, with a short note on each new script's responsibility. Include the build slices with acceptance criteria.
- `CLAUDE.md` in the new project root — the coding conventions from the kit (so every future session follows them), the read-only rule for source projects, and the project's key paths.

**Stop here and wait for my approval.**

---

## Phase 2 — Build in playable slices

Commit to git after each slice. Each slice must run in the editor before moving on. After each slice, tell me what to test.

1. **Setup:** new project structure, kit imported, FP controller ported and working with mobile touch (left joystick move, right-side drag look). Greybox room.
2. **Carry and place:** raycast from screen centre, context button shows Pick up / Place, crate parents to a hold point, places by snapping to the nearest free slot in the targeted bay with a ghost preview.
3. **Bays and launch:** three bays with 6 slots each, fill counters, launch button enabled when full, shuttle launch (tween up + particles + camera shake + haptic + sound), credits payout with coin pop-ups.
4. **Conveyor and spawner:** kinematic conveyor (no physics) that moves and queues crates; scripted spawn schedule; all crate types with their twists (cryo ring timer, volatile slowdown + wobble, junk swipe-throw).
5. **Scanner booth and chute:** suspicious crate tells (shake, sparks, green drip); booth reveal (casing switches to a URP Lit material with Surface Type set to Transparent (or a simple URP Shader Graph with fresnel glow), hidden item child enabled, glowing scan line sweeps down); red/green result; chute destroys and rewards; "Added to collection" pop-up; news-alert fine if a bad crate is shipped unscanned.
6. **Shift flow and UI:** HUD (credits, shift timer, level, pause), shift summary screen with a **mock** "2x payout" rewarded-ad button (fake 2-second ad, then doubles), one working upgrade (**grav-sled: carry 3 crates**), start next shift.
7. **Juice and polish:** sounds, haptics, tweens on every important action, radio text lines from Rook for the tutorial beats.
8. **Art pass** (only if time remains, see below), then Android build.

---

## Prototype scope (the pitch shift)

One scripted **pitch shift of about 5 minutes** that shows every mechanic in order, so the audience sees everything in one play-through:

| Time | Spawns / event |
| --- | --- |
| 0:00 | Rook radio intro. One standard crate, arrow to Bay A. First launch inside 60 seconds. |
| ~1:00 | More standard crates. |
| ~1:45 | Cryo crates begin (Bay B), ring timers. |
| ~2:45 | First suspicious crate. Rook: "That one looks off… scan it." Reveal: alien egg → chute. |
| ~3:30 | Junk crates (swipe into chute). |
| ~4:00 | Volatile crates (Bay C, slowdown + wobble). |
| ~5:00 | Shift ends → summary → mock 2x ad → buy grav-sled → optional second shift with faster spawns. |

**Tuning (all in ScriptableObjects so I can tweak without code):** shift length, spawn schedule, conveyor speed, cryo timer (start at 45 s), scan duration (start at 2.5 s), payouts per crate type, chute bonus, fine amount, walk speed, volatile slowdown multiplier.

**Out of scope for the pitch:** real ads/IAP SDKs, save system beyond what the kit gives for free, multiple stations, automation/drones, daily missions, events, full collection book screen (a pop-up is enough), story beyond tutorial radio lines.

---

## Technical rules

- **Use the kit's systems** for pooling (crates, coin pop-ups, particles), audio, haptics, events and UI. Follow its conventions exactly.
- **No physics carrying.** Raycast + parent to hold point + snap to slot.
- **No physics conveyor.** Move crates along a path in code and queue them.
- **Data-driven crates:** a `CrateType` ScriptableObject per type (colour/material, destination bay, payout, twist settings).
- **Mobile first:** touch controls must work on a phone; also keep mouse/keyboard working in the editor for fast testing.
- **Ask me before:** adding any package or plugin, changing project-wide settings beyond what's needed, or doing anything that would touch the source projects.
- **Unity MCP** is currently configured for another project (`D:\Awais\Backup\Core Game Template`). Remind me to point it at the new project when editor access is needed.

---

## Art pass (last, only if time remains)

Free **CC0** packs from Kenney (kenney.nl), no licence issues:

- **Space Station Kit** — room, walls, floors, props.
- **Modular Space Kit** — extra modular wall/floor tiles.
- **Space Kit** — shuttles to launch.
- **Sci-Fi Sounds** — launches, beeps, UI.

Crates can stay as coloured cubes with simple decals; the colour coding is the design. Prefer an editor script that builds/re-skins the room from a layout definition over hand-placing pieces.

---

## Start

Begin with **Phase 0**. Read `docs/GDD.md`, then analyse both source projects and write `docs/ANALYSIS.md`. Do not create or change any project files yet.
