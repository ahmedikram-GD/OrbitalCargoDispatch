# Orbital Cargo Dispatch: Graveyard Shift — Game Design Document

> This is the full game vision. The scope for the Monday pitch prototype is defined in `KICKOFF.md`. Build only what KICKOFF.md asks for; use this file to understand intent, tone and rules.

## High concept

You run the night shift on an orbital cargo station: sort colour-coded crates into the right shuttles, catch the smuggled ones, and grow a tiny dock into a planet-hopping logistics empire.

| | |
| --- | --- |
| Genre | First-person casual simulator (sort and deliver) with an idle automation layer |
| Platform | Mobile, landscape (Android and iOS) |
| Audience | Casual simulator players; fans of Supermarket Simulator-style mobile sims |
| Session | 3 to 5 minute shifts; 30 to 60 minutes of content on day one |
| Comparables | Supermarket Simulator (loop and growth), Papers Please and Contraband Police (inspection), Quarantine Zone (spot the threat) |
| Business model | Free to play: rewarded ads first, light interstitials, IAP |

**The hook:** every crate tells you where it goes at a glance, so sorting is effortless and satisfying. The twist is that some crates hide something they shouldn't. Spotting them, scanning them in the booth and watching the X-ray reveal is the moment players remember and share.

**Design rule above all others:** one glance, one decision. The player never reads, compares or calculates. This is a casual mobile game.

## Setting and fantasy

The player is the new night-shift dispatcher at a transfer waystation orbiting a mining colony. Giant interstellar haulers can't enter the atmosphere, so they dock outside and push their cargo through to you. You load it onto small shuttles that drop it to the surface.

**Tone:** cozy industrial sci-fi with a light comic edge. Humming machinery, a planet glowing through the bay window, radio chatter from characters with personality. Mistakes are funny, never grim ("Alien egg hatches on colony! Dispatcher fined.").

**Recurring characters (radio only, no animation):**

- **The Boss** — grumpy station manager, hands out directives, complains about budgets.
- **Rook** — friendly veteran hauler pilot, teaches the ropes (tutorial voice).
- **Vex** — charming smuggler who tries to slip things past you (story only).

## Core loop

One shift is one loop: crates arrive, you carry each to the bay its colour points to, full shuttles launch, and cash comes out. Between shifts you spend that cash on upgrades.

1. **Intake.** A hauler docks outside and crates roll in on the conveyor.
2. **Sort.** Pick up a crate, walk to the matching bay, it snaps into a free slot.
3. **Inspect.** Crates that look wrong go to the scanner booth first.
4. **Launch.** When a shuttle's slots are full, hit its launch button. It blasts off and pays out.
5. **Upgrade.** At shift end, spend credits on speed, carry capacity, bays, drones and crate licences.

### Room layout (single room)

- Conveyor enters through an airlock on one side wall and runs along the floor.
- Three shuttle bays line the far wall: Bay A (Heavy), Bay B (Cryo), Bay C (Hazard). Each bay has 6 slots (2 rows of 3) and a green launch button.
- Scanner booth sits next to the end of the conveyor.
- Chute (round hatch in the floor) sits next to the booth.
- Upgrade terminal is by the door on the opposite side wall.
- Player starts in the middle of the room.

## Crates and bays

Every crate shows its destination by colour and shape. Each special type adds exactly one twist the player feels rather than calculates.

| Crate | Look | Goes to | Twist | Unlocks |
| --- | --- | --- | --- | --- |
| Standard | Grey-blue steel, barcode | Bay A (Heavy) | None. The baseline. | Start |
| Cryo | White casing, cyan/blue lights, frost vapour | Bay B (Cryo) | Ring timer on the crate. Deliver before it turns red. | Shift 2 |
| Junk | Rusty metal, red tape, beeping | Chute | Swipe to throw it in. | Shift 3 |
| Volatile | Yellow, black chevrons, amber strobe | Bay C (Hazard) | Slows you down and wobbles while carried. | Shift 5 |
| Suspicious | Any type, but shaking, sparking or dripping green goo | Scanner booth | Hides something. See Discovery. | Shift 2 (scripted) |

**Failure is soft.** An expired cryo crate or a wrong-bay delivery just pays less. Nothing ends a shift early.

## Discovery: the scanner booth

Some crates hide something. The player spots the tell, drops the crate in the scanner booth, watches an X-ray reveal, and sends it to the chute or its bay. This is the signature moment.

1. **Spot it.** A suspicious crate shakes, sparks, drips green goo or makes an odd sound. Normal crates never do.
2. **Scan it.** Carry it into the booth. A 2–3 second X-ray animation turns the casing transparent and reveals what's inside.
3. **Act on it.**
   - **Bad (red):** alien egg, smuggled weapons, ticking device → chute → bonus credits + item logged in the collection book.
   - **Clean (green):** false alarm → crate goes to its normal bay.

**Consequences stay light and funny.** Ship a bad crate unscanned → news alert after launch + small fine. Chute a clean crate → lose its value.

**Collection book.** Every bad item found is logged (the crate itself is destroyed). Items come in themed sets (Alien Wildlife, Black Market Tech, Stolen Art, Weird Food) with a reward for completing a set.

**Deepens over time:** tells get slightly subtler at higher levels; new stations add new hidden items; booth upgrades add faster scans, double scans, an auto-flag drone.

## Controls and HUD (mobile landscape)

- **Move:** floating thumbstick, left half of the screen.
- **Look:** drag anywhere on the right half.
- **Context button (bottom right):** Pick up / Place / Scan / Launch depending on the target. Placement snaps to the nearest free slot with a ghost preview. No physics fiddling.
- **Throw:** quick upward swipe while holding junk → assisted arc into the chute.
- **HUD top bar:** credits, shift timer, station level, pause.
- **In world:** fill counter above each bay ("4 / 6"); cryo timer as a ring on the crate itself.
- **Optional rewarded button** (e.g. "Free drone") near the context button.

## The first 10 minutes (FTUE)

| Time | What happens | Teaches |
| --- | --- | --- |
| 0:00–1:00 | Rook greets you on the radio. One grey crate, an arrow to Bay A, then a glowing launch button. Shuttle blasts off, cash rains out. | Move, pick up, place, launch |
| 1:00–3:00 | More grey crates, slightly faster. First upgrade prompt: walking speed. | Earn and upgrade rhythm |
| 3:00–5:00 | Bay B unlocks. Cryo crates arrive with ring timers. | Prioritising under light pressure |
| 5:00–7:00 | A crate starts shaking. Rook: "That one looks off… scan it." X-ray reveals an alien egg → collection book. | The discovery hook |
| 7:00–10:00 | Shift ends. Summary screen, first 2x rewarded-ad offer, grav-sled teased behind a lock. Chute unlocks next shift. | Session end, monetisation, reason to return |

Volatile crates stay locked until around shift 5.

## Progression

- **Station level (XP):** every launch gives XP; levels unlock crate licences, bays and features.
- **Station expansion:** cramped single-bay dock → big station (more bays, wider conveyor, second airlock, racks, crew room).
- **Equipment:** grav-sled (carry 3), mag-boots (faster), cargo exosuit (carry 5, no volatile slowdown), personal helper drone.
- **Automation (idle layer):** drones/crew that auto-unload, auto-deliver standard crates, auto-launch full shuttles; enables offline earnings.
- **New stations (chapters):** each reuses all systems, adds a new look + one new crate behaviour.

| Station | Theme | New crate |
| --- | --- | --- |
| 1. Ironfall | Industrial mining colony | The base four types |
| 2. Glacia | Frozen moon | Ice crates that slide when set down |
| 3. Verdant | Jungle world | Live-animal crates that hop around |
| 4. Nimbus | Gas giant | Anti-grav crates that float off if left alone |
| 5. The Bazaar | Alien trade hub | VIP alien cargo with big payouts |

## Retention and live ops

- **Daily:** 7-day login cycle, three simple missions, one VIP shipment, offline earnings.
- **Weekly events:** Meteor storm (bonus junk), Smuggler crackdown (double suspicious crates), Holiday cargo (gift crates with cosmetics).
- **Story:** short radio exchanges between shifts; a small arc per station.

## Economy and monetisation

- **Credits (soft):** earned from launches; spent on upgrades, expansion, staff.
- **Quantum Cores (hard):** missions, events, collection sets, shop; spent on premium staff, cosmetics, skips, rare equipment.

**Rewarded ads:** 2x shift payout (summary), 2x offline earnings (return), free helper drone 3 min (in shift), instant restock (empty conveyor), Lucky Scan (booth), free chest (daily).

**Interstitials:** only between shifts, never while carrying/walking, max one per two shifts, none in the first three sessions.

**IAP:** remove forced ads, starter pack, VIP subscription, season pass, cosmetics, core packs. Prices to be set by the market team.

## Design pillars

1. **One glance, one decision.** No reading, no maths.
2. **Always something growing.** Station, collection, fleet. Every session ends with visible progress.
3. **The reveal is the star.** The scanner moment is the hook in play, marketing and sharing.
4. **Cheap content, long life.** New stations reuse systems; new art and one twist make a chapter.
