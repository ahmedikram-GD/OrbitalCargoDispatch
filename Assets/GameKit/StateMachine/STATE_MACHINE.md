# GameKit StateMachine — Developer Guide

`namespace Blues.Core.StateMachine` · `Blues.Core.UI` · `Blues.Core.Application` (+ a few global-namespace
helpers) · root `Assets/GameKit/StateMachine/`

**One-line model:** a single `ScriptableObject` FSM owns one *current* state plus a *stack of paused* states;
transitions are assets, states are assets, and every screen in the game is entered by raising a `GameEvent`
that a flow-controller `MonoBehaviour` maps to a `Transition`.

---

## Mental model

There is exactly one live FSM in the game — the [`FiniteStateMachine.asset`](GameFlow/Objects/FiniteStateMachine.asset)
`ScriptableObject`. It holds `CurrentState` and a `Stack<State> PausedStates`; a state is either *current*,
*paused underneath the current one*, or *not in the machine at all*. Nothing in gameplay calls the FSM
directly: gameplay raises a `GameEvent`, a `BaseApplicationFlowController` subclass ([`GameFlow`](../AppFlow/GameFlow/Scripts/GameFlow.cs))
translates that event + a `UICloseReasons` into a `Transition` + a `ClosePolicy`, and only then does the FSM
move.

States are `ScriptableObject`s, so **there is one shared instance per asset for the whole app lifetime** —
they have no `OnEnable`/`OnDisable` scoping, no per-instance fields, and they survive scene loads. Almost every
rule below follows from that single fact.

---

## Key types

| Type | File | Role |
|---|---|---|
| `FiniteStateMachine` | [StateMachine/Scripts/FiniteStateMachine.cs](StateMachine/Scripts/FiniteStateMachine.cs) | The machine. Owns `CurrentState`, `PausedStates`, the transition coroutine, the sorting-order counter, and the four lifecycle events. |
| `IState` | [StateMachine/Scripts/IState.cs](StateMachine/Scripts/IState.cs) | Narrow handle the FSM hands to a state (`TransitionTo` / `ClearPausedStates` / `ReloadCurrentState`) so states can't touch the whole FSM. |
| `State` | [StateMachine/Scripts/State.cs](StateMachine/Scripts/State.cs) | `ScriptableObject` base. Virtual `Enter/Pause/Resume/Exit/ExitImmediate` + the two authored flags `PausePreviousState`, `ShowBanner`. |
| `UIViewState` | [StateMachine/Scripts/UiViewState.cs](StateMachine/Scripts/UiViewState.cs) | State that owns a pooled/`Resources` UI prefab and binds its buttons. 17 shipping states subclass it. |
| `Transition` | [StateMachine/Scripts/Transition.cs](StateMachine/Scripts/Transition.cs) | Asset holding `ToState` + an optional `ClosePolicy` override + a `virtual Execute()` coroutine. |
| `TimedTransition` | [StateMachine/Scripts/TimedTransition.cs](StateMachine/Scripts/TimedTransition.cs) | `Transition` whose `Execute()` waits `delayInSeconds`. **No assets of this type exist in the project today.** |
| `StateLogger` | [StateMachine/Scripts/StateLogger.cs](StateMachine/Scripts/StateLogger.cs) | Dev-only `MonoBehaviour` that logs the four FSM events. **Not present in any scene or prefab.** |
| `UIBase` | [Base/Scripts/UiBase.cs](Base/Scripts/UiBase.cs) | Abstract view root: `Show/Hide/Pause/Resume`, canvas sorting, raycast blocking, `CloseImmediate`. |
| `IShowable` / `ICanvasSorter` | [Base/Scripts/IShowable.cs](Base/Scripts/IShowable.cs) · [Base/Scripts/ICanvasSorter.cs](Base/Scripts/ICanvasSorter.cs) | The two contracts `UIBase` implements; `GameState` talks to its HUD through `IShowable` only. |
| `UiAnimationSystem` | [Base/Scripts/UiAnimationSystem.cs](Base/Scripts/UiAnimationSystem.cs) | Plain C# DOTween driver constructed per `UIBase`. Plays Fade / Scale / Slide / None per phase. |
| `StateAnimationConfig` | [Base/Scripts/StateAnimationConfig.cs](Base/Scripts/StateAnimationConfig.cs) | Per-view SO: enter/exit type, ease, duration, `StartScale`. Also declares `DefaultAnimationType` + `AnimationPhase`. |
| `UICloseReasons` / `ClosePolicy` | [Base/Scripts/UiCloseReasons.cs](Base/Scripts/UiCloseReasons.cs) | The caller's *intent* (10 reasons) and the stack operation it maps to (`Default`, `ClearAll`, `PopUntil`, `PopOne`). |
| `ApplicationBase` | [GameFlow/Scripts/ApplicationBase.cs](GameFlow/Scripts/ApplicationBase.cs) | Splash-scene `MonoBehaviour`. Sets frame rate, starts `TimeMachine.Tick()`, then `yield return FiniteStateMachine.Init()`. **The only boot entry.** |
| `BaseApplicationFlowController<T>` | [GameFlow/Scripts/BaseApplicationFlowController.cs](GameFlow/Scripts/BaseApplicationFlowController.cs) | Event→transition router. Owns reason→policy mapping and the single-in-flight transition coroutine. |
| `ApplicationFlowController` | [GameFlow/Scripts/ApplicationFlowController.cs](GameFlow/Scripts/ApplicationFlowController.cs) | Generic kit sample subclass (MainMenu/SpinWheel/Game/…). The shipping game uses `GameFlow` instead. |
| `SplashState` | [GameFlow/Scripts/SplashState.cs](GameFlow/Scripts/SplashState.cs) | Boot state: additive-loads the game scene, builds the state roots, instantiates `GameFlow`, boots LiveOps. |
| `Loading` | [GameFlow/Scripts/Loading.cs](GameFlow/Scripts/Loading.cs) | Splash progress bar. Tweens a `Float` variable 0→1 that `SplashState` waits on. |
| `StateRootManager` | [GameFlow/Scripts/StateRootManager.cs](GameFlow/Scripts/StateRootManager.cs) | Static holder for the two scene roots (`States`, `UIStates`) that states parent their instances under. |
| `BannerController` | [../AppFlow/GameFlow/Scripts/BannerController.cs](../AppFlow/GameFlow/Scripts/BannerController.cs) | Bridges `OnStateEntered`/`OnStateResumed` to `MonetizationAdsService`, driven purely by `State.ShowBanner`. |

Dead / unwired in this project: `UiAnimations` + `UiConfig` ([Base/Scripts/UiAnimations.cs](Base/Scripts/UiAnimations.cs)) — the
`MonoBehaviour` is on no prefab; `UiConfig` survives only because `GameHud`/`Hud` reuse it for HUD bars.
`AnimationEventHandler` ([Base/Scripts/AnimationEventHandler.cs](Base/Scripts/AnimationEventHandler.cs)) has no references at all.
Inside `UiAnimationSystem`, `_currentAnimationCoroutine` and `WaitForAnimationComplete` are never assigned/called.
`UIBase.OnHideComplete` is private and unreferenced. `FiniteStateMachine.ReloadCurrentState()` has no callers.

---

## State lifecycle

### Boot

```
ApplicationBase.Start()                       (splash scene MonoBehaviour)
  └─ FiniteStateMachine.Init()                FiniteStateMachine.cs:43
       CurrentState = BootState (SplashState)
       yield SplashState.Enter(fsm)           SplashState.cs:33
         ├─ LoadSceneAsync(1, Additive), allowSceneActivation = false
         ├─ WaitUntil(progress >= 0.9f)
         ├─ WaitUntil(SceneLoadingProgress >= 1)   ← driven by Loading.cs
         ├─ Instantiate(Resources.Load<GameFlow>("GameFlow"))
         ├─ allowSceneActivation = true; WaitUntil(isDone); SetActiveScene
         ├─ SetupStateRoots()  →  StateRootManager.Initialize(States, UIStates)
         ├─ GameFlow.Boot()                    GameFlow.cs:125
         └─ liveOpsBootstrapper.Bootstrap()
       OnStateEntered(SplashState)
       PausedStates.Clear()
```

`GameFlow.Boot()` branches on the Yarn current-level `DBInt`: `<= 1` boots straight into gameplay for the
first-level tutorial, otherwise it boots Home.

```
Splash ──► Home ──► YarnSortGameState ──► LevelCompleteState ──► Home        (level >= GoToHomeOnLevelCompleteFromLevel)
                          ▲    │                    └────────────► YarnSortGameState (next level)
                          │    └────► LevelFailState ──► RetryAfterLevelFailState / Home
                          │
   paused overlays pushed on top of whatever is current:
   SettingsState · HomeSettingsState · ShopState · RemoveAdsState · BuyBoosterState · LivesRefillState
   FeatureUnlockedState · BoosterUnlockedState · LeaveState · RetryState
   IAPPurchasePendingState → IAPPurchaseSuccessState / IAPPurchaseFailedState
```

### One transition, exactly as coded (`DoTransition`, FiniteStateMachine.cs:77)

1. Bail if `CurrentState == nextState` (logs "skipping redundant transition").
2. Bail if `nextState` is on the paused stack — you must `PopOne`/`PopUntil` instead (FiniteStateMachine.cs:89).
3. **Either** `PauseCurrentState()` — when `pauseCurrent` or `nextState.PausePreviousState` — which
   increments `v_CurrentStateSortingOrder` by 10, awaits `CurrentState.Pause()`, fires `OnStatePaused`, then
   pushes onto the stack; **or** `ExitCurrentState()` — awaits `CurrentState.Exit()`, fires `OnStateExited`.
4. `yield return transition.Execute()` — the transition's own coroutine (base yields immediately;
   `TimedTransition` waits).
5. `yield return nextState.Enter(this)` — **awaited in full**.
6. Only now `CurrentState = nextState`, then `OnStateEntered`.

Pop/jump paths: `PopPausedState()` exits current then resumes the stack top; `JumpTo(target)` `ExitImmediate()`s
every state above the target (each decrementing the sort order by 10), then exits current and resumes the
target; if the target was never paused it exits current, `ClearPausedStates()`, and enters the target fresh.
`ClearPausedStates()` tears the whole stack down with `ExitImmediate()` — **no animations, no awaits** — because
those states are occluded and paying N exit animations serially would be visible dead time.

### Inside a `UIViewState`

`Enter` (UiViewState.cs:24): `base.Enter` (stores `Listener`) → `uIStatesPooler.Get(stateId)` *or*
`Resources.Load<GameObject>(stateId)` + `Instantiate` → parent to `StateRootManager.UIStates`,
`SetAsLastSibling`, stretch the `RectTransform` and reset scale/rotation → `GetComponent<UIBase>()` →
`SetActive(true)` → **`_uiInstance.Show()` — fire-and-forget, not awaited** → `OnBindView(view)`.

`Exit` (UiViewState.cs:73): null `_uiInstance` first (double-release guard) → `BlockRaycast()` → `OnUnbindView` →
**`yield return instance.Hide()` — awaited** → pool `Release` or `Destroy` → `base.Exit()`.

`ExitImmediate` (UiViewState.cs:105): `OnUnbindView` → `CloseImmediate()` → release. No yields.

`Pause`/`Resume` forward to `_uiInstance?.Pause()/Resume()`, which flip `blocksRaycasts` and force-complete any
running tween (`UiAnimationSystem.ForceCompleteCurrentAnimation`).

`GameState` ([../AppFlow/States/Game/GamePlay/Scripts/GameState.cs](../AppFlow/States/Game/GamePlay/Scripts/GameState.cs))
is the non-UI variant: it spawns a gameplay prefab under `StateRootManager.States` and a HUD under
`UIStates`, awaits `gameHudInstance.Hide()` in `Exit`, and raises four `GameEvent`s
(`GameStateEnter/Paused/Resumed/Exit`).

---

## Transitions

A `Transition` is an asset with two authored fields and one hook:

- `ToState` — the destination `State` asset.
- `closePolicy` — `Default` (use the reason's mapping) / `ClearAll` / `PopUntil` / `PopOne`.
- `virtual IEnumerator Execute()` — runs *between* the outgoing state leaving and the incoming state entering.
  The base yields immediately; `TimedTransition` waits `delayInSeconds`.

Transitions are **not registered on states**. They are `[SerializeField]` fields on the flow-controller
`MonoBehaviour`, each paired with a `GameEvent` in `RegisterFlowEvents()`. Evaluation
(BaseApplicationFlowController.cs:40-76):

```
GameEvent raised → GoTo(transition, reason)
   stop any in-flight HandleTransition coroutine
   policy = transition.closePolicy != Default ? transition.closePolicy : GetPolicyForReason(reason)
   switch policy: ClearAll → fsm.ClearPausedStates() | PopOne → fsm.PopPausedState() | PopUntil → fsm.JumpTo(ToState) and return
   fsm.TransitionTo(transition, ShouldPauseCurrent(reason), exitCurrentImmediately: reason == Home)
```

| `UICloseReasons` | `ClosePolicy` | pauses current? | outgoing state's exit |
|---|---|:--:|---|
| `Home` | `ClearAll` | no | `ExitImmediate` (instant — see below) |
| `Game` | `ClearAll` | no | `Exit` (normal, animated) |
| `ResumeGame`, `Revive`, `SkipLevel` | `PopOne` | no | `Exit` |
| `ResumeAny` | `PopUntil` | no | `Exit` |
| `FullScreenPlacement`, `ShowFullScreenPlacement`, `DailyLogin` | `Default` (no stack surgery) | **yes** | n/a — paused, not exited |
| `UntouchPaused` | `Default` | no | `Exit` |

`Home` is the one reason that also skips the CURRENT (non-paused) state's animated `Exit()` in favour of
`ExitImmediate()` — `exitCurrentImmediately` on `FiniteStateMachine.TransitionTo`/`DoTransition`/
`ExitCurrentState`, set by `HandleTransition` when `reason == UICloseReasons.Home`. Without this,
`GameState.Exit()` awaits `gameHudInstance.Hide()`'s fade/slide animation before Home can even start
entering — a real, user-visible delay on the back button that going Home specifically must not have,
since the player is leaving the level outright. `Game` (e.g. loading the next level) deliberately keeps
its normal animated exit; only `Home` is special-cased.

The back button is wired straight to `PopPausedState()` (BaseApplicationFlowController.cs:78).
All 19 shipping `Transition` assets are authored `closePolicy: Default`, so today the reason mapping is the
only thing choosing a policy.

---

## UI states (`UIViewState` / `UIBase`)

**Binding.** `stateId` is both the pool key (`PoolManagerSO.Get/Release`) and, when `usePooling` is off, the
`Resources.Load` path. The state finds `UIBase` by `GetComponent` on the acquired object and stores it in
`_uiInstance`. The relationship is strictly one-way: **the state subscribes to the view's buttons in
`OnBindView` and unsubscribes in `OnUnbindView`; the view never references the state.** See
`HomeState.OnBindView` (HomeState.cs:57) and `RemoveAdsState.OnBindView` (RemoveAdsState.cs:75).

**Show/hide animation.** `UIBase.Show(bool playDefaultAnimation = false, bool makeStateinteractable = true)`
and `UIBase.Hide(bool playDefaultAnimation = false)` both default to **no animation**, and `UIViewState`
always calls the parameterless overloads. So an enter/exit animation exists only if the concrete view
overrides `Show`/`Hide` and forwards `true` — e.g. `RemoveAdsView.Show → base.Show(true, true)` and
`RemoveAdsView.Hide → base.Hide(true)`, whereas `HomeView.Show → base.Show(false, true)`.
`Hide` is guarded by `_isHiding`, blocks raycasts, then `WaitUntil(completed)` before deactivating —
that `WaitUntil` (UiBase.cs:103) is what the FSM is parked on during `Exit`.

**Canvas sorting.** `UIBase.Show` reads the shared `Int` variable `v_CurrentStateSortingOrder` into
`_canvas.sortingOrder`, then `ApplySortingOrder` walks the cached `childCanvases` (root excluded) assigning
`start+1, start+2, …`. The counter itself is moved only by the FSM: `+10` on pause, `−10` on resume/pop.

**Close reasons.** A state never decides how the stack unwinds. It raises `backBtnPressedEvent` (→ `PopOne`)
or a `goToXEvent`, and the flow controller turns the accompanying `UICloseReasons` into the policy.

---

## RULES

1. **Never call `Enter`/`Exit`/`Pause`/`Resume` on a state yourself — always go through the FSM.**
   The FSM is what assigns `Listener`, updates `CurrentState`, maintains the paused stack and the sorting
   counter, and fires `OnStateEntered`. `GiveUpState` is the one violation in the codebase
   (`StartCoroutine(giveUpState.Enter(null))`, GameFlow.cs:198) and it pays for it: it is never `CurrentState`,
   never pushed, and although its asset sets `ShowBanner: 1` the banner never shows because
   `BannerController` only listens to FSM events.

2. **Never acquire, activate, reparent or destroy a state's view outside `UIViewState`.**
   `Enter`/`Exit`/`ExitImmediate` are the only places the pool is touched; a manual `SetActive` leaves
   `_uiInstance` and the pool disagreeing, and the next `Release(stateId, …)` returns a live object.

3. **`Enter` is setup, not work — never park in it.** The FSM awaits `Enter` in full (FiniteStateMachine.cs:106)
   and only assigns `CurrentState` *after* it returns, so a slow `Enter` freezes the flow and leaves the FSM
   reporting the *previous* state. `SplashState` is the deliberate exception (it is awaiting scene load).

4. **Anything you subscribe in `Enter` must be unsubscribed in `Exit` *and* in `Pause`, and re-subscribed in
   `Resume`.** States are `ScriptableObject`s: `OnEnable`/`OnDisable` do not scope to being on screen.
   `RemoveAdsState` documents the exact failure (RemoveAdsState.cs:30) — a subscription that survives `Pause`
   fired a second concurrent `PopPausedState` when the IAP success state closed.

5. **Re-assert subscriptions of `ScriptableObject` collaborators on every `Enter`.**
   `GameEvent` nulls its handler list in `OnDisable`, and an SO service cannot self-heal in `OnEnable` like a
   scene component. `HomeState.Enter` calls `liveOpsDispatcher?.Initialize()` **before** `base.Enter`
   (HomeState.cs:42) so LiveOps routing is live before the view's delayed home-landing fires. This only works
   because `Initialize()` is idempotent — keep any such re-assert idempotent.

6. **Make `Enter` self-cleaning (idempotent).** A transition coroutine can be stopped mid-`Exit`, so a state can
   be re-entered while still owning instances. `GameState.Enter` calls `ReleaseSpawnedInstances()` first
   (GameState.cs:47) — without it you get two HUDs and two gameplay boards.

7. **Do all teardown after the last `yield` synchronously, and null your handle before yielding.**
   `UIViewState.Exit` nulls `_uiInstance` before awaiting `Hide()` (UiViewState.cs:78) so a re-entrant `Exit`
   can't double-release; `GameState.ReleaseSpawnedInstances` is deliberately yield-free (GameState.cs:114) so
   coroutine cancellation can't leave it half-done.

8. **Override `ExitImmediate` in any state that owns spawned objects — and never let it yield or animate.**
   The default falls back to `Exit()` (State.cs:40), so a stack clear would pay every state's hide animation
   serially while the destination builds. `UIViewState` and `GameState` both override it.

9. **Don't expect an enter animation from the state layer.** `UIViewState` calls `Show()` with the defaults
   (`playDefaultAnimation = false`) and does not await it. Opting into the animation is the **view's**
   job — override `Show`/`Hide` and forward `true` (see `RemoveAdsView`).

10. **A `Hide` override must always reach completion.** `Exit` awaits `instance.Hide()`, and `UIBase.Hide` ends
    in `WaitUntil(() => completed)` (UiBase.cs:103). A custom hide that never completes deadlocks the whole
    FSM, not just that screen.

11. **Never write to `v_CurrentStateSortingOrder` from state or view code.** Only the FSM moves it
    (`+10` on pause, `−10` on resume/pop/clear); views may only *read* it via `UIBase.Show`. An unbalanced
    write permanently skews every subsequent screen's canvas order.

12. **Never transition to a state that is currently paused.** `DoTransition` refuses it with a warning
    (FiniteStateMachine.cs:89). Use `ResumeGame`/`Revive`/`SkipLevel` (`PopOne`) or `ResumeAny` (`PopUntil`).

13. **Never let the same `State` asset be pushed twice.** One asset = one instance; a duplicate push corrupts
    `PausedStates` and `_pausedStateLookup`. The guard at FiniteStateMachine.cs:193 is `#if UNITY_EDITOR` only —
    in a build the corruption is silent.

14. **State logic raises `GameEvent`s; it does not drive the FSM.** Every shipping state closes itself with
    `backBtnPressedEvent.Invoke()` and navigates with `goToXEvent` (`RemoveAdsState.CloseState`,
    `BuyBoosterState.Close`, `LevelCompleteState.GoToHome`). Keeping the policy decision in the flow controller
    is what makes `UICloseReasons` meaningful.

15. **Route every navigation through `GoTo`, never a hand-rolled coroutine.** `BaseApplicationFlowController`
    tracks `_activeHandleTransitionCoroutine` and stops the previous one; the comment at
    BaseApplicationFlowController.cs:42 records the bug it fixes — concurrent transitions cancelling each other
    mid-`Enter` and orphaning HUD instances.

16. **`ShowBanner` is a declarative flag on the state asset — never call `ShowBanner()`/`HideBanner()` from a
    state.** `BannerController` evaluates it on `OnStateEntered` and `OnStateResumed` only
    (BannerController.cs:15-16). Consequences to respect: (a) it is *not* evaluated on exit or pause, so the
    incoming/resumed state alone decides the banner — every state must set the flag correctly, and the default
    is `false`; (b) states entered outside the FSM never trigger it (see rule 1). Today only the gameplay states
    (`NormalGameState`, `YarnSortGameState`, `TemplateGameState`) and `GiveUpState` set it to `1`.

17. **Subscribe with method groups, not lambdas, whenever you need to unsubscribe.** `GameFlow`/
    `ApplicationFlowController` unsubscribe with freshly allocated lambdas (GameFlow.cs:161-174), which never
    match the delegates added in `RegisterFlowEvents` — those handlers are effectively never removed. Only the
    method-group ones (`GoToHome`, `GotoGame`, `backBtnPressedEvent`) actually detach. Follow the method-group
    pattern for anything new.

18. **Keep `stateId` in sync with the prefab.** It is simultaneously the `PoolManagerSO` key and the
    `Resources` path; a mismatch fails at runtime with a `LogError` and an empty screen (UiViewState.cs:35/47).

19. **`UIBase` must sit on the object that carries the `Canvas` and `CanvasGroup`.** `Awake` overwrites the
    serialized `_canvas`/`_canvasGroup` with `GetComponent` results (UiBase.cs:28-29) and constructs the
    animation system from them; on a child object you get null-refs on the first `Show`.

---

## Adding a new state

1. **Write the class.** Subclass `UIViewState` for a screen/popup, `GameState` for a gameplay+HUD state, or
   `State` for a headless one. Add `[CreateAssetMenu(menuName = "GameStates/…")]`.
2. **Override `OnBindView` / `OnUnbindView`** (UI states) to add and remove the view's button listeners. Do not
   let the view reference the state.
3. **Build the prefab.** Root object with `Canvas` + `CanvasGroup` + your `UIBase` subclass; assign
   `animationConfig` (a `StateAnimationConfig`) and `currentStateSortingOrder`
   (`GameFlow/Variables/v_CurrentStateSortingOrder.asset`). Register it in the `PoolManagerSO` under the id you
   will use, or place it in a `Resources` folder if you set `usePooling = false`.
4. **Create the state asset.** Set `stateId` to that id/path, assign `uIStatesPooler`, and set the two authored
   flags: `PausePreviousState` (true only if this screen should stack *over* the current one — today only
   `RemoveAdsState` does) and `ShowBanner`.
5. **Create the transition asset** — `Create ▸ ProjectCore ▸ State Machine ▸ Transitions ▸ Basic Transition`.
   Set `ToState` to the new state. Leave `closePolicy = Default` unless this specific route must override the
   reason mapping.
6. **Create the entry `GameEvent` asset** that gameplay/UI will raise.
7. **Wire it in [`GameFlow`](../AppFlow/GameFlow/Scripts/GameFlow.cs)** — add a `[SerializeField] GameEvent` and
   `[SerializeField] Transition` field, then `Subscribe`/`UnSubscribe` in `RegisterFlowEvents` /
   `UnregisterFlowEvents`, choosing the `UICloseReasons` whose policy you want (see the table above).
8. **Assign the references** on `Assets/GameKit/AppFlow/GameFlow/Prefabs/Resources/GameFlow.prefab` — the prefab
   `SplashState` instantiates from `Resources`.
9. **Override `ExitImmediate`** if the state owns spawned objects and does not already inherit a suitable one.

Nothing needs registering with `StateRootManager` (it is a static holder initialised once by `SplashState`) and
nothing needs adding to `FiniteStateMachine.asset` — only `BootState` lives there.

---

## Gotchas

- **The FSM is an asset, so its runtime state is serialized into it.** `CurrentState` and `PausedStates` are
  `[SerializeField]`; `FiniteStateMachine.asset` is currently committed with `CurrentState` pointing at
  `HomeState`. Expect spurious diffs after play sessions, and don't treat the checked-in value as meaningful —
  `Init()` overwrites it with `BootState`.
- **`Init()` clears `PausedStates` *after* entering the boot state, and never clears `_pausedStateLookup`**
  (FiniteStateMachine.cs:51-56). `_pausedStateLookup` is a plain field, so with *Enter Play Mode without Domain
  Reload* stale entries survive and `TransitionTo` will silently refuse a perfectly valid destination with
  "State … is paused".
- **`v_CurrentStateSortingOrder` persists.** The asset has `ResetToDefaultOnPlay: 0` and is currently sitting at
  `10`, not `0` — evidence of an unbalanced pause/resume leaking across sessions. If UI starts sorting oddly,
  check this value first.
- **`TimedTransition` is unused** — no assets exist. `Transition.Execute()` is therefore always a no-op today.
- **Every shipping transition is `closePolicy: Default`**, so the reason→policy table is the *entire* policy
  surface in practice. Overriding `closePolicy` on an asset is an untested path.
- **`UICloseReasons` is larger than its mapping.** `FullScreenPlacement`, `ShowFullScreenPlacement`, `DailyLogin`
  and `UntouchPaused` all fall through to `Default` — meaning *no stack surgery*. The first three still pause
  the current state via `ShouldPauseCurrent`; `UntouchPaused` neither pauses nor pops, so it fully **exits** the
  current state. That is the intent for `LeaveState`/`RetryAfterLevelFailState`, but it is easy to mis-pick.
- **Two flow controllers exist.** `Blues.Core.Application.ApplicationFlowController` is the generic kit sample;
  the shipping game uses the global-namespace `GameFlow`. Both derive from `BaseApplicationFlowController<Transition>`;
  only `GameFlow` is instantiated (by `SplashState`, from `Resources/GameFlow`).
- **Unsubscribe-by-lambda does nothing** (rule 17). In practice the flow controller's handlers stay attached for
  the app's lifetime, which is survivable only because there is exactly one `GameFlow` instance.
- **Debug spam in the hot path:** `UIBase.Show`/`Hide` call `print(...)` (UiBase.cs:46, 77) and
  `BaseApplicationFlowController.GoTo` prints every navigation (line 45). Strip these before profiling.
- **`StateRootManager.Initialize` is guarded by `IsInitialized`** and never resets — a second additive load of
  the game scene would leave every state parenting into the destroyed roots.
- **`FiniteStateMachine.Init()` only warns** when `BootState` is unassigned and then yields out; the game simply
  never starts, with one console line to explain it.
