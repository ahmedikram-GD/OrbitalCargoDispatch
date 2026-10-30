# GameKit FSM — The States

`namespace GameKit.StateMachine` · `GameKit.StateMachine.Application` · `GameKit.AppFlow` ·
root `Assets/GameKit/StateMachine/` + `Assets/GameKit/AppFlow/States/`

Companion to [STATE_MACHINE.md](STATE_MACHINE.md), which documents the *machine*. This document covers the
*states*: what each one is, how it is entered and left, what it owns, and the contract between a state and its
view.

---

## 1. The core rule

> **A state is the brain of exactly one view. The state holds all functionality. The view is passive and only
> asks the state what to do — the view never references the state.**

Mechanically this is expressed by two hooks on `UIViewState`
([StateMachine/Scripts/UiViewState.cs:148](StateMachine/Scripts/UiViewState.cs)):

```csharp
protected virtual void OnBindView(UIBase view)   { }  // state subscribes to the view's buttons
protected virtual void OnUnbindView(UIBase view) { }  // state removes exactly what it added
```

The dependency arrow is one-way:

```
   State  ──[reads buttons, subscribes handlers, pushes data]──►  View
     ▲                                                             │
     └──────────── ✗ NEVER ────────────────────────────────────────┘
              (no serialized State field, no State method call)
```

What belongs on which side:

| Concern | Owner |
|---|---|
| Button click → what actually happens | **State** |
| Currency spend / grant, ad requests, analytics, IAP | **State** |
| "Which panel/variant should show" decisions, threshold checks against `GameConfig` | **State** |
| Persistent flag writes (`DBBool` / `DBInt`) | **State** |
| Raising navigation `GameEvent`s | **State** |
| Exposing `Button` properties, text/sprite setters, `Show`/`Hide` overrides, DOTween sequences | **View** |
| Sound + haptic click feedback (invoked *by* the state) | **View** |
| Purely cosmetic buttons that touch nothing but themselves | **View** |

When a screen needs more than button wiring, the state passes a **context object** of data + callbacks and the
view merely distributes it. `LevelFailState.BuildContext()`
([../AppFlow/States/LiveopsFailFlow/LevelFail/Scripts/LevelFailContext.cs](../AppFlow/States/LiveopsFailFlow/LevelFail/Scripts/LevelFailContext.cs))
is the reference implementation of that pattern.

---

## 2. The three state base types

| Base | File | Owns | Used by |
|---|---|---|---|
| `State` | [StateMachine/Scripts/State.cs](StateMachine/Scripts/State.cs) | Nothing — just the five lifecycle slots and the two authored flags `PausePreviousState`, `ShowBanner`. | `SplashState`, `GiveUpState` |
| `UIViewState` | [StateMachine/Scripts/UiViewState.cs](StateMachine/Scripts/UiViewState.cs) | One `UIBase` view acquired by `stateId` from the pool or `Resources`, plus the `OnBindView`/`OnUnbindView` binding contract. | 17 screens/popups |
| `GameState` | [../AppFlow/States/Game/GamePlay/Scripts/GameState.cs](../AppFlow/States/Game/GamePlay/Scripts/GameState.cs) | A gameplay prefab under `StateRootManager.States` **and** a `GameHud` under `UIStates`, plus four `GameEvent`s. | `NormalGameState`, `TemplateGameState` |

All states are `ScriptableObject`s → **one shared instance per asset for the whole app lifetime**. There is no
`OnEnable`/`OnDisable` scoping, no per-instance fields, and they survive scene loads. Every rule about
subscribing in `Enter` / unsubscribing in `Pause` *and* `Exit` follows from that.

### The five lifecycle slots

| Slot | Called by the FSM when | Awaited? |
|---|---|---|
| `Enter(IState listener)` | this state becomes current | **yes, in full** — `CurrentState` is only assigned after it returns |
| `Pause()` | another state stacks on top of this one | yes |
| `Resume()` | the state above was popped | yes |
| `Exit()` | this state is fully torn down | yes |
| `ExitImmediate()` | this state is torn down as part of a **bulk** clear while occluded | yes, but must not yield or animate |

`IState` ([StateMachine/Scripts/IState.cs](StateMachine/Scripts/IState.cs)) is the deliberately narrow handle
the FSM hands to `Enter` — a state can only see `TransitionTo` / `ClearPausedStates` / `ReloadCurrentState`,
not the whole machine. In practice **no shipping state uses it**: they all navigate by raising `GameEvent`s.

---

## 3. How a state is executed

### 3.1 Boot

```
ApplicationBase.Start()                            (splash scene)
  └─ FiniteStateMachine.Init()                     FiniteStateMachine.cs:43
       CurrentState = BootState = SplashState.asset
       yield SplashState.Enter(fsm)                SplashState.cs:34
         additive-load game scene → wait progress + fake loader
         Instantiate(Resources.Load<GameFlow>("GameFlow"))
         activate scene → SetupStateRoots() → StateRootManager.Initialize()
         GameFlow.Boot()                           GameFlow.cs:134
         liveOpsBootstrapper.Bootstrap()
       OnStateEntered(SplashState); PausedStates.Clear()
```

`GameFlow.Boot()` branches on the current-level `DBInt`: `<= 1` boots straight into gameplay (so the
first-level tutorial plays), otherwise it boots Home.

### 3.2 Runtime routing — nothing calls a state directly

```
state / view / gameplay
      │  raises a GameEvent  (goToShopEvent, backBtnPressedEvent, goToGameEvent.Raise(6) …)
      ▼
GameFlow.RegisterFlowEvents()                      GameFlow.cs:149
      │  maps event → (Transition asset, UICloseReasons)
      ▼
BaseApplicationFlowController.GoTo()               BaseApplicationFlowController.cs:40
      │  stops any in-flight transition coroutine
      │  policy = transition.closePolicy != Default ? it : GetPolicyForReason(reason)
      │  ClearAll → fsm.ClearPausedStates()
      │  PopOne   → fsm.PopPausedState()
      │  PopUntil → fsm.JumpTo(ToState) and return
      ▼
FiniteStateMachine.TransitionTo(transition, pauseCurrent, exitCurrentImmediately: reason == Home)
      └─ DoTransition()                            FiniteStateMachine.cs:77
           1. bail if already in ToState
           2. bail if ToState is on the paused stack
           3. pauseCurrent || ToState.PausePreviousState ? PauseCurrentState() : ExitCurrentState(exitCurrentImmediately)
           4. yield transition.Execute()
           5. yield ToState.Enter(this)             ← fully awaited
           6. CurrentState = ToState; OnStateEntered
```

Reason → policy (`BaseApplicationFlowController.cs:83` / `:97`):

| `UICloseReasons` (raise code) | `ClosePolicy` | pauses current? | net effect on the outgoing state |
|---|---|:--:|---|
| `Home` (0) | `ClearAll` | no | whole stack `ExitImmediate`, then current **also** `ExitImmediate` — nothing animates on the way home, so the back button doesn't wait on an outgoing hide/fade |
| `Game` (1) | `ClearAll` | no | whole stack `ExitImmediate`, then current `Exit` (normal, animated) |
| `SkipLevel` (2), `Revive` (3), `ResumeGame` (4) | `PopOne` | no | current `Exit`, stack top `Resume` |
| `ResumeAny` (6) | `PopUntil` | no | `JumpTo` target: everything above it `ExitImmediate`, target `Resume` |
| `FullScreenPlacement` (5), `ShowFullScreenPlacement` (8), `DailyLogin` (7) | `Default` | **yes** | current is **paused and pushed**, not exited |
| `UntouchPaused` (9) | `Default` | no | current is **fully exited**, stack untouched |

The hardware/UI back button is wired straight to `PopPausedState()` (`BaseApplicationFlowController.cs:78`),
which is why almost every state closes itself with a single `backBtnPressedEvent.Invoke()`.

### 3.3 Inside `UIViewState.Enter` / `Exit`

```
Enter  (UiViewState.cs:24)
  base.Enter                       → stores Listener
  usePooling ? pooler.Get(stateId) : Instantiate(Resources.Load(stateId))
  parent → StateRootManager.UIStates, SetAsLastSibling, stretch RectTransform, reset scale/rotation
  _uiInstance = GetComponent<UIBase>()
  SetActive(true)
  _uiInstance.Show()               → FIRE-AND-FORGET, not awaited, no animation by default
  OnBindView(_uiInstance)          ← the state wires the view's buttons HERE

Exit   (UiViewState.cs:73)
  _uiInstance = null               → double-release guard, before any yield
  instance.BlockRaycast()          → swallow clicks during the transition gap
  OnUnbindView(instance)           ← the state removes its listeners HERE
  yield instance.Hide()            → AWAITED; UIBase.Hide ends in WaitUntil(completed)
  pooler.Release(stateId, …) or Destroy
  base.Exit()

ExitImmediate (UiViewState.cs:105)
  OnUnbindView → CloseImmediate() → release.  No yields, no animation.

Pause / Resume  → forward to _uiInstance.Pause() / Resume() (flip blocksRaycasts, force-complete tweens)
```

Because `Show()` is called with the defaults, **an enter animation exists only if the view overrides `Show`
and forwards `true`** (e.g. `RemoveAdsView.Show → base.Show(true, true)`). That is a view decision, not a
state decision.

### 3.4 Inside `GameState.Enter` / `Exit`

```
Enter  (GameState.cs:40)
  ReleaseSpawnedInstances()        ← idempotency guard: a previously interrupted Exit may still own instances
  spawn gameplayPrefabId under StateRootManager.States   (pool or Resources)
  spawn hudPrefabId    under StateRootManager.UIStates   (pool or Resources)
  gameHudInstance.Show()           → GameHud slides/fades its header + footer bars in
  GameStateEnter.Invoke()

Exit   (GameState.cs:111)
  yield gameHudInstance.Hide()     ← awaited (bar fade-out)
  ReleaseSpawnedInstances()        ← yield-free so cancellation can't leave it half-done
  base.Exit(); GameStateExit.Invoke()

ExitImmediate (GameState.cs:129)
  ReleaseSpawnedInstances() immediately — skips the HUD hide animation, because on the way Home the board
  would otherwise keep rendering on a black screen while Home builds.

Pause / Resume → IShowable.Pause()/Resume() on the HUD + GameStatePaused / GameStateResumed events
```

`GameState` talks to its HUD only through `IShowable` — it never touches a `GameHud` API directly beyond
`Show`/`Hide`.

### 3.5 Stack shape at runtime

```
                       ┌──────────────────────────┐
   CurrentState  ─────►│  e.g. LivesRefillState   │  visible, interactive
                       ├──────────────────────────┤
                       │  ShopState               │  paused (sortingOrder −10)
   PausedStates ──────►├──────────────────────────┤
   (Stack<State>)      │  HomeState               │  paused (sortingOrder −20)
                       └──────────────────────────┘
```

`v_CurrentStateSortingOrder` moves **only** in the FSM: `+10` on pause, `−10` on resume / pop / clear. Views
may read it (`UIBase.Show`) but must never write it.

---

## 4. State catalogue

Legend for **Enters via** — the `GameEvent` a state raises, and the `UICloseReasons` `GameFlow` pairs with it.

### 4.1 Flow map

```
Splash
  └─► Home ◄──────────────────────────────────────────────────────────┐
        │  play (energy OK)                                           │
        ▼                                                             │
      NormalGameState / TemplateGameState                             │
        ├─ win  ─► LevelCompleteState ─┬─► Home (level ≥ threshold) ──┘
        │                              └─► back to gameplay + load next level
        └─ fail ─► LevelFailState ─────┬─► continue (paid / free / rewarded / IAP) ─► gameplay
                                       └─► RetryAfterLevelFailState ─┬─► gameplay (retry)
                                                                     └─► Home

Paused overlays, pushed on top of whatever is current (UICloseReasons.FullScreenPlacement):
  SettingsState · HomeSettingsState · ShopState · LivesRefillState · BuyBoosterState
  BoosterUnlockedState · FeatureUnlockedState · RemoveAdsState · RetryState
  IAPPurchasePendingState ─► IAPPurchaseSuccessState / IAPPurchaseFailedState

Fully-exiting overlays (UICloseReasons.UntouchPaused):
  LeaveState · RetryAfterLevelFailState · IAP success / failed

Outside the FSM entirely:
  GiveUpState  (entered by GameFlow.Start + GameFlow.GoToGiveUp — see §5)
```

### 4.2 Per-state reference

| State | Base | `stateId` (pool key / Resources path) | Pool | `PausePrev` | `Banner` | View |
|---|---|---|:--:|:--:|:--:|---|
| `SplashState` | `State` | — | — | 0 | 0 | — (splash scene UI) |
| `HomeState` | `UIViewState` | *(asset-authored)* | ✓ | 0 | 0 | `HomeView` |
| `NormalGameState` | `GameState` | gameplay + hud prefab ids | ✓ | 0 | **1** | `GameHud` subclass |
| `TemplateGameState` | `GameState` | hud prefab id only | ✓ | 0 | **1** | `GameHud` subclass |
| `LevelCompleteState` | `UIViewState` | `LevelComplete` | ✓ | 0 | 0 | `LevelCompleteView` |
| `LevelFailState` | `UIViewState` | `LevelFail` | ✓ | 0 | 0 | `LevelFailView` |
| `RetryAfterLevelFailState` | `UIViewState` | `RetryAfterLevelFail` | ✓ | 0 | 0 | `RetryAfterLevelFailView` |
| `RetryState` | `UIViewState` | `Retry` | ✓ | 0 | 0 | `RetryView` |
| `LeaveState` | `UIViewState` | `LeavePanel` | ✓ | 0 | 0 | `LeaveView` |
| `GiveUpState` | `UIViewState` | `GiveUp` | ✓ | 0 | **1** | `GiveUpView` |
| `SettingsState` | `UIViewState` | `GameSettings` | ✓ | 0 | 0 | `SettingsView` |
| `HomeSettingsState` | `UIViewState` | `HomeSettings` | ✗ Resources | — | 0 | `HomeSettingsView` |
| `ShopState` | `UIViewState` | `Shop` | ✓ | 0 | 0 | `ShopView` |
| `LivesRefillState` | `UIViewState` | `RefillLives` | ✓ | 0 | 0 | `LivesRefillView` |
| `BuyBoosterState` | `UIViewState` | `BuyBoosters` | ✓ | 0 | 0 | `BuyBoosterView` |
| `BoosterUnlockedState` | `UIViewState` | `BoosterUnlocked` | ✓ | 0 | 0 | `BoosterUnlockedView` |
| `FeatureUnlockedState` | `UIViewState` | `FeatureUnlocked` | ✓ | 0 | 0 | `FeatureUnlockedView` |
| `RemoveAdsState` | `UIViewState` | `RemoveAdsView` | ✓ | **1** | 0 | `RemoveAdsView` |
| `IAPPurchasePendingState` | `UIViewState` | `PurchasePendingView` | ✓ | 0 | 0 | `PurchasePendingView` |
| `IAPPurchaseSuccessState` | `UIViewState` | `PurchaseSuccessView` | ✓ | 0 | 0 | `PurchaseSucessView` |
| `IAPPurchaseFailedState` | `UIViewState` | `PurchaseFailedView` | ✓ | 0 | 0 | `PurchaseSuccessView` *(class in `PurchaseFailedView.cs`)* |

`RemoveAdsState` is the **only** state with `PausePreviousState: 1` — it always stacks over whatever is
current, regardless of the reason the caller passed.

---

### `SplashState` — [GameFlow/Scripts/SplashState.cs](GameFlow/Scripts/SplashState.cs)

Boot state, headless. The deliberate exception to "`Enter` is setup, not work": it parks in `Enter` awaiting
the additive scene load, the fake loading bar, scene activation, state-root construction, `GameFlow.Boot()`
and the LiveOps bootstrap. Never exited in a normal session.

### `HomeState` — [../AppFlow/States/HomeScreen/Scripts/HomeState.cs](../AppFlow/States/HomeScreen/Scripts/HomeState.cs)

- **Enter**: re-asserts `liveOpsDispatcher.Initialize()` **before** `base.Enter` (a `GameEvent` drops its
  handlers on `OnDisable`, and an SO dispatcher cannot self-heal), then invokes `updateVerticalBar`, clears
  `dailyChallengeActivated`, turns background music on.
- **Binds**: `PlayButton`, `SettingsButton`.
- **Owns**: the energy gate — `currentLives < GameConfig.EnergyPerLevel && !unlimitedLives` routes to
  `goToRefillLivesEvent` instead of gameplay; otherwise a 0.05 s delay then `goToGameEvent.Raise(1)`
  (`Game` → `ClearAll`).
- **Owns the home system's lifecycle**: the rest of the home screen's logic lives in `HomeSystemSO`
  ([../AppFlow/States/HomeScreen/Scripts/HomeSystemSO.cs](../AppFlow/States/HomeScreen/Scripts/HomeSystemSO.cs)),
  the MVC controller. The **state** drives it — `OnViewEnabled` then `OnViewShown` from `OnBindView` (that
  order matters: `OnViewShown` early-returns until the system has a view), `OnViewDisabled` from
  `OnUnbindView`, `OnViewResumed` from `Resume()`. The system asset stays serialized on the *view* because the
  two home prefabs use different ones (`HomeSystem` / `BO_HomeSystem`); the state reads it via
  `HomeView.System`.

### `NormalGameState` / `TemplateGameState`

Thin `GameState` subclasses. `TemplateGameState` adds nothing. `NormalGameState` adds three public
event-raisers (`GoToLevelCompleteState`, `GoToLevelFailState`, `Reload`) that currently have **no callers**.
Both set `ShowBanner: 1`, so `BannerController` shows the banner on their `OnStateEntered`/`OnStateResumed`.

### `LevelCompleteState` — [../AppFlow/States/LevelComplete/Scripts/LevelCompleteState.cs](../AppFlow/States/LevelComplete/Scripts/LevelCompleteState.cs)

- **Binds**: `ClaimButton`, `ClaimSoloButton`, `Claim2xButton`.
- **Owns**: the entire reward flow — coin grant, `soundService.Play("Coin")`, coin-trail VFX trigger, Adjust +
  Metica `TrackEarnAsset`, rewarded-ad request and its dismissed callback, interstitial after 2.1 s, confetti
  stop, retry-attempt reset, and the routing decision:
  `level >= GameConfig.GoToHomeOnLevelCompleteFromLevel` → `goToHomeEvent.Raise(1)`, else
  `backbuttonPressed` (back to gameplay) + `loadCurrentLevelEvent`.
- Guards every click on `view.IsRewardedCompleted` and blocks raycasts before starting work.

### `LevelFailState` — [../AppFlow/States/LevelFail/Scripts/LevelFailState.cs](../AppFlow/States/LevelFail/Scripts/LevelFailState.cs)

The **reference implementation** of the brain pattern for a complex screen. Instead of binding buttons it
builds a `LevelFailContext` in `OnBindView` and calls `view.StartFlow(ctx)`:

- Resolves the fail reason from `FailService.LastReason` (single source of truth) → `LevelFailReason` enum,
  and looks up the per-reason icon/title/detail from `FailReasonVisualSet`.
- Computes both continue prices (`PriceToContinue`, `PriceToContinueAfterBombFail`, escalating with
  `levelContinuePaidAttempts` past level 20).
- Supplies **eleven callbacks**: coin continue, free play-on, IAP continue, bomb variants, rewarded variants,
  buy-offer analytics, final close, and the view's own click-feedback delegate.

The view then just distributes the context to its panel presenters. Continue paths decrement currency, bump
attempt counters, fire analytics, and close via a delayed `backBtnPressedEvent`.

### `RetryAfterLevelFailState` — [../AppFlow/States/RetryAfterLevelFail/Scripts/RetryAfterLevelFailState.cs](../AppFlow/States/RetryAfterLevelFail/Scripts/RetryAfterLevelFailState.cs)

Entered with `UICloseReasons.UntouchPaused` → the failing gameplay state is fully **exited**, not paused.
Binds Retry + Close. Retry: if lives remain, bump `levelRetryAttempts`, fire `levelFinishedRetryEvent`, try an
interstitial, `replayLevelEvent`, `gotoGameEvent.Raise(6)` (`ResumeAny` → `PopUntil`); otherwise go Home.
Close: Home + interstitial + reset retry attempts.

### `RetryState` — [../AppFlow/States/Retry/Scripts/RetryState.cs](../AppFlow/States/Retry/Scripts/RetryState.cs)

The home-screen replay path. Binds Retry + Close. The state owns the LiveOps interception: it builds a
`LiveOpsContext`, asks `view.HasInterceptingLiveOpsPanel(ctx)`, and on the first press either runs
`view.StartLiveOps(ctx)` (setting `_liveOpsConsumed`) or retries for real. `Retry()` requires
`currentLives > 0`, else `goToLivesRefillEvent`. Both button bindings are null-safe — `Retry.prefab` ships
without a close button.

### `LeaveState` — [../AppFlow/States/Leave/Scripts/LeaveState.cs](../AppFlow/States/Leave/Scripts/LeaveState.cs)

Quit-confirmation, entered with `UntouchPaused`. Binds Leave + Close, and — like `RetryState` — owns the
LiveOps interception decision (skipped entirely while `dailyChallengeActivated`), plus the streak-footer
context it hands to `view.ShowLiveOpsFooters(...)` on open.
`Leave()` clears `levelInProgress`, fires `levelFinishedExitEvent`, then branches:
daily-challenge active → `goToRetryAfterChallengeFailEvent`; otherwise straight Home + a `Giveup`
interstitial (the level's energy was already spent on the first move and is only refunded on a win, so
leaving forfeits it). Close → `gotoGameEvent.Raise(6)` (`PopUntil` back to gameplay).

### `GiveUpState` — [../AppFlow/States/GiveUp/Scripts/GiveUpState.cs](../AppFlow/States/GiveUp/Scripts/GiveUpState.cs)

An ordinary pooled `UIViewState`. Binds GiveUp + Close (both null-safe — `GiveUp.prefab` ships without a
close button). GiveUp clears `levelInProgress` and raises `goToRetryAfterLevelFailEvent`; Close raises
`backBtnPressed`.

It used to be a plain `State` driven by hand from `GameFlow` (`InjectView` + `StartCoroutine(Enter(null))` +
a self-`Exit()`), which meant it was never `CurrentState`, never on the paused stack, unreachable by the back
button, and its `ShowBanner: 1` never took effect. **Note:** nothing currently raises `e_GoToGiveUp`, so this
state is wired but unreached — see the compliance report (L4).

### `SettingsState` — [../AppFlow/States/Settings/Scripts/SettingsState.cs](../AppFlow/States/Settings/Scripts/SettingsState.cs)

In-game settings. Binds Support, RestorePurchases, PrivacyPolicy, Close, Home, Discord. Owns the
`Application.OpenURL` calls for all three links (support, privacy, Discord), and the Home branch: if `firstMoved` is false, go straight Home +
`levelFinishedExitEvent` + a `GiveUp` interstitial; otherwise open `LeaveState`. Close →
`gotoGameEvent.Raise(3)` (`Revive` → `PopOne`). `Exit` sets `homeState.Reason = HomeEntryReason.Exit`.

### `HomeSettingsState` — [../AppFlow/States/HomeSetting/Scripts/HomeSettingsState.cs](../AppFlow/States/HomeSetting/Scripts/HomeSettingsState.cs)

Home-screen settings panel. `usePooling: 0` → loaded from `Resources` by path `HomeSettings` and `Destroy`ed
on exit. Binds Support, RestorePurchases, PrivacyPolicy (no close button on this panel — it is a home section,
closed by navigating Home).

### `ShopState` — [../AppFlow/States/Shop/Scripts/ShopState.cs](../AppFlow/States/Shop/Scripts/ShopState.cs)

Binds `CloseButton` → `PlayCloseFeedback()` + `backButtonPressed`, and `WatchAdButton` → `WatchAdForCoins()`
(rewarded ad → `coinsRewardOnAdWatch` coins + Adjust/Metica earn + coin trail — the same shape as the three
other rewarded surfaces). Also owns two gating decisions: `RefreshWatchAdOffer()` (the button only unlocks
from `GameConfig.RewardedAdAvailableFromLevel`, and its label is driven from the same value it grants) and
`RefreshNoAdsSections()` (subscribed to `removeAds` for as long as the view lives). The IAP product tiles are
still handled by the IAP framework, not this state.

### `LivesRefillState` — [../AppFlow/States/LivesRefill/Scripts/LivesRefillState.cs](../AppFlow/States/LivesRefill/Scripts/LivesRefillState.cs)

Out-of-energy popup. Binds Refill, WatchAd, Close, each gated on the state's own click debounce
(`TryConsumeActionClick`, `actionClickLockSeconds`, reset on every open). `Exit` and `ExitImmediate` both
raise `HomeView.ResumeHomeAction` so the home Play button is re-enabled on every teardown path.
- `Refill()`: flat `GameConfig.OutOfLivesBuyPrice` for `OutOfLivesBuyAmount` lives (remote-overridable), coin
  decrement, life-trail VFX, `LivesSystemMono.AddLivesAction`, sound, analytics + Adjust/Metica spend, then a
  2 s delayed close. Insufficient coins → `goToShopEvent`.
- `WatchAd()`: rewarded ad → `OutOfLivesAdReward` lives + trail + 1.25 s delayed close.

### `BuyBoosterState` — [../AppFlow/States/BoosterBuy/Scripts/BuyBoosterState.cs](../AppFlow/States/BoosterBuy/Scripts/BuyBoosterState.cs)

Receives the target booster via `SetPendingBooster(booster, definition)` **before** the state is opened, then
`Enter` calls `view.Setup(...)`. Binds Close, Buy, Ad.
- `BuyBooster()`: spend `definition.buyPrice` → `AddCharge(definition.buyQuantity)`, refresh UI counters,
  analytics + Adjust/Metica spend & earn, close. Insufficient coins → `goToShopEvent`.
- `WatchAd()`: rewarded ad grants `max(1, definition.rewardedQuantity)` so the reward always matches the
  advertised "Get x N".

### `BoosterUnlockedState` — [../AppFlow/States/BoosterUnlocked/Scripts/BoosterUnlockedState.cs](../AppFlow/States/BoosterUnlocked/Scripts/BoosterUnlockedState.cs)

`Enter` resolves the just-unlocked `BoosterDefinitionSO` from `BoosterID` against `boosterDatabase`, pushes it
to the view (`UpdateUI`, `StartOnboarding`), and bails out gracefully (`GoBack()`) when no database is wired —
the kit must run game-less. Claim → `view.PlayClaimExit(ClaimBooster)` → `view.ExecuteOnboarding(..., cb)` →
`boosterInfo.GrantUnlockReward()` → `backButtonPressed`.

### `FeatureUnlockedState` — [../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedState.cs](../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedState.cs)

Binds `ClaimButton` → `view.PlayClaimExit(GoBack)` → `backBtnPressed`, and owns
`ApplyUnlockedFeature()`: it resolves **which** feature the popup is announcing from the current level against
`GameConfig.UnlockTimeBomb/IceBlock/LockAndKeyFeatureOnLevel` plus the three per-feature onboarding `DBBool`s,
tells the view via `ShowFeature(UnlockedFeature)`, and commits that feature's flag. Two behavioural quirks are
preserved from the original view-side implementation — the flag is committed on open rather than on claim, and
when no threshold matches the contents are left untouched. See the compliance report (V3).

### `RemoveAdsState` — [../AppFlow/States/RemoveAds/Scripts/RemoveAdsState.cs](../AppFlow/States/RemoveAds/Scripts/RemoveAdsState.cs)

The exemplar for **subscription scoping** on a `ScriptableObject` state:

```
Enter  → purchaseSuccess.Subscribe(CloseState)
Pause  → purchaseSuccess.UnSubscribe(CloseState)      ← critical: without this, a paused RemoveAds fired a
Resume → purchaseSuccess.Subscribe(CloseState)          second concurrent PopPausedState when the IAP
Exit   → purchaseSuccess.UnSubscribe(CloseState)        success state closed
```

`Resume` additionally re-checks `removeAds` (the purchase may have completed while paused) and defers the
close by one frame so the FSM finishes its current `PopPausedState` first. The state also exposes the
onboarding/expiry **decisions** (`ShouldShowOnboarding`, `ShouldPlayExpirationFlow`, `MarkOnboardingShown`,
`IsViewOpen`, `IsViewClosed`) which `RemoveAdsVisibility` drives — brain-side logic consumed by an external
sequencer.

### `IAPPurchasePendingState` / `IAPPurchaseSuccessState` / `IAPPurchaseFailedState`

Driven by `GameFlow.GoToPurchaseView(SuccessInfo)` off `UnityIAPNetwork.OnPurchaseStateEvent`:
`Initiated` → pending (`FullScreenPlacement`, so it stacks); then after `purchaseResultDelay`, success →
`InjectSucccessInfo` + `purchaseSuccessTransition`, failure → `purchaseFailedTransition` (both
`UntouchPaused`). Pending sets `isIAPPending` true on enter; success/failed clear it on exit and success also
raises the IAP coin-trail event. Success and Failed bind their view's `CloseButton`/`ContinueButton` to
`CloseBtnClicked()` in `OnBindView`; Pending has no interactive controls at all.

---

## 5. Known deviations at a glance

These are documented in detail in [STATES_MVP_COMPLIANCE_REPORT.md](STATES_MVP_COMPLIANCE_REPORT.md); listed
here so this document isn't misleading on its own.

**Still open:**

| Deviation | Where |
|---|---|
| Small decision leaks in otherwise-passive views | `LevelCompleteView` (claim-button threshold, thumbnail lookup), `LevelFailView` (IAP product match) |
| A non-state view holds a `State` reference | `BoosterButtonView` → `BuyBoosterState` (HUD widget, not a `UIBase` view, so no owning state) |
| Rewarded-ad + no-ads logic in a view with no owning state | `HomeShopView` |

**Fixed** (2026-07-29 — layering only, bar one 200 ms hide-timing change noted in the report):

| Deviation | Where | Fix |
|---|---|---|
| View holds a serialized `State` reference and calls into it | the three IAP views | views expose buttons; states bind in `OnBindView` |
| Business logic lives in the view, not the state | `ShopView`, `FeatureUnlockedView` | moved into `ShopState` / `FeatureUnlockedState` |
| Flow decisions built in the view instead of a state-built context | `RetryView`, `LeaveView` | context + intercept decision moved into `RetryState` / `LeaveState` |
| A second controller SO owned a screen's lifecycle instead of the state | `HomeSystemSO` / `HomeState` | `HomeState` now drives `OnViewEnabled/Shown/Resumed/Disabled` |
| State entered/exited outside the FSM | `GiveUpState` | promoted to `UIViewState` + `GiveUpTransition`; `InjectView` and the manual `Enter`/`Exit` removed |
| State calls its own `base.Exit()` mid-flow | `LevelFailState` (bomb-continue paths) | removed; the FSM pop via `HideAfterContinue()` is the teardown |
| Decision leaks in otherwise-passive views | `SettingsView` (Discord URL), `LivesRefillView` (click-gate timing + cross-screen hook) | moved into `SettingsState` / `LivesRefillState` |

---

## 6. Adding a new state

See [STATE_MACHINE.md § Adding a new state](STATE_MACHINE.md) for the full asset checklist. The state-side
essentials:

1. Subclass `UIViewState` (screen/popup), `GameState` (gameplay + HUD), or `State` (headless).
   Add `[CreateAssetMenu(menuName = "GameStates/…")]`.
2. Give the view a `public Button XButton => xBtn;` property per interactive control, plus dumb
   `SetX(...)` / `PlayXFeedback()` methods. **No `State` field on the view.**
3. Override `OnBindView` to cast the view, `AddListener` your handlers, and push initial data;
   override `OnUnbindView` to `RemoveListener` exactly what you added and null your view field.
4. Put every decision, currency/ad/analytics call and persistent-flag write in the state. If there are more
   than a handful, build a context struct of data + callbacks and hand it to the view
   (`LevelFailState.BuildContext()`).
5. Subscribe in `Enter`; unsubscribe in **both** `Pause` and `Exit`; re-subscribe in `Resume`.
6. Make `Enter` idempotent (release anything you might still own) and keep post-yield teardown yield-free.
7. Override `ExitImmediate` if the state owns spawned objects — no yields, no animation.
8. Navigate by raising `GameEvent`s only; never call `Enter`/`Exit`/`Pause`/`Resume` yourself and never touch
   `v_CurrentStateSortingOrder`.
