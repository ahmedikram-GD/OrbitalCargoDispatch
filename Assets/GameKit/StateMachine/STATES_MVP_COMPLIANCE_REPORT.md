# State-is-the-Brain — Compliance Report

**Audited rule**

> Each state is the brain for its view and is responsible for holding all functionality. The view is passive
> and only asks the state what to do. The view never references the state.

**Scope** — every class deriving from `State`, `UIViewState` or `GameState` in the project (21 states), plus
each state's paired view. Companion documents: [STATES.md](STATES.md), [STATE_MACHINE.md](STATE_MACHINE.md).

**Audited on** 2026-07-29, branch `Shahbaz/LiveOps-Improvements`.
**V1–V4 fixed on** 2026-07-29 — see [§ Fix log](#fix-log).
**L1–L4 + A1 fixed on** 2026-07-29 — see [§ Fix log — leakage pass](#fix-log--leakage-pass).
The findings are kept below for history; each carries its current status.

---

## Verdict

**Original audit: 7 of 21 states violated the rule outright and 5 more leaked. All 12 have since been
fixed; every state now satisfies the state-is-the-brain rule and the state-machine rules.**

| Verdict | Was | Now |
|---|:--:|:--:|
| ✅ **Compliant** | 9 | **21** |
| 🟡 **Compliant with leakage** | 5 | **0** |
| 🔴 **Violated** | 7 | **0** |

Verified in-editor after the fixes:

- **0** of 28 `UIBase` view classes hold a reference to a `State` (reflection sweep over all loaded
  assemblies — note `State` lives in `GameKit.Runtime`, not `Assembly-CSharp`).
- **17 of 18** `UIViewState` subclasses implement `OnBindView`; the exception is
  `IAPPurchasePendingState`, whose view has no interactive controls.
- **0** live calls to `Enter`/`Exit`/`Pause`/`Resume` on a state from outside the FSM, and **0** remaining
  `InjectView` methods.

The **mechanism** was always right: `UIViewState` bakes the contract into the base class
(`OnBindView` / `OnUnbindView`, [StateMachine/Scripts/UiViewState.cs:142-151](StateMachine/Scripts/UiViewState.cs)),
and `LevelFailState`'s context pattern shows the rule scales to a complex screen. The fixes reuse those two
existing patterns rather than inventing new ones.

Still open are the smaller items **A2, A3, A5** (minor decision leaks in otherwise-passive views, and one
null guard) plus the dead-code sweep — see [§ Still open](#still-open).

---

## 🔴 Violations (all fixed)

### V1 — The three IAP views hold a serialized `State` reference and call into it

**Status: ✅ FIXED.**

**Severity: high — this is the exact inverse of the rule.**

| File | Evidence |
|---|---|
| [../../SDKsEssentials/InApps/Scripts/Views/PurchaseSucessView.cs:11](../../SDKsEssentials/InApps/Scripts/Views/PurchaseSucessView.cs) | `[SerializeField] private IAPPurchaseSuccessState purchaseState;` → `purchaseState.CloseBtnClicked()` (lines 47, 51) |
| [../../SDKsEssentials/InApps/Scripts/Views/PurchaseFailedView.cs:11](../../SDKsEssentials/InApps/Scripts/Views/PurchaseFailedView.cs) | `[SerializeField] private IAPPurchaseFailedState purchaseState;` → `purchaseState.CloseBtnClicked()` (lines 48, 52) |
| [../../SDKsEssentials/InApps/Scripts/Views/PurchasePendingView.cs:9](../../SDKsEssentials/InApps/Scripts/Views/PurchasePendingView.cs) | `[SerializeField] private IAPPurchasePendingState retryState;` — never used; a dead inverted dependency |

None of the three states implements `OnBindView`/`OnUnbindView`. The view subscribes its own buttons in
`OnEnable` and pushes the call *up* into the state.

Why it matters beyond style: the prefab now carries a hard reference to a `ScriptableObject` state asset, so
the view cannot be reused, cannot be tested without the state, and — because states are shared singletons —
two simultaneously-live prefab instances would both drive the same state.

**Two secondary defects found in the same files:**

1. **Class/file name mismatch.** `PurchaseFailedView.cs` declares `public class PurchaseSuccessView`, while
   `PurchaseSucessView.cs` declares `public class PurchaseSucessView`. Two near-identical type names, neither
   matching its file. `IAPPurchaseFailedState.asset` has `stateId: PurchaseFailedView`, so the *prefab* named
   `PurchaseFailedView` carries the `PurchaseSuccessView` component. Renaming either class without care will
   silently break the wrong prefab.
2. `PurchaseSuccessView` (the failed one) logs `Debug.LogError` on every open/close — error-level logging on a
   normal path.

**Fix**

```csharp
// PurchaseSucessView.cs — view side
public Button CloseButton    => closeButton;
public Button ContinueButton => continueBtn;
// delete the purchaseState field and OnEnable/OnDisable wiring

// IAPPurchaseSuccessState.cs — state side
protected override void OnBindView(UIBase v)
{
    _view = (PurchaseSucessView)v;
    _view.CloseButton.onClick.AddListener(CloseBtnClicked);
    _view.ContinueButton.onClick.AddListener(CloseBtnClicked);
}
protected override void OnUnbindView(UIBase v) { /* mirror removals */ }
```

Then clear the now-orphaned reference on the three prefabs, delete the unused field in `PurchasePendingView`,
and rename the two view classes to match their files.

---

### V2 — `ShopView` owns the rewarded-coin economy; `ShopState` owns nothing but Close

**Status: ✅ FIXED.**

**Severity: high — business logic (ads + currency + analytics) sits in a view.**

[../AppFlow/States/Shop/Scripts/ShopState.cs](../AppFlow/States/Shop/Scripts/ShopState.cs) binds a single
button:

```csharp
protected override void OnBindView(UIBase v)
{
    _view = (ShopView)v;
    _view.CloseButton.onClick.AddListener(OnCloseClicked);
}
```

Meanwhile [../AppFlow/States/Shop/Scripts/ShopView.cs:112-139](../AppFlow/States/Shop/Scripts/ShopView.cs)
runs the whole rewarded flow itself:

```csharp
private void OnAdWatchClick()
{
    analyticsSystem.LogAdClicked("Shop");
    adsService.ShowRewarded("Shop", (rewarded) => {
        if (rewarded) {
            adjustAnalytics.TrackEarnAsset("Coin", "Shop", mazeGameConfig.coinsRewardOnAdWatch, "Rewarded");
            softCurrency.Increment(mazeGameConfig.coinsRewardOnAdWatch);
            meticaAnalyticsSystem?.TrackEarnAsset(...);
            analyticsSystem.LogAdWatched("SHOP");
            UICollector.PlayTrailAction?.Invoke("CoinPrefab", watchAdSourceIcon, watchAdTargetIcon, 10);
        }
    });
}
```

Plus two more decisions in the view: the availability gate
`levelNoForText >= mazeGameConfig.RewardedAdAvailableFromLevel` (`OnEnable`, lines 74-83) and
`CheckRemoveAds()` (lines 99-110), which decides whether the no-ads sections show based on `removeAds` +
`adsRemoteSettings.ShowInterstitialFromLevel`. The code comment even concedes it:
*"AdWatchBtn stays view-internal (rewarded-ad flow, never touches the state)."*

Note this is exactly the flow that **is** correctly state-owned everywhere else —
`LevelCompleteState.WatchAdToClaimBonus`, `LivesRefillState.WatchAd`, `BuyBoosterState.WatchAd`. The shop is
the odd one out.

**Fix**: expose `public Button WatchAdButton => AdWatchBtn;`, `SetAdRewardText(int)`,
`SetNoAdsSectionsVisible(bool)` on the view; move `OnAdWatchClick`, the level gate and `CheckRemoveAds` into
`ShopState`, subscribing `removeAds.GameEvent` in `Enter` and unsubscribing in `Pause`/`Exit` (mirror
`RemoveAdsState`).

---

### V3 — `FeatureUnlockedView` decides which feature unlocked **and writes the persistent flags**

**Status: ✅ FIXED (layering only).** The decision now lives in `FeatureUnlockedState.ApplyUnlockedFeature()`.
The two *behavioural* quirks called out below — the flag being committed before the player claims, and the
empty popup when no threshold matches — were **deliberately preserved**, because fixing them changes runtime
behaviour. They remain open; see [§ Still open](#still-open-behavioural-follow-ups).

**Severity: high — the state is an empty shell; the view mutates save data.**

[../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedState.cs](../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedState.cs)
contains one binding and `GoBack()`. The entire feature-unlock decision is in
[../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedView.cs:51-85](../AppFlow/States/FeatureUnlocked/Scripts/FeatureUnlockedView.cs):

```csharp
private void OnEnable()
{
    claimBtn.interactable = true;
    soundService.Play("UnlockFX");
    if (levelNoForText.GetValue() == mazeGameConfig.UnlockTimeBombFeatureOnLevel && !timebombOnboardingCompleted.GetValue())
    {
        timeBombContent.SetActive(true); iceBlockContent.SetActive(false); lockAndKeyOnboarding.SetActive(false);
        soundService.Play("BoosterUnlocked");
        timebombOnboardingCompleted.SetValue(true);      // ← persistent write, from a view, in OnEnable
    }
    else if (levelNoForText.GetValue() == mazeGameConfig.UnlockIceBlockFeatureOnLevel && !iceBlockOnboardingCompleted.GetValue())
    { … iceBlockOnboardingCompleted.SetValue(true); }
    else if (levelNoForText.GetValue() == mazeGameConfig.UnlockLockAndKeyFeatureOnLevel && !lockAndKeyOnboardingCompleted.GetValue())
    { … lockAndKeyOnboardingCompleted.SetValue(true); }
}
```

Three real problems, not just layering:

- The `DBBool` "onboarding completed" flags are committed in `OnEnable` — **before the player has claimed
  anything**. Any path that shows this view and then aborts still burns the unlock.
- Because the panel is pooled, `OnEnable` fires on every re-show; the `== level` equality means an off-by-one
  level or a re-show at a different level silently leaves **all three** contents inactive and shows an empty
  popup.
- Adding a fourth unlockable feature means editing a view and adding two more serialized fields, rather than
  extending a data-driven decision in the state.

**Fix**: move the decision into `FeatureUnlockedState.Enter` (ideally table-driven — a small
`FeatureUnlockDefinitionSO` list of `{ level, flag, contentId, sound }`), have it push the result via
`view.ShowFeature(definition)`, and only write the completed flag in the claim callback, next to `GoBack()` —
mirroring how `BoosterUnlockedState` grants its reward.

---

### V4 — `RetryView` / `LeaveView` build the LiveOps context and decide whether to intercept

**Status: ✅ FIXED.**

**Severity: medium — a flow decision in the view, inconsistent with `LevelFailState`.**

[../AppFlow/States/Retry/Scripts/RetryView.cs:54-77](../AppFlow/States/Retry/Scripts/RetryView.cs) and
[../AppFlow/States/Leave/Scripts/LeaveView.cs:58-82](../AppFlow/States/Leave/Scripts/LeaveView.cs) are
near-identical:

```csharp
public void PlayRetryFlow(System.Action onRetry)
{
    if (!_liveOpsConsumed && liveOpsFlow != null)
    {
        var ctx = new LiveOpsContext {
            LevelNo         = levelNo != null ? levelNo.GetValue() : 0,
            StreakContinued = streakContinued != null && streakContinued.GetValue(),
            OnAccept        = OnLiveOpsFinished
        };
        if (liveOpsFlow.HasAnyValidPanel(ctx)) { _liveOpsConsumed = true; StartLiveOps(ctx); return; }
    }
    liveOpsFlow?.ForceHideAll();
    onRetry?.Invoke();
}
```

The view reads model data (`levelNo`, `streakContinued`, `dailyChallengeActivated`), constructs the context,
and decides *"does the retry/leave happen at all, or does a LiveOps offer intercept it?"* — a functional
decision, not presentation. `LeaveView.Show` (lines 122-133) builds a second context to show footers.

This is the same problem `LevelFailState` already solved correctly: there, the **state** builds
`LevelFailContext` and the view only distributes it. The two patterns coexist in the same codebase for the
same kind of screen.

**Fix**: build `LiveOpsContext` in `RetryState`/`LeaveState`, expose `bool HasInterceptingPanel(ctx)` and
`void StartLiveOps(ctx)` on the view (presentation only), and keep the "intercept or proceed" branch in the
state. `_liveOpsConsumed` should live on the state too — it is flow state, and on a pooled view it currently
resets on every `OnEnable`.

---

## 🟡 Compliant with leakage (all fixed)

### L1 — `HomeState` shares its brain with `HomeSystemSO`

**Status: ✅ FIXED (lifecycle ownership).** `HomeState` now drives the system; `HomeSystemSO` is retained
deliberately — see the fix log for why folding it in was rejected.

[../AppFlow/States/HomeScreen/Scripts/HomeState.cs](../AppFlow/States/HomeScreen/Scripts/HomeState.cs) binds
Play + Settings and owns the energy gate. Everything else the home screen does — play-button difficulty and
level text, section navigation, illustration source, home lock, the delayed home-landing signal,
magic-streak visibility, and routing of ten inbound `GameEvent`s — lives in
[../AppFlow/States/HomeScreen/Scripts/HomeSystemSO.cs](../AppFlow/States/HomeScreen/Scripts/HomeSystemSO.cs),
which `HomeView` binds itself to in `OnEnable`.

This is a **deliberate, documented MVC split**, and it is genuinely better than logic-in-the-view: the view
stays passive and drives everything through `IHomeView`. But measured against *this* rule it means the home
screen has **two brains**, and the view's `OnEnable`→`homeSystem.OnViewEnabled(this)` back-reference means the
view is the one initiating (an SO can't hold a scene ref, hence the runtime injection). Practical consequences:

- `HomeSystemSO`'s lifecycle is tied to the *view's* enable/disable, not the state's `Enter`/`Exit`/`Pause`/
  `Resume` — so the home screen alone doesn't participate in the pause-scoped subscription discipline the rest
  of the states follow (rule 4 in [STATE_MACHINE.md](STATE_MACHINE.md)).
- A reader looking for "what does the home Play button do" finds it in the state; "what does the home Shop
  button do" is in the SO. Two places to look.

**Recommendation**: keep the split if you value the reusable-per-home-prefab wiring (`HomeSystem.asset` vs
`BO_HomeSystem.asset` genuinely need it), but make `HomeState` own the `HomeSystemSO` lifecycle — call
`OnViewEnabled/Disabled` from `HomeState.OnBindView/OnUnbindView` and `OnViewShown/Resumed` from
`Enter`/`Resume` — so the state stays the single entry point and subscriptions become pause-scoped.
Alternatively fold `HomeSystemSO` into `HomeState`, since `HomeState` *is* already a `ScriptableObject`.

### L2 — `SettingsView` / `HomeSettingsView` keep one URL button

**Status: ✅ FIXED**, except `HomeSettingsView.Hide()` — see the fix log.

`SettingsState` correctly owns `OpenPrivacyPolicy()` and `ProvideSupport()`, but
[../AppFlow/States/Settings/Scripts/SettingsView.cs:95](../AppFlow/States/Settings/Scripts/SettingsView.cs)
keeps `VisitDiscordLink()` → `Application.OpenURL(mazeGameConfig.DiscordLink)`, self-wired in `OnEnable`. Three
sibling links, two in the state and one in the view. Also: `SettingsView` has an unused `[SerializeField]
HomeView homeView` — a view-to-view reference that should go.

`HomeSettingsView` has the same `VisitDiscordLink()` method but **nothing calls it** (the `discordBtn` field is
commented out) — dead code.

Separately, `HomeSettingsView.Hide()` (line 105) is `yield break` with no `SetActive(false)` — the panel is
never actually hidden by `Exit`; it only disappears because the state is `usePooling: 0` and the object is
`Destroy`ed. Fragile if pooling is ever turned on.

**Fix**: expose `DiscordButton` and move the `OpenURL` into the state; delete the dead method and the
`homeView` field; give `HomeSettingsView.Hide` a real body.

### L3 — `LivesRefillView` holds the click-gate timing and a cross-view side effect

**Status: ✅ FIXED.**

[../AppFlow/States/LivesRefill/Scripts/LivesRefillView.cs:111](../AppFlow/States/LivesRefill/Scripts/LivesRefillView.cs)
owns `TryConsumeActionClick()` / `btnLock` / the 0.5 s `Invoke(nameof(UnlockButtonClick))`. The state politely
asks the view for permission (`if (!view.TryConsumeActionClick()) return;`) — inverted: debounce policy is the
brain's, and the state cannot reason about it. Also `OnDisable` fires
`HomeView.ResumeHomeAction?.Invoke()` — a view reaching across to another screen's static hook.

Timer/lives text subscriptions in `OnEnable`/`OnDisable` and config-driven label refresh are legitimately
view-side (pure display), so those are fine.

### L4 — `GiveUpState` is driven outside the FSM

**Status: ✅ FIXED** — promoted to a real `UIViewState` and wired through a transition.

Binding-wise `GiveUpState` is textbook: it wires `GiveUpButton`/`CloseButton` itself and the view holds no
state reference. The violation is machinery, not MVP:

```csharp
// GameFlow.cs:200
private void Start()          { giveUpState.InjectView(giveUpView); }
// GameFlow.cs:205
private void GoToGiveUp()     { StartCoroutine(giveUpState.Enter(null)); }
// GiveUpState.cs:57
public void CloseBtnClicked() { CoroutineHandler.StartStaticCoroutine(Exit()); }
```

It is entered with `Listener = null`, is never `CurrentState`, is never pushed on the paused stack, and exits
itself. Consequences: its `ShowBanner: 1` never takes effect (`BannerController` only listens to FSM events);
the back button cannot close it; and it can be shown on top of a state that then exits underneath it.

**Fix**: convert to a `UIViewState` with a `stateId` + pool entry, a `GiveUpTransition`, and a
`goToGiveUpEvent` registered in `RegisterFlowEvents` with `UICloseReasons.FullScreenPlacement`. Remove
`InjectView` and the manual `Enter`/`Exit` calls.

---

## Additional findings (outside the audited rule, worth fixing)

| # | Finding | Location |
|---|---|---|
| A1 | `LevelFailState` calls `CoroutineHandler.StartStaticCoroutine(base.Exit())` in `ContinueLevelAfterBombFail`, `ContinueBombFreeAfterInApp` and `ShowRewardedAndContinueBomb` — a state driving its own teardown behind the FSM's back, *in addition to* the `backBtnPressedEvent` that `HideAfterContinue()` already raises. Double teardown; the FSM's `CurrentState` is left stale. | [../AppFlow/States/LevelFail/Scripts/LevelFailState.cs:233](../AppFlow/States/LevelFail/Scripts/LevelFailState.cs), `:255`, `:302` |
| A2 | `LevelCompleteView.SetUpClaimButtonsVisibility()` decides which claim button shows from `levelNoForText >= mazeGameConfig.CompleteRewardedLevel`, and `ApplyThumbnails()` does its own `LevelThumbnailLibrarySO` lookup — both are state-side decisions, though the view otherwise stays passive. | [../AppFlow/States/LevelComplete/Scripts/LevelCompleteView.cs:218](../AppFlow/States/LevelComplete/Scripts/LevelCompleteView.cs), `:260` |
| A3 | `LevelFailView.HandlePurchased(string id)` matches `levelFailBundle.ProductId` and picks between `OnContinueIAPBomb` / `OnContinueIAPOutOfEnergies` itself. The context already carries `IsBombFail`, so it's a small leak, but the product-id match belongs in the state. | [../AppFlow/States/LevelFail/Scripts/LevelFailView.cs:112](../AppFlow/States/LevelFail/Scripts/LevelFailView.cs) |
| A4 | `BoosterButtonView` (a HUD widget, not a state's view) holds `[SerializeField] BuyBoosterState buyBoosterState` and calls `SetPendingBooster`. Same inverted shape as V1, though outside the state↔view pairing. Prefer a `GameEventWithParam`/pending-selection SO. | [../Boosters/Core/Scripts/BoosterButtonView.cs:68](../Boosters/Core/Scripts/BoosterButtonView.cs) |
| A5 | `BoosterUnlockedState.Enter` dereferences `view.UpdateUI/StartOnboarding` unguarded. If the pool `Get(stateId)` fails, `base.Enter` `yield break`s early, `OnBindView` never runs and `view` is null → `NullReferenceException`. Add a `if (view == null) { GoBack(); yield break; }`. | [../AppFlow/States/BoosterUnlocked/Scripts/BoosterUnlockedState.cs:37](../AppFlow/States/BoosterUnlocked/Scripts/BoosterUnlockedState.cs) |
| A6 | `NormalGameState.GoToLevelCompleteState()`, `GoToLevelFailState()` and `Reload()` have **no callers** anywhere in the project (no C#, no prefab `UnityEvent`). Dead public API on a state. | [../Architecture/NormalGameState.cs:15](../Architecture/NormalGameState.cs) |
| A7 | Duplicate `SettingsState.asset`: `AppFlow/States/HomeSetting/Scriptables/SettingsState.asset` (`stateId` empty, `usePooling: 0`) is **referenced by nothing** — the wired one is `AppFlow/States/Settings/Scriptables/SettingsState.asset`. An empty `stateId` on the orphan would fail at runtime if anyone wired it. Delete it. | `AppFlow/States/HomeSetting/Scriptables/` |
| A8 | `RemoveAdIcon` has `[SerializeField] RemoveAdsState removeAdsState` and never uses it — dead inverted reference. | [../AppFlow/States/RemoveAds/Scripts/RemoveAdIcon.cs:18](../AppFlow/States/RemoveAds/Scripts/RemoveAdIcon.cs) |
| A9 | Commented-out `InjectView` stubs remain in eight states (`HomeState`, `SettingsState`, `LeaveState`, `RetryState`, `RetryAfterLevelFailState`, `LivesRefillState`, `RemoveAdsState`, `LevelCompleteState`) — leftovers from the pre-`OnBindView` era. They invite reintroducing the anti-pattern. | various |

---

## Fix log

Applied 2026-07-29. **Scope: layering only — no behavioural change.** Every moved block is a straight port:
same conditions, same order, same events, same flags. Where a call moved from a view's `OnEnable`/`Show` into
the state's `OnBindView`, the relative order of the calls was preserved and both still run in the same frame
of the same `Enter()` (`UIViewState.Enter` does `SetActive` → `Show()` → `OnBindView`), and none of the
affected views opt into an enter animation that could observe the difference.

| # | Change | Files |
|---|---|---|
| V1 | Deleted the `purchaseState` / `retryState` fields; views now expose `CloseButton` / `ContinueButton`; the two states bind them in `OnBindView` / `OnUnbindView`. | `PurchaseSucessView.cs`, `PurchaseFailedView.cs`, `PurchasePendingView.cs`, `IAPPurchaseSuccessState.cs`, `IAPPurchaseFailedState.cs` |
| V2 | `WatchAdForCoins()` (ads + coins + Adjust/Metica + trail), `RefreshWatchAdOffer()` (level gate + label) and `RefreshNoAdsSections()` moved to `ShopState`. View exposes `WatchAdButton`, `SetWatchAdVisible/RewardText`, `SetNoAdsSectionsVisible` and the two trail endpoints. | `ShopView.cs`, `ShopState.cs` |
| V3 | The level-threshold/flag decision moved to `FeatureUnlockedState.ApplyUnlockedFeature()`. View exposes `ShowFeature(UnlockedFeature)`. | `FeatureUnlockedView.cs`, `FeatureUnlockedState.cs` |
| V4 | `LiveOpsContext` construction, the `HasAnyValidPanel` intercept decision and the `_liveOpsConsumed` flow flag moved to `RetryState` / `LeaveState`. Views expose `HasInterceptingLiveOpsPanel`, `StartLiveOps`, `ForceHideLiveOps`, `RestoreRetryUI`/`RestoreLeaveUI`, `ShowLiveOpsFooters`. | `RetryView.cs`, `RetryState.cs`, `LeaveView.cs`, `LeaveState.cs` |

**Serialized-reference migration.** Moving a `[SerializeField]` from a prefab to a `ScriptableObject` drops
the wired value, so the 16 affected references were re-assigned on the state assets from the values read off
the prefabs, and each was verified by round-trip. Every affected view component exists on exactly **one**
prefab, so the mapping was 1:1 with no wiring collapsed.

| State asset | Fields added |
|---|---|
| `ShopState.asset` | `softCurrency`, `mazeGameConfig`, `levelNoForText`, `adsService`, `analyticsSystem`, `adjustAnalytics`, `meticaAnalyticsSystem`, `removeAds`, `adsRemoteSettings` |
| `FeatureUnlockedState.asset` | `mazeGameConfig`, `levelNoForText`, `timebombOnboardingCompleted`, `iceBlockOnboardingCompleted`, `lockAndKeyOnboardingCompleted` |
| `RetryState.asset` | `levelNo` |
| `LeaveState.asset` | `levelNo` |

Scene-bound references (`liveOpsFlow`, `detailTextRoot`, buttons, trail icons, `noAdsSections`) stayed on the
views — a `ScriptableObject` cannot hold them — which is why the states reach them through passive view
methods rather than owning them directly.

**Two deliberate non-ports:**
- `ShopView.shopOpened` and the `OnShopViewOpened`/`OnShopViewClosed` statics stayed in the view: they are
  GameObject-lifecycle broadcasts, not decisions, and moving them would change *when* they fire.
- `ShopState`'s `removeAds` subscription is **not** pause-scoped (unlike `RemoveAdsState`'s). The handler only
  toggles visibility and the shop view stays active while the state is paused — matching what the view did.

**One incidental fix in touched code:** `Retry.prefab` ships with `closeBtn` unassigned (`{fileID: 0}` in
`HEAD` — pre-existing), and `RetryState.OnBindView` bound it unguarded, throwing an `NRE` out of `Enter()` and
leaving the FSM's `CurrentState` stale. Both Retry bindings are now null-safe. Assigning the button on the
prefab (or confirming the dialog is meant to have none) is still the real fix.

---

## Fix log — leakage pass

Applied 2026-07-29, after the V1–V4 pass. Same discipline: straight ports, order preserved.

| # | Change | Files |
|---|---|---|
| L1 | `HomeState` now drives `HomeSystemSO`'s lifecycle — `OnViewEnabled` + `OnViewShown` from `OnBindView` (in that order), `OnViewDisabled` from `OnUnbindView`, `OnViewResumed` from a new `Resume()` override. The view no longer initiates any of it. | `HomeView.cs`, `HomeState.cs` |
| L2 | Discord link moved to `SettingsState.OpenDiscord()` (bound via a new `DiscordButton`); deleted the dead `gameStateActive`, `homeView`, `mazeGameConfig`, `OnLeavePanelExit` from `SettingsView`, and the dead `VisitDiscordLink` + `mazeGameConfig` from `HomeSettingsView`. | `SettingsView.cs`, `SettingsState.cs`, `HomeSettingsView.cs` |
| L3 | Click debounce moved to `LivesRefillState` (`_actionLocked` + `actionClickLockSeconds`, default 0.5s); `HomeView.ResumeHomeAction` now raised by the state from `Exit()` **and** a new `ExitImmediate()` override; deleted the view's `btnLock`, `TryConsumeActionClick`, `IsActionUnlocked`, `UnlockButtonClick`, and the dead `LockInteraction`. | `LivesRefillView.cs`, `LivesRefillState.cs` |
| L4 | `GiveUpState` promoted from `State` to `UIViewState`; `InjectView`, the manual `StartCoroutine(giveUpState.Enter(null))` and the self-`Exit()` all removed. It now closes via `backBtnPressed` and is entered by a normal transition. | `GiveUpState.cs`, `GiveUpView.cs`, `GameFlow.cs` |
| A1 | Removed the three `CoroutineHandler.StartStaticCoroutine(base.Exit())` calls from `LevelFailState`'s bomb-continue paths. | `LevelFailState.cs` |

**New/changed assets for L4.** `GiveUpState` was previously driven by hand, so it had none of the
`UIViewState` plumbing. Added and verified end-to-end
(`GiveUpTransition → GiveUpState → stateId "GiveUp" → pool entry → GiveUp.prefab → GiveUpView : UIBase`):

| Asset | Change |
|---|---|
| `GiveUpState.asset` | `stateId = "GiveUp"`, `usePooling = true`, `uIStatesPooler = MazeGamePooler`, `backBtnPressed = e_BackBtnPressed`. `ShowBanner` stays `1` — and now actually takes effect, since the state finally goes through the FSM. |
| `MazeGamePooler.asset` | new pool entry `GiveUp` → `GiveUp.prefab` (capacity 1, max 1, no prewarm) |
| `GiveUpTransition.asset` | **created**, `ToState = GiveUpState`, `closePolicy = Default` |
| `GameFlow.prefab` | `giveUpTransition` wired; the obsolete `giveUpView` field is gone |

`LivesRefillState.actionClickLockSeconds` was explicitly verified as `0.5` after import — a newly added
serialized field can deserialize to `0` on an existing asset, which would have silently removed the debounce.

### Deliberate judgement calls

- **`HomeSystemSO` was kept, not folded into `HomeState`.** The two home prefabs
  (`HomeScreen` / `BO_HomeScreen`) reference **different** system assets (`HomeSystem` / `BO_HomeSystem`), and a
  `ScriptableObject` state cannot hold that per-prefab choice. The state reads it back via `HomeView.System`,
  so the state owns the *lifecycle* while the prefab keeps the *wiring*. This is option A from L1's original
  recommendation.
- **Subscriptions were NOT made pause-scoped.** L1's recommendation also suggested that, but Home's inbound
  events currently keep routing while Home sits paused behind another screen; unsubscribing on pause would
  change behaviour. Left as-is.
- **`HomeSettingsView.Hide()` still does not deactivate.** The same view type is also embedded in
  `BO_HomeScreen.prefab` as a home section that no state drives, where adding `SetActive(false)` would fire
  `OnDisable` across that hierarchy. It is harmless today (`usePooling = 0`, so `Exit` destroys the instance
  immediately after) and is now documented in the file.
- **Two latent NREs fixed in touched code:** `GiveUpView.OnEnable` dereferenced its unassigned `closeBtn`
  (it would have thrown the first time the state was ever shown), and `GiveUpState`'s bindings are null-safe
  for the same reason — the same pre-existing prefab gap already found on `Retry.prefab`.

### The one behavioural difference

Removing A1's manual `base.Exit()` means the fail panel now hides when the FSM pops it (via
`HideAfterContinue()`'s 0.2 s `backBtnPressedEvent`) instead of ~200 ms earlier. Stack handling is
identical — the old manual call nulled `_uiInstance`, so the FSM's subsequent `Exit()` was already a no-op —
and this makes the bomb-continue paths behave exactly like the out-of-moves path, which never had the extra
call. Revert those three edits if the faster hide was intentional.

---

## Still open

- **V3 quirks** — the onboarding flag is committed when the popup opens rather than on claim, and no
  threshold matching leaves all three contents inactive (empty popup). Preserved as-is.
- **A2, A3, A5** — `LevelCompleteView`'s claim-button threshold + thumbnail lookup, `LevelFailView`'s IAP
  product match, and the `BoosterUnlockedState` null guard.
- **A4** — `BoosterButtonView` holds a `BuyBoosterState` reference. Left alone: it is a gameplay HUD widget,
  not a state's paired view (it does not derive from `UIBase`), so it has no `OnBindView` to move into; it
  needs a pending-selection SO or a `GameEventWithParam` instead.
- **Dead-code sweep** — A6, A7, A8, A9, and the `PurchaseSuccessView` / `PurchaseSucessView` class-vs-file
  renames (deferred: renaming re-binds the prefab script reference).
- **`HomeShopView`** — the home-screen shop panel carries its own copies of the rewarded-ad and
  no-ads-section logic. It is a `UIBase` with **no owning state**, so there is nowhere to move that logic
  without first giving it one.
- **`GiveUpState` is wired but unreached.** Nothing raises `e_GoToGiveUp` (verified across C#, prefab
  `UnityEvent`s and scenes), and `GameFlow.giveUpView` was `NULL`, so the popup could never appear before this
  change either — `Enter` bailed on `view == null`. It is now correct *if* something raises the event; whether
  the give-up flow should be reachable at all is a product decision.

---

## Compliance checklist for review

Use this on any new or modified state:

- [ ] The view has **no** `[SerializeField]` field typed as a `State` subclass, and calls no state method.
- [ ] The state implements `OnBindView` **and** `OnUnbindView`, and the removals mirror the additions exactly.
- [ ] Every currency change, ad request, analytics call, IAP interaction and `DB*` write is in the state.
- [ ] Every "which panel / which variant / should this happen at all" decision is in the state.
- [ ] The view exposes only `Button` properties, dumb setters, `Show`/`Hide` overrides, tweens, and
      `PlayXFeedback()` methods invoked *by* the state.
- [ ] More than ~4 callbacks? Use a context struct built by the state (`LevelFailState.BuildContext()`).
- [ ] Subscriptions: `Enter` subscribes, `Pause` **and** `Exit` unsubscribe, `Resume` re-subscribes.
- [ ] `Enter` is idempotent; post-yield teardown is yield-free; `ExitImmediate` is overridden if the state
      owns spawned objects.
- [ ] Navigation is by `GameEvent` only — no direct `Enter`/`Exit`/`Pause`/`Resume`, no
      `v_CurrentStateSortingOrder` writes.
- [ ] No flow flags (`_liveOpsConsumed`-style) stored on a pooled view.
