# GameKit AppFlow — Screens, States & Flow

The app's screen layer: one ScriptableObject `State` per screen, a scene `GameFlow` router that turns SO
`GameEvent`s into FSM transitions, and the MVP views those states drive.

`Assets/GameKit/AppFlow/` · ~111 scripts · mostly the **global namespace** (only
`Blues.Core.GameHud`, `ProjectCore.GameHud`, `ProjectCore.Systems.DailyRewardSystem`,
`Game.LiveOps.DailyReward`, `BlockOutRewrads` are namespaced). Core FSM types it builds on live in
`Blues.Core.StateMachine` / `Blues.Core.UI` / `Blues.Core.Application` under
[`Assets/GameKit/StateMachine/`](../StateMachine/).

---

## Mental model

Every screen is a **ScriptableObject state** (`UIViewState` or `GameState`) that owns *logic* but no scene refs;
its **view** is a pooled prefab the state spawns, shows, and binds buttons to (`OnBindView` / `OnUnbindView`).
Navigation is never a direct call: gameplay/UI raises a `GameEvent` asset (`e_GoToLevelFail`, `e_GoToShop`, …),
the scene singleton [`GameFlow`](GameFlow/Scripts/GameFlow.cs) maps that event to a `Transition` asset plus a
`UICloseReasons`, and `FiniteStateMachine` executes it against a **stack of paused states**.

The close *reason* — not the destination — decides whether the previous screen is paused (pushed) or torn down:
`Home`/`Game` → `ClearAll`, `ResumeGame`/`Revive`/`SkipLevel` → `PopOne`, `ResumeAny` → `PopUntil`,
`FullScreenPlacement`/`UntouchPaused` → push a modal on top
([`BaseApplicationFlowController.GetPolicyForReason`](../StateMachine/GameFlow/Scripts/BaseApplicationFlowController.cs)).
Going "back" is just `e_BackBtnPressed` → `PopPausedState()`.

## Screen map

```
AppBase.Start ──► FiniteStateMachine.Init ──► SplashState            (StateMachine/GameFlow)
                                                  │ loads GameScene additively,
                                                  │ instantiates Resources/GameFlow,
                                                  │ LiveOpsBootstrapper.Bootstrap()
                                                  ▼
                                        GameFlow.Boot()
                                  v_LevelNoForText <= 1 ? ─────────────┐
                                                  │ no                 │ yes (first-level tutorial)
                                                  ▼                    ▼
    ┌───────────────────────────────►  HomeState  ─── e_GotoGameWithInt ──►  YarnSortGameState : GameState
    │                                (HomeView)                              (gameplay prefab + GameHud)
    │                                    │                                        │
    │  e_GotoHomeWithInt(ClearAll)       │ modals (FullScreenPlacement = push)     │  in-game modals
    │                                    ├─► ShopState (ShopView)                  ├─► SettingsState
    │                                    ├─► LivesRefillState                      ├─► LeaveState ──► Home
    │                                    ├─► HomeSettingsState                     ├─► GiveUpState (manual)
    │                                    └─► RemoveAdsState                        └─► BuyBoosterState
    │                                                                              │
    │                                  ┌───────────────────────────────────────────┤
    │                                  ▼                                           ▼
    │                          LevelCompleteState                           LevelFailState
    │                          (LevelCompleteView                           (LevelFailView →
    │                           + StreakFooterRouter)                        LevelFailFlowController)
    │                                  │                                           │
    │        level >= GoToHomeOn…FromLevel ? GoToHome : back to game               │ continue → e_BackBtnPressed
    └──────────────────────────────────┘                              OnFinalClose ▼
                                                                RetryAfterLevelFailState ──► Home / retry

Cross-cutting pushes (any screen):  BoosterUnlockedState · FeatureUnlockedState · RetryState ·
                                    IAPPurchasePending/Success/FailedState (UnityIAPNetwork callbacks)
Overlay-only (no State asset):      DailyRewardView · LeaderboardView · NoWifiView · WarningMovesView
```

`Transition` assets carry the destination plus an optional `ClosePolicy` override; the real wiring is on the
[`GameFlow.prefab`](GameFlow/Prefabs/Resources/GameFlow.prefab) (loaded from `Resources` by `SplashState`).

---

## Sub-areas

### `GameFlow/` — router, boot & device settings

| Type | File | Role |
|---|---|---|
| `GameFlow` | [GameFlow/Scripts/GameFlow.cs](GameFlow/Scripts/GameFlow.cs) | The only navigation authority. Subscribes ~16 `GameEvent`s → `GoTo(transition, reason)`. Also owns the IAP pending/success/failed screens off `UnityIAPNetwork.OnPurchaseStateEvent`. |
| `AppBase` | [GameFlow/Scripts/AppBase.cs](GameFlow/Scripts/AppBase.cs) | Scene bootstrap: sound service, pool managers, `TimeMachine.Tick()`, first-launch analytics, then `FSM.Init()`. Sets `homeState.Reason = AppLaunch`. |
| `BannerController` | [GameFlow/Scripts/BannerController.cs](GameFlow/Scripts/BannerController.cs) | Bridges `FiniteStateMachine.OnStateEntered/OnStateResumed` to `MonetizationAdsService.Show/HideBanner()`. Visibility is driven purely by each `State.ShowBanner` flag — **no banner code lives in any state**. |
| `AdaptiveFrameRate`, `FrameRateSetter`, `MultiTouchSetter`, `URPRenderScaleSetter` | same folder | Device-side knobs; unrelated to navigation. |

**Entry/exit:** `SplashState` instantiates the `GameFlow` prefab, then calls `Boot()`. `Boot()` routes to
`gameTransition` while `v_LevelNoForText <= 1` (first-level tutorial), otherwise to Home
([GameFlow.cs:125](GameFlow/Scripts/GameFlow.cs)). The older `_firstMoveTutorialSeen` DBBool is still serialized
but **no longer read** — the comment explains Yarn never sets it, which made the boot-to-game path sticky.

### `States/HomeScreen/` — the hub (MVC, not MVP)

Home is the only screen split three ways: `HomeState` (flow/business), `HomeSystemSO` (controller — every
decision), `HomeView` (passive).

| Type | File | Role |
|---|---|---|
| `HomeState` | [States/HomeScreen/Scripts/HomeState.cs](States/HomeScreen/Scripts/HomeState.cs) | Re-asserts `LiveOpsDispatcher.Initialize()` **before** `base.Enter`, wires Play/Settings, gates Play on `v_CurrentLives >= GameConfig.EnergyPerLevel` (else `e_GoToLivesRefill`), toggles background music. Exposes `HomeEntryReason { AppLaunch, LevelWin, LevelFail, Exit }`. |
| `HomeSystemSO` | [States/HomeScreen/Scripts/HomeSystemSO.cs](States/HomeScreen/Scripts/HomeSystemSO.cs) | The controller SO. Subscribes the navigation events (`e_OpenStore`, `e_OpenHome`, `e_OpenJourney`, …), owns the section index, decides play-button difficulty/level text, pulls the illustration from an `IHomeIllustrationSource` (asset: `JourneyService`), locks home on show and schedules `OnHomeLanding` after `homeLandingDelay` (0.4 s). |
| `HomeView` | [States/HomeScreen/Scripts/HomeView.cs](States/HomeScreen/Scripts/HomeView.cs) | Passive `UIBase`. Only serialized refs + dumb animations; forwards `OnEnable/OnDisable/Show/Resume` to the system. `SetHomeLock` drives `blockerCanvas`. |
| `IHomeView`, `UISection` | [States/HomeScreen/Scripts/IHomeView.cs](States/HomeScreen/Scripts/IHomeView.cs) | The view contract; `UISection { Shop, Leaderboard, Home, Journey, Settings }` — index order **must** match the panel list passed to `IUIFlowTransition.Initialize`. |
| `IHomeSection`, `IHomeSectionPanel`, `IHomeIllustrationSource` | [States/HomeScreen/Scripts/IHomeFeatureContracts.cs](States/HomeScreen/Scripts/IHomeFeatureContracts.cs) | Shell contracts so LiveOps can plug into home with **no compile-time dependency** in either direction. |
| `HomeStatesFlowManager` | [States/HomeScreen/Scripts/HomeStatesFlowManager.cs](States/HomeScreen/Scripts/HomeStatesFlowManager.cs) | `IUIFlowTransition` — DOTween horizontal panel slide; recomputes slide distance from the live root width each transition. |
| `IllustrationScaler`, `PlayButtonGlowAnimation`, `GiftProgressBarController`, `UIHighlightGroup`, `HighlightItem`, `OnboardingBase` | same folder | Cosmetics + a small highlight/onboarding helper set. |

**Entry/exit:** entered by `e_GotoHomeWithInt` (always forced to `UICloseReasons.Home` → `ClearAll`, see
[GameFlow.cs:273](GameFlow/Scripts/GameFlow.cs)); exits when Play raises `e_GotoGameWithInt` after a 0.05 s delay.

### `States/Game/` + `States/BO_Game/` — gameplay shell & HUD

| Type | File | Role |
|---|---|---|
| `GameState` *(abstract)* | [States/Game/GamePlay/Scripts/GameState.cs](States/Game/GamePlay/Scripts/GameState.cs) | Spawns a gameplay prefab **and** a HUD prefab (pooled or `Resources.Load`), parents them under `StateRootManager.States` / `.UIStates`, raises `GameStateEnter/Paused/Resumed/Exit`. Concrete: `YarnSortGameState` (shipping), `NormalGameState`, `TemplateGameState`. |
| `GameHud` | [States/Game/GameHud/Scripts/GameHud.cs](States/Game/GameHud/Scripts/GameHud.cs) | `IShowable` base. Header/footer are **faded** (CanvasGroup alpha) rather than slid; `_showBarsEvent` / `_hideBarsEvent` / `_snapBarOffEvent` let tutorials and states drive the bars without a reference. |
| `NormalGameHud`, `DefaultGameHud` | [Game/GameHud/Scripts/NormalGameHud.cs](States/Game/GameHud/Scripts/NormalGameHud.cs), [BO_Game/Scripts/DefaultGameHud.cs](States/BO_Game/Scripts/DefaultGameHud.cs) | The two `GameHud` subclasses. `DefaultGameHud` applies `PlatformFooterSetter.ApplyOffset()` **before** `base.Awake()` caches the footer, and sequences the energy reveal behind the bar fade. |
| `BannerAreaView` | [States/Game/GameHud/Scripts/BannerAreaView.cs](States/Game/GameHud/Scripts/BannerAreaView.cs) | Banner slot inside the HUD prefab. Background is shown only on `MonetizationAdsService.OnBannerVisible == Show`; the X button raises `e_GoToRemoveAds` (suppressed while `controlsLocked`). Swaps sprite on `ThemeManager.ThemeChangedGlobal`. |
| `Hud`, `HudAnimations` | [States/Game/GameHud/FinalScripts/](States/Game/GameHud/FinalScripts/) | **Legacy slide-based HUD**, not a `GameHud` subclass and not used by `GameState`. `Hud.Hide()` even calls the *slide-in* helpers — treat as dead code. |
| `GameStateInitializer` | [States/Game/GamePlay/Scripts/GameStateInitializer.cs](States/Game/GamePlay/Scripts/GameStateInitializer.cs) | **Fully commented out** — an empty `Awake`. Dead. |
| `TapPointerFX`, `HudLevelView`, `BoostersBuyAnimationConfig` | — | HUD cosmetics/config. |

**Entry/exit:** `e_GotoGameWithInt` carries a raw `UICloseReasons` int (validated in `GotoGame`); Exit awaits the
HUD hide, `ExitImmediate` skips it for bulk clears.

### `States/LevelComplete/` & `States/LevelFail/`

| Type | File | Role |
|---|---|---|
| `LevelCompleteState` | [States/LevelComplete/Scripts/LevelCompleteState.cs](States/LevelComplete/Scripts/LevelCompleteState.cs) | Claim / claim-2×-with-rewarded; grants `RewardOnLevelComplete`, fires interstitial, then routes: `levelNoForText >= GameConfig.GoToHomeOnLevelCompleteFromLevel` → Home, else back to gameplay + `loadCurrentLevelEvent`. |
| `LevelCompleteView` | [States/LevelComplete/Scripts/LevelCompleteView.cs](States/LevelComplete/Scripts/LevelCompleteView.cs) | Exposes `ClaimButton` / `Claim2xButton` / `ClaimSoloButton`; calls `streakRouter.SyncAndDecide()` to show the winning LiveOp footer. |
| `LevelFailState` | [States/LevelFail/Scripts/LevelFailState.cs](States/LevelFail/Scripts/LevelFailState.cs) | Builds the whole `LevelFailContext` (reason, visuals, prices, ~10 callbacks) in `OnBindView` and hands it to the view. Reason comes from `FailService.LastReason.Id` mapped in `ResolveReason()`. |
| `LevelFailView` | [States/LevelFail/Scripts/LevelFailView.cs](States/LevelFail/Scripts/LevelFailView.cs) | Locks camera/controls on enable and restores on disable; runs `LevelFailFlowController.StartFlow(ctx)`; handles the hold-to-peek fade and the free-play-on layout shift (`boardsRect.offsetMax.y = -320`). |
| `LevelFailPanelBase` + `OutOfMovesBoardPanel`, `LooseALifeWarningBoardPanel`, `BombExplodedBoardPanel` | [States/LiveopsFailFlow/LevelFail/](States/LiveopsFailFlow/LevelFail/Scripts/), [States/LevelFail/Scripts/BombExplodedBoardPanel.cs](States/LevelFail/Scripts/BombExplodedBoardPanel.cs) | The authored fail boards. `LooseALifeWarningBoardPanel.CanShow` returns `true` unconditionally — it is the catch-all terminator of the chain. |
| `HoldAndSeeInput`, `FailOfferPresenter` | same | Hold-to-see-the-board input (with an EnhancedTouch fallback for missed `PointerUp`) and the reason-driven IAP offer swap. |

**Entry/exit:** `e_GoToLevelComplete` / `e_GoToLevelFail` (both `FullScreenPlacement` → gameplay is *paused*, not
exited). Fail exits either via continue (`e_BackBtnPressed` after 0.2 s → resumes gameplay) or via
`OnFinalClose` → `e_GoToRetryAfterLevelFail` + `streakFailedEvent`.

### `States/LiveopsFailFlow/` — the two arbitration hosts

Split into `LevelFail/` (fail screen) and `LevelExit/` (quit dialog). See
[Panel arbitration](#panel-arbitration-level-fail--leave) below — this is the subtle part.

| Type | File | Role |
|---|---|---|
| `LevelFailFlowController` | [.../LevelFail/Scripts/LevelFailFlowController.cs](States/LiveopsFailFlow/LevelFail/Scripts/LevelFailFlowController.cs) | Priority-sorted `List<ILevelFailPanel>` (Odin-serialized). One main panel at a time + one footer alongside. |
| `LiveOpsFlowController` | [.../LevelExit/Scripts/LiveOpsFlowController.cs](States/LiveopsFailFlow/LevelExit/Scripts/LiveOpsFlowController.cs) | Same machinery for the Leave dialog, plus `HasAnyValidPanel` (interception test) and `ShowFooters` (footer-only path). |
| `LiveOpFooterFailPanel` / `LiveOpFooterLiveOpsPanel` | [.../LiveOpFooterFailPanel.cs](States/LiveopsFailFlow/LevelFail/Scripts/LiveOpFooterFailPanel.cs), [.../LiveOpFooterLiveOpsPanel.cs](States/LiveopsFailFlow/LevelExit/Scripts/LiveOpFooterLiveOpsPanel.cs) | Adapters that let a LiveOp footer prefab enter the *same* list. |
| `MagicStreakFailPanel`, `BroomFailPanel`, `RaceFailPanel` / `MagicStreakLosePanel`, `BroomStreakLosePanel`, `RaceLosePanel` | both folders | Per-LiveOp "you'll lose your streak" boards. `IsLiveOp => true`. |
| `LevelFailContext`, `LiveOpsContext` | — | The DTOs the state builds; panels never reach into a service for level/streak data. |

### `States/Shop/` · `States/RemoveAds/` · `States/BoosterBuy/` · `States/BoosterUnlocked/`

| Type | File | Role |
|---|---|---|
| `ShopState` / `ShopView` / `HomeShopView` | [States/Shop/Scripts/](States/Shop/Scripts/) | Coin/IAP store. `ShopView` is the pushed modal; `HomeShopView` is the home *section* variant (same content, lives inside `HomeView`'s panel list). Both hide `noAdsSections` when `removeAds` is set. |
| `RemoveAdsState` / `RemoveAdsView` | [States/RemoveAds/Scripts/](States/RemoveAds/Scripts/) | Remove-ads upsell. Subscribes `purchaseSuccess` in `Enter`/`Resume` and unsubscribes in `Pause`/`Exit` — deliberately **not** `OnEnable/OnDisable`, because a paused subscription causes a second concurrent `PopPausedState` when `IAPPurchaseSuccessState` exits ([RemoveAdsState.cs:27](States/RemoveAds/Scripts/RemoveAdsState.cs)). |
| `RemoveAdsVisibility` | [States/RemoveAds/Scripts/RemoveAdsVisibility.cs](States/RemoveAds/Scripts/RemoveAdsVisibility.cs) | Still on the **retired legacy** `ILiveOpsOnboarding` / `ILiveOpsExpireFlow` interfaces — it only runs if `LegacyLiveOpDiscovery` picks it up. |
| `BuyBoosterState` / `BuyBoosterView` | [States/BoosterBuy/Scripts/](States/BoosterBuy/Scripts/) | Buy-with-coins or watch-rewarded for `_pendingBooster`, injected by `BoosterButtonView.SetPendingBooster` *before* the transition. Rewarded grant uses `rewardedQuantity` (min 1), not a hardcoded 1. |
| `BoosterUnlockedState` / `BoosterUnlockedView` / `BoostersOnBoardingHandler` | [States/BoosterUnlocked/](States/BoosterUnlocked/) | Unlock ceremony driven by the `DBString BoosterID` + `BoosterDatabaseSO`. `Unlock()` grants no charges, so the state adds `rewardedQuantity` — guarded on `GetChargeCount() == 0` so replaying doesn't stack. |

### `States/DailyRewards/` — the daily-login calendar

Largest sub-area (≈1.9 k lines). `DailyRewardSystemSO` (a `LiveOpBaseConfig`) owns the cycle, week scaling and
missed-day rules; `DailyRewardServiceSO` (`namespace Game.LiveOps.DailyReward`) is the **first-class LiveOp
wrapper** — it subclasses `LiveOpServiceBase<LiveOpSaveData>`, implements `ILiveOpFlowProvider` (`Reward` kind)
and owns initialization (server time → calendar), so **no scene `DailyRewardSystemMono` is required**.
`DailyRewardSystemMono` is the older scene-driven initializer and `DailyRewrardVisibility` *(sic)* the legacy
reward-popup bridge that publishes itself via `SceneComponentRegistry<T>` and polls the panel root by
**reflection** (`GetField("panelRoot")`) to know when the popup closed. `DailyRewardView` (744 lines) is a plain
overlay `UIBase` — there is **no `DailyRewardState`** and no `GameFlow` transition for it.

### `States/Settings/` & `States/HomeSetting/`

`SettingsState` is the in-game settings modal (support / restore / privacy / close / **home** button that exits
the level); `HomeSettingsState` is the home-screen counterpart with the same three link buttons and no home exit.
`BasePanelView` + `BasePanelConfig` re-skin a panel by `LevelsData.GetLevelDifficulty(levelNo)`; its hard/very-hard
tag toggles are commented out. `SettingsAnimationConfig`, `GameSettings` are data holders.

### Smaller states

| Sub-area | Entry event | Notes |
|---|---|---|
| `States/Leave/` | `e_GoToLeave` (`UntouchPaused`) | Quit dialog. `LeaveView` runs the LiveOps interception; `LeaveState.Leave()` goes straight Home + `Giveup` interstitial, deliberately skipping the retry panel (energy was already spent on the first move). |
| `States/GiveUp/` | `e_GoToGiveUp` | The **only** state driven outside the FSM — `GameFlow.GoToGiveUp()` calls `StartCoroutine(giveUpState.Enter(null))` on a view injected in `Start()`. Plain `State`, not `UIViewState`. |
| `States/Retry/`, `States/RetryAfterLevelFail/` | `e_GoToRetry`, `e_GoToRetryAfterLevelFail` | Replay-current-level and post-fail retry/quit. |
| `States/LivesRefill/` | `e_GoToLivesRefill` | Energy refill: coins, rewarded ad, or bounce to shop. |
| `States/FeatureUnlocked/` | `e_GoToFeatureUnlocked` | `FestureUnlockController` *(sic)* watches `BO_OnLevelLoad` and fires the state when the level matches an `UnlockXFeatureOnLevel` and its onboarding DBBool is unset. |
| `States/NoInternet/`, `States/MovesWarning/`, `States/FeedBacks/` | — | `InternetChecker` (static `OnInternetStatusChanged`), `NoWifiView`, `WarningMovesView`, `FeedbackHandler`: plain overlay MonoBehaviours, no state assets. |
| `Scripts/`, `Architecture/View/` | — | `GameConfig` (the shared remote-tuned config SO), `LeaderboardView` (overlay `UIBase`, statics `LeaderboardOpened/Closed`), `VerticalGroupAnimator` (staggered child fade used by the fail screen), `TileInfo`/`TileType`/`RotateImageForever`. |

---

## Panel arbitration (Level Fail / Leave)

Two controllers, one design. Both hold a single Odin-serialized list sorted **descending by `Priority`**, and both
run *two* independent selections over it.

**1. The main slot — a chain, not a set.** `StartFlow(ctx)` sorts, hides everything, then walks the list from the
top and shows the **first** panel whose `CanShow(ctx)` is true. Closing that panel (`RequestClose()`) hides it and
*continues from the same index*, so the player can be walked through several boards in priority order. When the
list is exhausted the terminal callback fires — `ctx.OnFinalClose` (fail → `e_GoToRetryAfterLevelFail`) or
`ctx.OnAccept` (leave → actually quit). `_liveOpAlreadyShown` caps the chain at **one** `IsLiveOp` panel per flow,
so Magic/Broom/Race can't stack three "you'll lose your streak" boards
([LevelFailFlowController.cs:150](States/LiveopsFailFlow/LevelFail/Scripts/LevelFailFlowController.cs)).

**2. The footer strip — a separate winner-takes-all pass.** A footer is not a competitor for the slot; it coexists
with whatever the main pass showed. `ShowPreferredFooter()` scans the *same* list for footer adapters only, picks
the highest-priority one whose `CanShow` is true, and shows it in addition. Every non-footer branch in the loops
explicitly `continue`s past footer entries.

**How LiveOp footers get into that list.** On the first `StartFlow`, `BuildFooterPanels()` (run **once**, guarded
by `_footersBuilt`) walks `LiveOpRegistrySO.AllServices`, keeps the ones implementing
`ILiveOpLevelCompleteFooter`, asks each for `GetFooterAddress(LiveOpFooterScreen.LevelFail | .Leave)`, and
`LiveOpAddressables.SpawnAsync`s that prefab under `footerContainer`. The loaded prefab exposes an
`ILevelFailPanel` / `ILiveOpsPanel` component (searched with `GetComponentInChildren(true)`), which is wrapped in
`LiveOpFooterFailPanel` / `LiveOpFooterLiveOpsPanel` and appended to the list. The adapter is what makes this
work: **priority and eligibility come from the LiveOp service** (`FooterPriority`, `ShouldShowFooter()`, evaluated
live on every flow), while **rendering delegates to the prefab's own panel implementation** — the prefab's own
`Priority`/`CanShow` are deliberately ignored (e.g. `MagicStreakLevelCompleteFooter` returns `Priority => 0`,
`CanShow => true`).

Because the load is async, the very first fail/leave screen can open before the footer exists. Both controllers
therefore re-run `ShowPreferredFooter()` from inside the spawn callback when `_ctx != null && isActiveAndEnabled`
([LevelFailFlowController.cs:80](States/LiveopsFailFlow/LevelFail/Scripts/LevelFailFlowController.cs)).

**Leave-screen quirks.** `HasAnyValidPanel(ctx)` — the test `LeaveView` uses to decide whether to *intercept* the
quit — skips footer entries, and footers are built inside `StartFlow` which runs *after* that test. So a footer can
never on its own turn a quit into an interception; it only rides along. To still show the strip on the plain quit
dialog, `LeaveView.Show()` calls the separate `LiveOpsFlowController.ShowFooters(ctx)` path.

Level **Complete** uses a different (shared) implementation of the same idea: `StreakFooterRouter` →
`LiveOpFooterArbiter` in the LiveOps module — see [../LiveOps/ARCHITECTURE.md](../LiveOps/ARCHITECTURE.md).

---

## Integration points

| Module | Direction | Signal / API |
|---|---|---|
| **LiveOps dispatcher** | AppFlow → LiveOps | `HomeState.Enter` calls `liveOpsDispatcher.Initialize()` (asset `LiveOpsDispatcher`) *before* `base.Enter`. `HomeSystemSO.OnViewShown` schedules `OnHomeLanding` 0.4 s later. Win/fail routing rides the existing events: `BO_OnLevelWin`, `e_GoToLevelFail`, with `v_LevelNoForText` as the level. |
| **LiveOps home lock** | LiveOps → AppFlow | `e_ChangeHomeLockState` (`GameEventWithBool`) → `HomeSystemSO.HandleHomeLock` → `HomeView.SetHomeLock` → `blockerCanvas`. Home locks itself on show and relies on the flow runner to release it. |
| **LiveOps footers** | AppFlow → LiveOps | `LiveOpRegistrySO` + `ILiveOpLevelCompleteFooter` + `LiveOpAddressables.SpawnAsync` (Resources-path loader) in both flow controllers; `StreakFooterRouter.SyncAndDecide()` from `LevelCompleteView`. |
| **LiveOps home sections** | LiveOps → AppFlow | `IHomeSectionPanel` / `IHomeIllustrationSource` (`JourneyService` asset supplies the home illustration), `e_OpenJourney`, `e_CheckMagicVisibility`, `magicStreakHomeShown` (0 = shown, 1 = not). |
| **Boosters** | both | `BuyBoosterState` ↔ `BoosterSO` / `BoosterDefinitionSO` / `BoosterDatabaseSO`; `e_GoToBuyBooster`, `e_GoToBoosterUnlock`, `updateBoostersUICountEvent`. |
| **Rewards / daily** | AppFlow → LiveOps | `DailyRewardServiceSO` is registered in `LiveOpRegistrySO` and provides the `Reward`-kind flow on home landing. |
| **Ads / banner** | AppFlow → Monetization | `MonetizationAdsService` (`AdsService` asset): `ShowRewarded(placement, cb)` from LevelFail / LevelComplete / BuyBooster / LivesRefill; `TryShowInterstitial("LevelComplete" \| "Giveup")`; banner exclusively via `BannerController` + `State.ShowBanner`; `OnBannerVisible` → `BannerAreaView`. |
| **Economy** | AppFlow → economy | `DBInt softCurrency` (`Increment`/`Decrement`), `v_CurrentLives` + `v_UnlimitedLivesFlag`, `GameConfig` prices (`coinsRequiredToContinue`, `CoinsRequiredToContinueAfterBombFail`, `EnergyPerLevel`, `RewardOnLevelComplete`). |
| **Analytics** | AppFlow → analytics | `AnalyticsSystem`, `AdjustAnalytics.TrackSpendAsset/TrackEarnAsset`, `MeticaAnalyticsSystem` (null-conditional everywhere — it's optional). |
| **IAP** | Monetization → AppFlow | `UnityIAPNetwork.OnPurchaseStateEvent` → `GameFlow.GoToPurchaseView`; `PlayTrailParticles` (`GameEventWithParam<string>`) → `LevelFailView.HandlePurchased`. |
| **Fail framework** | Gameplay → AppFlow | `FailService.LastReason.Id` is the single source of truth for the fail reason; `FailReasonVisualSet` supplies per-reason icon/title/detail. Adding a reason = new id + a panel that matches it. |

---

## Rules & gotchas

1. **Home lock is a contract, not a suggestion.** `HomeSystemSO.OnViewShown` always sets `SetHomeLock(true)`.
   Nothing in AppFlow unlocks it — the LiveOps flow runner releases it via `e_ChangeHomeLockState` in a `finally`.
   If you add a home-landing consumer that can early-return, home stays permanently un-tappable.

2. **`GameEvent` is a ScriptableObject and nulls its handler list in `OnDisable`.** SO subscribers cannot self-heal
   in `OnEnable` the way scene components do, which is exactly why `HomeState.Enter` re-calls
   `liveOpsDispatcher.Initialize()` every single home entry ([HomeState.cs:23](States/HomeScreen/Scripts/HomeState.cs)).
   Any new SO that subscribes at boot needs the same re-assertion.

3. **`GameFlow.UnregisterFlowEvents` mostly doesn't unregister.** All but two subscriptions are lambdas, and
   `UnregisterFlowEvents` builds *new* lambdas — delegate equality fails, so `UnSubscribe` is a no-op
   ([GameFlow.cs:158](GameFlow/Scripts/GameFlow.cs)). It happens to be harmless only because `GameEvent.OnDisable`
   clears everything and `GameFlow` is a singleton. Don't copy the pattern; use method groups.

4. **Footers must not self-hide in `Awake`.** Footer prefabs ship with an inactive root, so `Awake` first runs
   *inside* the first `SetActive(true)`; a `SetActive(false)` there re-hides the footer the instant it's shown.
   Visibility is owned entirely by the arbiter/adapter (`MagicStreakLevelCompleteFooter.Awake` documents this).

5. **Footer adapters override the prefab's own priority.** If a footer never shows, check the *service's*
   `FooterPriority` / `ShouldShowFooter()`, not the prefab component — the wrapped view's values are ignored.

6. **Footers are built once, panels are re-sorted every flow.** `_footersBuilt` guards the spawn, but the async
   load may land after the screen is already up; that's why the spawn callback re-arbitrates. A footer added to a
   *disabled* controller will never appear until the next `StartFlow`.

7. **Coroutine ownership is split and both sides cancel.** `BaseApplicationFlowController` stops its previous
   `HandleTransition` on every `GoTo`, and `FiniteStateMachine.TransitionTo` stops its previous `DoTransition`.
   A transition interrupted mid-`Exit` can leave a HUD + gameplay alive, so `GameState.Enter` defensively calls
   `ReleaseSpawnedInstances()` first and keeps that method yield-free so it can't itself be interrupted
   ([GameState.cs:41](States/Game/GamePlay/Scripts/GameState.cs)). Never add a `yield` to it.

8. **Bulk clears must use `ExitImmediate`.** `ClearPausedStates` / `JumpTo` tear occluded states down with no exit
   animation; only the visible top state and the destination animate. A state that overrides `Exit` but not
   `ExitImmediate` will pay its full animation N times on the way Home.

9. **Modal subscriptions belong in `Enter`/`Exit` + `Pause`/`Resume`, not `OnEnable`/`OnDisable`.** A state SO
   stays enabled while paused; `RemoveAdsState` shows the fix and the bug it prevents (double `PopPausedState`).

10. **`LooseALifeWarningBoardPanel.CanShow` is `true` unconditionally.** It is the chain terminator — anything you
    want shown after it will never appear. Priorities decide everything; all panels default to `30` in code and are
    differentiated only by their serialized values on the prefabs.

11. **`Priority` ties are resolved by `List.Sort` (unstable).** Two panels with the same serialized priority can
    swap order between runs. Give every panel a distinct value.

12. **Dead / stubbed paths to ignore:** `Hud` + `HudAnimations` (legacy slide HUD, `Hide()` plays the show
    animation), `GameStateInitializer` (entirely commented out), `GameFlow._firstMoveTutorialSeen` (serialized,
    never read), `SettingsState.RestorePurchases` / `HomeSettingsState.RestorePurchases` (empty),
    `NormalGameHud.OnSettingsClicked` (empty), `RemoveAdsVisibility` (retired legacy LiveOps interfaces),
    `BasePanelView`'s hard/very-hard tag toggles (commented out), and the `InjectView` methods commented out across
    most states — view injection now happens through `UIViewState.OnBindView`.

13. **`NormalGameHud` leaks its Replay listener** — `OnEnable` adds `ReplayBtn.onClick` but `OnDisable` only
    removes `SettingsBtn`'s. On a pooled HUD the listener accumulates per re-show
    ([NormalGameHud.cs:40](States/Game/GameHud/Scripts/NormalGameHud.cs)).

14. **`GiveUpState` bypasses the FSM.** It's entered by a raw `StartCoroutine` from `GameFlow` on a manually
    injected view, so it is invisible to the paused-state stack, to `BannerController`, and to `ClearAll`.
