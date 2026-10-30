# GameKit — Module Index

Every module under `Assets/GameKit/` and where its documentation lives. Docs sit **next to the code they describe**, so they move and die with it.

## Assembly & namespace contract

- **Every kit script lives in `GameKit.<Module>[.<Sub>]`** — no global-namespace scripts, no legacy `Blues.*`/`ProjectCore.*`/`Game.*` names. Vendored libraries (`THEBADDEST.*`, `DG.*`) and the GDMonetization package (`Monetization.Runtime.*`) keep their own namespaces.
- **`GameKit.Runtime` is a compiled assembly** ([GameKit.Runtime.asmdef](GameKit.Runtime.asmdef)). `Assets/GDMonetization`, `SDKsEssentials`, `Metica`, `GameContent`, `Scripts`, and `Adjust` are asmref-merged into it (they form one dependency cycle with the kit). Editor-only folders compile into `GameKit.Editor`.
- **Game code (`Assets/YarnGame`, `Assets/Games`) stays in `Assembly-CSharp`**, which auto-references `GameKit.Runtime`. The reverse is a compile error — **the kit can never depend on a game**. A game plugs in through: `GameState` subclass + `Transition` assets, prefabs in a `Resources/` folder loaded by string id, `ILevelNavigator` / `IBoosterService` implementations registered in its own LifetimeScope, and its own `BoosterDatabase`/config assets wired into the kit's state assets (every such reference is missing-safe; `GameFlow.fallbackGameTransition` boots the Template game when the wired game is gone).
- Verified 2026-07-28: deleting `Assets/YarnGame` leaves a project that compiles cleanly and boots Splash → Home → Template gameplay with zero exceptions.

## Modules

| Module | Doc | What it is |
|---|---|---|
| **LiveOps** | [LiveOps/ARCHITECTURE.md](LiveOps/ARCHITECTURE.md) | The live-operations framework: one ScriptableObject "brain" per event, discovered by interface from one registry. Also has 9 deep-dive references under `.claude/skills/liveops/`. |
| **AppFlow** | [AppFlow/APP_FLOW.md](AppFlow/APP_FLOW.md) | Every screen and the flow between them — Splash → Home → Game → Complete/Fail, plus Shop, RemoveAds, BoosterBuy, DailyRewards, and the level-fail/leave panel arbitration. |
| **Architecture** | [Architecture/ARCHITECTURE.md](Architecture/ARCHITECTURE.md) | The foundation everything sits on: SO variables + persistence (`DB*`), the `GameEvent` system, ads service, Firebase remote config, coroutines, pooling, sound, time. |
| **StateMachine** | [StateMachine/STATE_MACHINE.md](StateMachine/STATE_MACHINE.md) | The FSM that drives app flow, the `UiViewState`/`UiBase` view layer — and **19 numbered rules** for writing states correctly. |
| **Boosters** | [Boosters/BOOSTERS.md](Boosters/BOOSTERS.md) · [Boosters/BOOSTER_CREATOR_TOOL.md](Boosters/BOOSTER_CREATOR_TOOL.md) | The booster pipeline (selection → consume → execution) and the editor wizard that scaffolds a new booster. |
| **Rewards** | [Rewards/REWARDS.md](Rewards/REWARDS.md) | `BaseRewardPanel` + `RewardDefinition`/`RewardApplier` + the fly-to-HUD animation, and the claim chain every liveop reward panel inherits. |
| **Leaderboard** | [Leaderboard/LEADERBOARD.md](Leaderboard/LEADERBOARD.md) | Weekly league vs simulated bots. ⚠️ Self-contained (NOT a registered LiveOp) and currently **not wired** — see its "Known issues" section. |
| **LevelSystem** | [LevelSystem/LEVEL_SYSTEM.md](LevelSystem/LEVEL_SYSTEM.md) | The canonical level counter (`v_LevelNoForText`) every unlock gate reads. ⚠️ All scripts in the folder are dead; the live writer is `BaseGameManager`. |
| **Core** | [Core/CORE.md](Core/CORE.md) | Three unrelated seams: the level-fail condition framework, two DI interfaces, and a dormant `Scripts/` folder. |
| **UI** | [UI/UI.md](UI/UI.md) | Mostly art; the only code is the onboarding/tutorial contract and booster overlay views. |
| **Economy** | [Economy/ECONOMY.md](Economy/ECONOMY.md) | Only the lives/energy refill clock. Coin balances are mutated directly by ~10 external consumers. |
| **Haptics** | [Haptics/HAPTICS.md](Haptics/HAPTICS.md) | One stateless SO mapping `HapticIntensity` → Lofelt presets behind the global vibration toggle. |
| **MetaLoader** | [MetaLoader/META_LOADER.md](MetaLoader/META_LOADER.md) | CSV config importer — ⚠️ **orphaned at runtime**; Firebase owns these targets now. |
| **Gameplay** | — | The sort-puzzle gameplay engine (`GameKit.Gameplay.*`): Board (models), Config (tuning SO classes), Services, Views, Controllers, Camera, Input, Theme, HUD, Boosters (Duster/Spool/Wand), Tutorial. The game supplies only data assets, prefabs, and a composition root (bootstrap + LifetimeScope + `ILevelNavigator`). |
| **Collection** | — | The collection/album panel (`GameKit.Collection`). Its `CollectionService.asset` points at the installed game's `BoosterDatabase` (missing-safe). |
| **Analytics** | — | Adjust/AppMetrica analytics loggers + scriptables (`GameKit.Analytics`), folded in from the old top-level `Assets/Analytics`. |
| **Tutorials** | — | Generic first-move / pinch-zoom hand tutorials + pointer notation (`GameKit.Tutorials`), folded in from `Assets/GameHudTutorials` + `Assets/PointerNotation`. |
| **Art / Config / InApp** | — | Assets only, no scripts. Also holds folded-in `Art/Particles`, `Art/Characters` (ex-Wajiha), `Art/Icons`, `UI/Animations` (CoinTrail). |

## Conventions

- **Docs live with their code.** A module's doc is `Assets/GameKit/<Module>/<NAME>.md`.
- **Docs describe what the code does**, not what it was meant to do. Dead, stubbed, and commented-out paths are called out as such rather than described as working features — several modules have significant dead code, and pretending otherwise is how it gets resurrected by mistake.
- **Keep them current.** Changing a module's architecture means updating its doc in the same change; for LiveOps also update `.claude/skills/liveops/references/`.
