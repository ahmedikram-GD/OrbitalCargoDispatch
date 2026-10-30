# ANALYSIS — source projects for Orbital Cargo Dispatch

> Phase 0 deliverable (read-only survey, 2026-10-30). Sources: `D:\Projects\NumberixPuzzle` (NX — the GameKit) and `D:\Projects\Silly-Justice` (SJ — first-person controller). Paths below are relative to each project's `Assets/`. Nothing in either project was modified.

**Headline facts**

| | NumberixPuzzle (kit) | Silly-Justice (FP controller) | New project |
|---|---|---|---|
| Unity | 6000.3.6f1 | 6000.3.15f1 | 6000.3.15f1+ (same 6.3 LTS stream; no API breaks) |
| Render pipeline | URP 17.3.0, **2D Renderer** (portrait puzzle) | URP 17.3.0, Universal Renderer, mobile asset (scale 0.8, no shadows) | URP 17.3 Universal 3D template |
| Input | Input System 1.18 ("Both" mode) | Input System 1.19 ("Both" mode) | Input System only — fine, neither codebase uses legacy `Input.*` in scope |
| Orientation | Portrait | Landscape | Landscape |
| Tweening | DOTween Pro 1.2.765 (+`DOTween.Modules` asmdef) | DOTween Pro 1.2.760 | copy NX's |
| Haptics | Lofelt NiceVibrations 4.1.2 (inside `Feel/`) | Lofelt NiceVibrations 4.1.2 (`_AhmedIkram/Plugins/`) | copy SJ's lean folder (identical code, no demos) |
| Inspector | **Odin Inspector 3.1** (paid) in 150/611 kit files | none | **decision needed** (see Risks #3) |
| Assemblies | `GameKit.Runtime` + `GameKit.Editor` asmdefs; game in Assembly-CSharp | none (all Assembly-CSharp, no namespaces) | `GameKit.Runtime` + game assembly |

---

## 1. Kit inventory (NumberixPuzzle `GameKit/`)

The kit's one architectural rule (from `GameKit/Architecture/ARCHITECTURE.md`): **"everything shared is an asset, not a singleton"** — state lives in ScriptableObject variables, signals go through ScriptableObject events, services are ScriptableObjects wired in the inspector. No singletons, no `FindObjectOfType`, no live DI container (VContainer is in the manifest but has **zero** code usage).

Legend for the "Port?" column: ✅ copy as-is · 🔧 copy with small edits · ❌ skip for the pitch.

### 1.1 Pooling — `Architecture/Utilities/PoolSystem/` ✅
- **What:** SO-configured GameObject pools over `UnityEngine.Pool.ObjectPool<T>`. One `PoolManagerSO` asset = a list of `PoolConfig { PoolID, Prefab, DefaultCapacity=10, MaxSize=100, Prewarm }`. Static `PoolManagerRunner` hosts delayed releases. Optional `IPoolable { OnPoolGet(); OnPoolRelease(); }` on the pooled root.
- **Entry point:** `GameKit.Architecture.PoolSystem.PoolManagerSO` (`Create ▸ Pools ▸ Pool Manager`). `Initialize(Transform root)` once at boot (the kit's `AppBase` does this for every pooler), then `Get(id)` / `Release(id, go)` / `ReleaseAfterDelay(id, go, seconds)`.
- **Gotchas:** `Get` on an unknown id returns `null` (always null-check); `Release` on an unknown id destroys the object. GameObject pools only. `PooledFxAutoRelease` and `PoolMonitor` are dead.
- **Deps:** Odin attribute only.
```csharp
[SerializeField] private PoolManagerSO particlesPooler;   // e.g. ParticlesPooler.asset
GameObject fx = particlesPooler.Get("CoinPop");
if (fx == null) return;
fx.transform.position = worldPos;
particlesPooler.ReleaseAfterDelay("CoinPop", fx, 1.5f);
```

### 1.2 Audio — `Architecture/Utilities/SoundSystem/` ✅
- **What:** `SoundService : ScriptableObject` is a **string-keyed sound library** (list of `Sound { SoundName, SoundType {SFX,UI,Music}, AudioClip | AudioClips[] + PlayRandomClip, Loop, Volume, Pitch, UseRandomPitch }`) playing through a pooled set of `AudioSource`s (`AudioSourcePool`, max 30 concurrent) routed to an `AudioMixer` from `SoundSettings` (exposed params must be named `volume`, `sfx`, `ui`, `music`; groups `SFX`/`UI`/`Music`). Gated by a `DBBoolWithEvent` sound toggle. Music is *not* routed through it — `Art/Sounds/Events/GameBgMusicController` does plain AudioSource fades on FSM events.
- **Entry point:** `THEBADDEST.SoundSystem.SoundService` asset. `Initialize()` once, then `Play(name)`, `Play(name, Vector3 pos)`, `Stop`, `FadeIn/FadeOut`, `SetVolume`, `SetMute`.
- **Deps:** `THEBADDEST.Coroutines`, Variables (`DBBoolWithEvent`), Odin attribute.
```csharp
[SerializeField] private SoundService soundService;
soundService.Initialize();                       // once at boot
soundService.Play("Launch");
soundService.Play("CratePlace", slot.position);  // 3D
soundService.FadeIn("ShiftLoop", 0.5f, 0.8f);
```

### 1.3 Haptics — `Haptics/Scripts/HapticsHandler.cs` ✅
- **What:** `GameKit.Haptics.HapticHandlerSO` (file name ≠ class name) wraps Lofelt `HapticPatterns.PlayPreset`. `ApplyHapticFeedback(HapticIntensity)` with `Selection, Light, Soft, Medium, Hard, Success, Failure`; gated by the `Vibrations` `DBBoolWithEvent`. `ApplyDynamicHaptic` is dead. One asset: `Haptics/Scriptables/HapticHandlerSO.asset`.
- **Also reusable (game-side, Numberix):** `Numberix/Game/Scripts/Haptics/NumberixHapticPolicy.cs` — pure-C#, unit-tested amplitude/budget policy for amplitude-capable devices. Overkill for the pitch; note for later.
- **Deps:** `Lofelt.NiceVibrations`, Variables.
```csharp
[SerializeField] private HapticHandlerSO haptics;
haptics?.ApplyHapticFeedback(HapticIntensity.Medium);   // crate snaps into slot
haptics?.ApplyHapticFeedback(HapticIntensity.Success);  // shuttle launch
```

### 1.4 Events / messaging — `Architecture/Utilities/Events/` ✅
- **What:** ScriptableObject event channels. `GameEvent` (`Subscribe(EventHandler)`, `UnSubscribe`, `Invoke()`, `HasListeners()`; `Invoke` try/catches each subscriber; **`OnDisable` nulls all handlers**). Typed `GameEventWithParam<T>` / `<T1,T2>` / `<T1,T2,T3>` with **`Raise(t)`** (plain multicast). Concrete assets: `GameEventWithBool/Int/String/Vector2Int`, `GameEventWithReturn<T>` (`Func<T>`), `GameEventWithReturnInt`. `GameEventButton` binds a uGUI Button to `Invoke()`. No listener MonoBehaviour/UnityEvent bridge — subscribers are code.
- **Asset naming:** `e_<Name>.asset`. New typed events are 3-line subclasses (`class GameEventWithCrate : GameEventWithParam<Crate>`).
- **Rule:** subscribe in `OnEnable`/`Enter`, unsubscribe in `OnDisable`/`Exit`, **method groups not lambdas**. SOs must not subscribe to events themselves (OnDisable wipe) — a scene `*Bootstrap` MonoBehaviour does it.
- **Deps:** Odin `[Button]` only.
```csharp
[SerializeField] private GameEvent onShuttleLaunched;          // e_ShuttleLaunched
[SerializeField] private GameEventWithInt onCreditsChanged;    // e_CreditsChanged
void OnEnable()  => onShuttleLaunched.Subscribe(HandleLaunched);
void OnDisable() => onShuttleLaunched.UnSubscribe(HandleLaunched);
...
onShuttleLaunched.Invoke();
onCreditsChanged.Raise(credits.GetValue());
```

### 1.5 Shared variables — `Architecture/Utilities/Variables/` ✅
- **What:** SO "variable" assets with `Value`/`DefaultValue`/`ResetToDefaultOnPlay`, `GetValue()/SetValue()`. Non-persistent: `Int, Float, Double, Bool, SharedString, SharedVector2/3/2Int/3Int`, `IntWithEvent/BoolWithEvent/StringWithEvent` (fire a `GameEvent` on mutation). Persistent: `DBInt, DBFloat, DBBool, DBString` (+`WithEvent`) → **PlayerPrefs** by a hand-typed `key`; `DBInt/DBFloat/DBBool.SetValue/Increment/Decrement` auto-save, **`DBString.SetValue` does not — call `Save()`**. `SharedDictionary` needs Odin serialization (skip).
- **Asset naming:** `v_<Name>` non-persistent, `vDBInt_<Name>` persistent.
- **Gotcha:** `Value` is serialized → play sessions dirty `.asset` files (set `ResetToDefaultOnPlay`). This is the kit's entire "save system" — enough for credits/upgrades in the pitch.
```csharp
[SerializeField] private DBInt credits;          // vDBInt_Credits, key "Credits"
[SerializeField] private DBBool gravSledOwned;   // vDBBool_GravSled
credits.Increment(payout);                       // persists
if (credits.GetValue() >= price) { credits.Decrement(price); gravSledOwned.SetValue(true); }
```

### 1.6 UI screens / state machine — `StateMachine/` + `AppFlow/` 🔧
- **What:** One `FiniteStateMachine` SO asset holds `CurrentState` + a `Stack<State> PausedStates`. States (`State`, `UIViewState`, `GameState`) and `Transition`s are SO assets. Navigation is event-driven: UI raises a `GameEvent` → a scene `BaseApplicationFlowController<T>` subclass (`GameFlow`) maps it to `(Transition, UICloseReasons)` → `ClosePolicy { Default, ClearAll, PopUntil, PopOne }` → FSM op. Views are pooled uGUI prefabs (`UIBase` on a root with `Canvas` + `CanvasGroup`; `Show(animate, interactable)`, `IEnumerator Hide(animate)`, sort order derived from stack depth) spawned by `stateId` from a `PoolManagerSO` or `Resources.Load`. **MVP rule: state is the brain, view is passive, view never references the state.**
- **Adding a screen** (verified against `AppFlow/States/Settings/Scripts/{SettingsState,SettingsView}.cs`): subclass `UIViewState` overriding `OnBindView/OnUnbindView`; prefab root `Canvas`+`CanvasGroup`+your `UIBase` subclass; register prefab id in the pooler; create State + Transition + entry `GameEvent` assets; add the pair to `GameFlow.RegisterFlowEvents` with a `UICloseReasons`; wire on the `GameFlow` prefab.
- **For the pitch:** keep the FSM core (`StateMachine/StateMachine/Scripts/*`, `StateMachine/Base/Scripts/*`, `StateMachine/GameFlow/Scripts/{BaseApplicationFlowController,StateRootManager}.cs`, `AppFlow/States/Game/GamePlay/Scripts/GameState.cs`) and write **our own** small `GameFlow` + 3 states (HUD/GameState, Pause, ShiftSummary). **Do not copy `AppFlow/States/*`** — 27 portrait puzzle screens coupled to ads/IAP/LiveOps.
- **Deps:** Odin (`FiniteStateMachine : SerializedScriptableObject` — the one hard Odin use in the core; trivially re-based to `ScriptableObject` if Odin is dropped), DOTween (`UIBase`/`UiAnimationSystem`), `THEBADDEST.Coroutines`, Pooling, Variables.
```csharp
[CreateAssetMenu(menuName = "OCD/States/Shift Summary")]
public class ShiftSummaryState : UIViewState
{
    [SerializeField] private GameEvent nextShiftPressed;         // → GameFlow maps to GameTransition, ClearAll
    private ShiftSummaryView view;
    protected override void OnBindView(UIBase v)   { view = (ShiftSummaryView)v; view.NextShiftButton.onClick.AddListener(NextShift); }
    protected override void OnUnbindView(UIBase v) { view?.NextShiftButton.onClick.RemoveListener(NextShift); view = null; }
    private void NextShift() => nextShiftPressed.Invoke();
}
```

### 1.7 Scene / app flow — `StudioSplash/`, `StateMachine/GameFlow/`, `AppFlow/GameFlow/Scripts/AppBase.cs` 🔧
- **How NX boots:** `Boot.unity` (camera + `StudioSplashController` + `StudioSplashBootstrap`) → additive `SplashScene.unity` (`AppBase.Start`: `soundService.Initialize()`, every `PoolManagerSO.Initialize(poolHolder)`, `TimeMachine.Tick()`, then `FSM.Init()` → `SplashState` additively loads `Game.unity`, instantiates `Resources/GameFlow`, sets up `StateRootManager.States/UIStates`, `GameFlow.Boot()`). Gameplay and HUD prefabs are **not in the scene** — `GameState` spawns them by id.
- **For the pitch:** a single `Boot` → `Game` flow is enough. Copy `StudioSplash/` only if a studio splash is wanted (it's dependency-free). Write a 40-line `AppBase`-style bootstrap of our own that initialises sound + pools + FSM (the kit's `AppBase` drags in Firebase remote config and analytics).
- **Device knobs** (standalone Monos, ✅): `AppFlow/GameFlow/Scripts/FrameRateSetter.cs` (per-tier URP render scale, 60 fps), `MultiTouchSetter.cs`. Skip `AdaptiveFrameRate`/`URPRenderScaleSetter`.

### 1.8 Tweening / juice ✅ (selected)
- **DOTween** is the kit's tween layer (82 kit files). Feel/MMFeedbacks is present in `Assets/Feel` but has **zero** code references — don't copy Feel (only NiceVibrations inside it is used).
- `UI/Animations/CoinTrail/Scripts/UICollector.cs` — coin fly-to-HUD: static `PlayTrailAction(poolId, RectTransform from, RectTransform to, count)`; pooled coins spread then converge, SFX `"CollectSpawn"`, `Soft` haptic per coin, `Hard` on finish, raises `HeaderSignals.CoinsGained`. **This is the credits payout pop-up.** Deps: DOTween, Pool, Haptics, Sound, `Art/UI/Scripts/Header/*`.
- `Art/UI/Scripts/Header/{CoinsWidget,HeaderSignals,IHeaderWidget}.cs` — HUD credits counter with count-up (`UIValueAnimator`), pulse, hit particles. Drop-in for the HUD credits field.
- `UI/ScreenTransition/ScreenFadeTransitionSO.cs` — dip-to-colour overlay, `Play(Action onCovered)`. Dependency-free except DOTween. Use between shifts.
- `Architecture/Utilities/UIValueAnimator.cs` — TMP int count-up coroutine.
- `Art/UI/Scripts/UISequentialScaler.cs`, `AppFlow/Architecture/View/Scripts/VerticalGroupAnimator.cs` — staggered panel reveals for the summary screen.
- `Core/Scripts/TransformShake.cs` exists but has no callers; **SJ's `CameraShaker` is better** (see §3) — use that for camera shake.
- Candidate FX prefabs that render in URP as-is: `Art/Particles/{ConfettiRainbow, GoldenHit, StarExplosion, StarTrail, ShopCoinsStars, SmallExplosion}`.

### 1.9 Coroutines / time ✅
- `Architecture/Utilities/CoroutineHandler-master/Runtime/` (`THEBADDEST.Coroutines`, own asmdef named `Coroutines`, zero deps): `CoroutineHandler.StartStaticCoroutine(...)`, `AfterWait(action, seconds, realTime)`, `AfterWait(action, Func<bool>)`, `WaitLoop`, `DoUpdate`. Lets ScriptableObjects run coroutines. Required by SoundService and the FSM.
- `Architecture/Utilities/TimeSystem/Scripts/TimeMachine.cs` — once-per-second `e_Tick` GameEvent (handy for the shift timer and cryo rings if we want tick-based updates). Note: two classes named `TimeMachine` exist (`THEBADDEST.Coroutines.TimeMachine` too) — always qualify.
- Skip `ServerTimeService`, `IdleTimer`.

### 1.10 Swipe / input — `Architecture/Utilities/SwipeSystem/` ✅
- `SwipeDetector` Mono (new Input System bindings `<Touchscreen>/touch0`, `minSwipeDistance`, `maxSwipeDuration`) → `event Action<SwipeData>`; `SwipeData { SwipeDirection Direction; float Intensity; float Duration }`; `KeyboardSwipeSimulator` (WASD) + `InputController` merging both into `static Action<SwipeData> SwipeAction`. No consumers in NX but self-contained. **Use for the junk-crate "swipe up to throw"** (with a guard so it only fires while holding junk and the touch began on the look area, not the joystick).
- `Architecture/Design Patterns/Command Pattern/` is a learning sample — skip.

### 1.11 Economy / rewards / config 🔧
- **Soft currency** = a `DBInt` (`v_SoftCurrency`, key `SoftCurrencyVariable`) + `e_UpdateSoftCurrency` event; no `Spend()` chokepoint — every caller does `GetValue() >= price` then `Decrement` and raises the event. We'll wrap this in one `CreditsService` SO for the pitch.
- `Rewards/`, `Economy/Currencies/Lives`, `Collection/`, `Boosters/` are coupled to LiveOps types (`RewardType`, `RewardSpawnAnimator`, `RewardsPrefabSO` live under `LiveOps/`) — **skip**; the "Added to collection" pop-up is a 1-view feature we write ourselves.
- `AppFlow/Scripts/GameConfig.cs` — central tunables SO, but Numberix-specific fields. We create our own `ShiftConfigSO`/`CrateTypeSO` following its style.

### 1.12 Ads abstraction — `Architecture/AdPackage/` ❌ (use SJ's mock instead)
- `MonetizationAdsService` is hard-wired to GDMonetization's static `AdsManager`; `IAdsService` is unused and doesn't match the implementation. `FullscreenAdGate` (static, SDK-agnostic) is fine but unnecessary. **SJ's `IRewardedAdService` + `FakeRewardedAdService`** is exactly the mock KICKOFF asks for (see §3.4).

### 1.13 Misc
- `UI/Theme/` (light/dark element tables) ✅ dependency-free but not needed.
- `Core/Fail/` (pluggable fail conditions) ❌ — failure is soft in our design.
- `Analytics/` ❌ — AppMetrica/Adjust coupled; nothing in our copy set references it once `AppFlow/States` is excluded.
- `Tutorials/`, `UI/Onboarding/` ❌ — hand/pinch tutorials for a puzzle; Rook radio lines are a new small script.
- `LiveOps/`, `Leaderboard/`, `MetaLoader/`, `LevelSystem/`, `LevelCompleteFeedback/` ❌.

### 1.14 Dependency matrix (systems we intend to copy)

| System | Odin | DOTween | TMP | Input System | NiceVibrations | Coroutines asmdef | Kit modules |
|---|---|---|---|---|---|---|---|
| Pooling | attr only | — | — | — | — | — | — |
| SoundService | attr only | — | — | — | — | **●** | Variables |
| HapticHandlerSO | — | — | — | — | **●** | — | Variables |
| Events | attr only | — | — | — | — | — | — |
| Variables (minus `SharedDictionary`) | attr only | — | — | — | — | — | Events |
| FSM core (`FiniteStateMachine`, `State`, `UIViewState`, `Transition`, `GameState`) | **● `SerializedScriptableObject`** (1 line) | — | — | — | — | **●** | Pooling, Variables |
| UIBase / UiAnimationSystem / StateAnimationConfig | — | **●** | — | — | — | — | Variables |
| BaseApplicationFlowController / StateRootManager | — | — | — | — | — | — | Events |
| UICollector + Header widgets | attr only | **●** | **●** | — | **●** | — | Pool, Sound, Events, Variables |
| ScreenFadeTransitionSO, UIValueAnimator, UISequentialScaler, VerticalGroupAnimator | attr only | **●** | ● (UIValueAnimator) | — | — | — | — |
| CoroutineHandler | — | — | — | — | — | (is it) | — |
| TimeMachine (e_Tick) | — | — | — | — | — | — | Events |
| SwipeSystem | — | — | — | **●** | — | — | — |
| FrameRateSetter / MultiTouchSetter | — | — | — | — | — | — | URP runtime |
| StudioSplash (optional) | `#if` guarded | — | — | — | — | — | — |

---

## 2. Architecture conventions (NumberixPuzzle)

These are the conventions the new project must follow (they become `CLAUDE.md` in Phase 1). Counts are grep results over `GameKit/` (611 .cs) and `Numberix/` (199 .cs); where two styles compete, the dominant one is given.

**Layers & assemblies.** Kit = `GameKit/<Module>/` compiled into `GameKit.Runtime` (asmdef, `rootNamespace: GameKit`, `autoReferenced: true`); editor code in per-module `Editor/` folders joined by `GameKit.Editor.asmref`. Game code lives outside `GameKit/` in `Assembly-CSharp` and **the kit never references the game**. The game plugs in via: a `GameState` subclass + `Transition` assets, prefabs in `Resources/` by string id, interface implementations, and config assets wired into kit state assets.

**Folder layout.** Feature folders with type sub-folders: `<Module>/{Scripts, Scriptables (SO instances), Prefabs (or Prefab/Resources for state views), Art, Events, Editor, Scenes}`. One screen = one folder `AppFlow/States/<Name>/` holding `<Name>State.cs` + `<Name>View.cs` + prefab + assets. SO *scripts* in `Scripts/`, SO *instances* in a sibling `Scriptables/` or `ScriptableObjects/`. Every module has a `<MODULE>.md` next to its code describing what the code *does* (index `GameKit/GAMEKIT_MODULES.md`). Build scenes under `<Game>/Content/Scenes/{Boot,SplashScene,Game}.unity`.

**Namespaces.** Kit: `GameKit.<Module>[.<Sub>]` (610/611 files; e.g. `GameKit.Architecture.Events`, `GameKit.Architecture.PoolSystem`, `GameKit.StateMachine`, `GameKit.UI`, `GameKit.Haptics`); vendored libs keep theirs (`THEBADDEST.Coroutines`, `THEBADDEST.SoundSystem`). Game: `<GameName>.<Area>` (Numberix uses `Puzzle.Config`, `Puzzle.Tutorial`, …; 34 game files are un-namespaced — a known inconsistency, don't repeat it). Editor: `<Root>.<Module>.EditorTools`.

**Naming.**
- Types: new ScriptableObjects end in `SO` (`ThemeServiceSO`, `LevelCatalogSO`, `*ConfigSO`, `*DefinitionSO`, `*AnchorSO`); exceptions are FSM `*State`/`*Transition` and the variable types (`Int`, `DBInt`). Suffixes in use: `State`, `View`, `Controller`, `Handler`, `Service[SO]`, `Manager`, `Presenter`, `Bootstrap`, `Runner`, `Mono` (scene host for an SO service), `Panel`, `Widget`. Interfaces `I*`. Leaf MonoBehaviours often `sealed`.
- Fields: **`[SerializeField] private camelCase`** (1986 vs 126 with underscore) — e.g. `[SerializeField, Required] private GridManager gridManager;`. Non-serialized private runtime fields `_camelCase` (363 vs 232). Public fields only on data-bag SOs/`[Serializable]` DTOs (`public int MaxLives = 5;`). Constants **PascalCase** (`private const string LogPrefix = "[LevelManager]";`), `static readonly` for non-const. Never `m_`.
- Properties expose serialized refs via expression bodies: `public Button SupportButton => supportBtn;`; mutable state `{ get; private set; }`. C# events `public event Action OnXxx` (kit) / `event Action Xxx` (game), invoked with `?.Invoke(`; handlers `HandleXxx` / `OnXxxClicked`. Bool queries `Is/Has/Can/Try…`.
- Asset prefixes: `e_` GameEvent, `v_` variable, `vDBInt_`/`vDBBool_`/`vDBString_` persistent variable, `t_` transition, `TD_`/`BD_` definitions. `CreateAssetMenu(fileName = "e_", menuName = "GameKit/<Module>/<Thing>")` for kit, `"<Game>/<Area>/<Thing>"` for game.
- Editor `MenuItem` roots: `GameDistrict/<Module>/<Action>` (kit), `Tools/<Game>/<Action>` (game).

**Attributes & inspector.** Plain Unity first (`[Header]` 610, `[Tooltip]` 364 — long, multi-line, explain *why* and name the asset to wire; `[Range]`, `[Min]`), Odin for grouping/buttons (`[Button]` 188 — debug/author actions on SOs; `[Title]` preferred over `[Header]` in game code; `[Required]` on must-wire refs; `[FoldoutGroup]`, `[BoxGroup]`, `[ShowIf]`, `[ReadOnly]`, `[InfoBox]`). Stay on plain `MonoBehaviour`/`ScriptableObject` — `Serialized*` bases are rare. `[DefaultExecutionOrder]` on bootstraps; `[NonSerialized]` on SO runtime caches.

**Code style.** Allman braces, 4 spaces, class indented inside namespace. Heavy `/// <summary>` XML docs with `<para>` explaining intent and trade-offs; `//` comments explain traps, not lines. `var` for obvious locals, explicit types for Unity structs/loops. `== null` checks (not `is null`), `?.` for optional SO refs. Coroutines (`IEnumerator`, `CoroutineHandler.StartStaticCoroutine`) are the async model — `async/await` is essentially absent. DOTween with `.SetLink(gameObject)`. Logging: raw `Debug.Log*` with a bracketed class prefix and `this` as context — `Debug.LogError("[LevelManager] No catalog assigned.", this);` — no logger wrapper, no `print`. File order: fields → properties → Unity lifecycle → public API → private helpers; sections marked with `// ── Lifecycle ─────` box-drawing comments (`#region` is rare).

**How systems talk (preferred order).**
1. **SO `GameEvent` channels** for cross-system signals (312 field refs); the pure model is *handed* channels (`boardModel.BindEventChannels(onStepAddedSO, …)`).
2. **SO variables** as the seam across assemblies (`Float SceneLoadingProgress` written by game, read by kit).
3. **Anchor SOs** for runtime object handles: `Value { get; private set; }`, `IsSet`, `Set()` in owner's `OnEnable`, `Clear()` in `OnDisable` (`Numberix/Game/ScriptableObjects/Scripts/Anchors/*AnchorSO.cs`) — replaces `FindObjectOfType`, which the project bans.
4. **Direct `[SerializeField]` references inside one prefab**, with `Awake` `GetComponent` fallbacks.
5. **Manual `Construct(...)`/`Initialize(...)` injection** from a scene `*Bootstrap` MonoBehaviour (`GameKit/Boosters/Core/Scripts/BoosterHudBootstrap.cs`) — SO services get their MonoBehaviour lifecycle from a bootstrap that subscribes events and re-asserts them.
6. **C# events** for tight intra-feature coupling. `UnityEvent` and static singletons are avoided (only `MainThreadDispatcher.Instance` exists).

**Config & data.** Tuning in SO assets with `[Header]/[Title]` groups and tooltips; game configs are `*SO` with `[SerializeField] private` + getters (`PathDragFeelSO`, `BoardCameraFitConfigSO`). Persistence = `DB*` variables → PlayerPrefs (plus raw PlayerPrefs blobs in LiveOps). Remote config (Firebase) overwrites SO fields and broadcasts `OnRemoteFetchedCompleted` — not needed for the pitch.

**Gameplay shape.** Façade over a pure model: `SinglePathBoardModel` (plain C#, unit-tested) bound to SO channels; `SinglePathGameController` adapts input → model; `SinglePathJuicePresenter` turns the same events into feedback (sound/haptics/particles). Our equivalent: `ShiftModel`/`BayModel` (pure C#) ← `CrateInteractionController` → `JuicePresenter`.

**Testing.** NUnit edit-mode tests in `<Game>/Editor/Tests` (`Assembly-CSharp-Editor`), class `<Subject>Tests`, methods `Scenario_ExpectedOutcome`, nested fakes implementing ports, `ScriptableObject.CreateInstance` in `SetUp`/destroy in `TearDown`. Pure model/geometry/config logic is what gets tested; nothing in `GameKit/` has tests.

**Known inconsistencies to not carry over:** mixed `SO` suffix (48 vs 56), mixed private-field underscore, mixed const casing, five different `MenuItem` roots, stale module docs still citing `Blues.Core.*`/`ProjectCore.*` namespaces, VContainer referenced by asmdefs but unused.

---

## 3. First-person controller (Silly-Justice)

SJ facts: no namespaces, no asmdefs (everything in Assembly-CSharp), `CharacterController`-based, all in `Update`, camera is a **child of the player root** (no Cinemachine — the package is installed but referenced by zero scripts), heavy XML docs, `[SerializeField] private _camelCase` fields, `*SO` configs deriving from `DescriptionBaseSO`, SO events accessed through a per-scene `DataManager.EventsOrNull?.OnX` hub.

### 3.1 Core controller — `Scripts/FirstPerson/` (13 files, clean)

| File | Responsibility | External SJ deps |
|---|---|---|
| `FirstPersonController.cs` | `[DefaultExecutionOrder(-10)]` orchestrator: `GetComponent<IMoveInput/ILookController/ICharacterMotor>()` in Awake; Update → `look.Look(input.Look)`, `motor.Move(input.Move, sprint, crouch)`, `Jump()`; cursor lock toggle; pause counter | `DataManager.EventsOrNull?.OnGamePaused` (1 line) |
| `Movement/CharacterMotor.cs` + `ICharacterMotor.cs` | `[RequireComponent(CharacterController)]`; `MoveTowards` accel, gravity, coyote jump, grounded stick force, crouch height lerp | `MovementConfigSO` |
| `Look/FirstPersonLook.cs` + `ILookController.cs` | yaw on body / pitch on `_cameraPivot`, clamp, optional smoothing & yaw limit; held-item auto-aim lock (SJ-specific, strip); `AimTowards(worldPoint, t)`, `SetPitch()` | `LookConfigSO`, `HeldLookConfigSO`, `DataManager.EventsOrNull?.OnHeldItemChanged` |
| `Look/CameraBobController.cs` | idle/walk/run bob blended by speed, `Kick(amplitude, duration)` jolt | `CameraBobConfigSO` |
| `Input/IMoveInput.cs` | `Vector2 Move, Look; bool SprintHeld, CrouchHeld, JumpPressed` — **the seam** | — |
| `Input/FirstPersonInputProvider.cs` | desktop: polls `Keyboard.current` + `Mouse.current.delta` | Input System |
| `Input/MobileMoveInputProvider.cs` | Move = `VirtualJoystick.Value`, Look = `TouchLookArea.Delta` (+ mouse delta when `_allowKeyboardMouse`), keyboard fallback | `VirtualJoystick`, `TouchLookArea` |
| `Config/{Movement,Look,CameraBob,HeldLook}ConfigSO.cs` | tunables; assets `ScriptableObjects/FirstPerson/FP_*Config.asset` (walk 4.5, sprint 7.5, gravity 20, pitch −45..65, `LimitYaw` **on ±80°** — turn off for a walk-around room) | `DescriptionBaseSO` |

Input is **direct device polling + uGUI pointer events**. `InputSystem_Actions.inputactions` is the untouched Unity template and is referenced by no script (only the `InputSystemUIInputModule` on `Prefabs/UICanvas/EventSystem.prefab`). Hands (`Animation/*`, Humanoid FBX + `Hands_Controller`) are optional; without them `HandInteractor` falls back to a timer (`ActionFallbackSeconds`, set ≈0.05) or a 10-line `IHandActionCues` stub.

### 3.2 Mobile touch — `Scripts/UI/Controls/` ✅ already supports touch
- `TouchLookArea.cs` — a **full-screen** transparent `Image` placed *first* in the canvas so every later sibling wins the raycast; tracks one `pointerId`, accumulates `eventData.delta` into `Delta` (cleared in `LateUpdate`); detects a tap (≤0.25 s, ≤20 px) → `event Action<Vector2> Tapped`. So look = "drag anywhere that isn't a control", not strictly right-half — matches the GDD well enough; a right-half rect is a one-line anchor change.
- `VirtualJoystick.cs` — uGUI `IPointerDown/Drag/Up`, normalised `Value`, dead zone, `_floating`, `_hideUntilTouched`; prefab uses a fixed stick bottom-left. Implements `ISteerStick` (copy the 10-line interface or drop it).
- Touch-stealing is solved purely by **EventSystem raycast ordering + per-control pointerId ownership** — buttons are uGUI Buttons that own their own finger. Works with mouse in the Editor unchanged (uGUI pointer = touch); flip `_allowKeyboardMouse` for WASD + raw mouse look.
- `TouchGate.cs`, `HeldItemButtonSwitcher.cs` are projectile-flow specific — skip.
- Canvas: Screen Space Overlay, `CanvasScaler` 1920×1080 match 0.5; EventSystem uses `InputSystemUIInputModule`.

### 3.3 Interaction — `Scripts/FirstPerson/Interaction/` 🔧 high reuse
- **Targeting:** `HandInteractor.FindGrabbableAlongRay` — `Physics.RaycastNonAlloc` from the camera **forward from screen centre**, `PickRange` 10 / `PickMask` (config asset `FP_InteractionConfig`), hits sorted, `GetComponentInParent<IGrabbable>()`, `CanGrab` veto via `IGrabGate`. Also `RequestPickAt(screenPos)` for tap-to-pick (`WorldTapPicker` ← `TouchLookArea.Tapped`).
- **Flow:** `UpdateFocus()` highlights the target; `IInteractInput.ConsumePick()/ConsumeThrow()` latch model fed by `InteractionInputProvider` (E/Q keys + public `RequestPick()/RequestThrow()`) and `InteractionButtonBinder` (uGUI Button → `RequestPick`). On grab: `held.OnGrabbed(holdPoint)`.
- **Attachment (`GrabbableItem`, ~1090 lines, ~60% web-strand FX):** `OnGrabbed` → kinematic, lerp to `holder.TransformPoint(offset)` over 0.18 s, **`SetParent(holder)`** + local offsets + scale pop — exactly KICKOFF's "parent to hold point, no physics". `OnReleased(velocity)` un-parents and re-enables physics (usable for the junk throw). Keep ~150 lines.
- **Highlight (`HighlightableItem`)**: stencil outline via `Shaders/FirstPersonOutlineMask.shader` + `FirstPersonOutlineFill.shader` (hand-written URP HLSL, fine), materials `GameData/FirstPerson/Resources/FP_Outline{Mask,Fill}.mat`, config `FP_HighlightConfig` (cyan, width 8, pulse). Bakes smoothed normals into mesh UV3 at runtime (fine for cubes).
- **Missing, must be written:** a **Place** branch (when holding, raycast for a slot target and parent to the slot) and a **context button** whose label follows focus state (Pick up / Place / Scan / Launch). `HeldItemButtonSwitcher` only knows held/not-held.

### 3.4 Other SJ systems worth taking
- **`_AhmedIkram/CameraShake/`** — `CameraShakeProfileSO` (Perlin, decay curve) + `CameraShaker` applying offsets inside `RenderPipelineManager.beginCameraRendering`/`endCameraRendering` so nothing else sees the shake; up to 4 stacked; `Play(profile)`. One `DataManager` line to replace. ✅ **Use for launch shake** (kit has nothing equivalent).
- **`_AhmedIkram/PropShop/Scripts/Ads/`** — `IRewardedAdService { bool IsReady; void Show(string placement, Action<RewardedAdResult> onFinished); }`, `RewardedAdResult { Completed, Skipped, Failed }`, `FakeRewardedAdService : MonoBehaviour` (real-time fake duration, optional overlay, `OnDisable` fails pending callback), `RewardedAdResolver`. Zero deps. ✅ **This is KICKOFF's mock 2× ad — copy verbatim.**
- `Scripts/Gameplay/Throwing/TrajectoryPreview.cs` (53 lines, LineRenderer ballistic arc) ✅ for the junk-throw ghost arc. `GameFlow/Tutorial/TutorialAimAssist.cs` (50-line velocity homing in FixedUpdate) 🔧 template for "assisted arc into the chute".
- `Scripts/UI/TaskBriefTickerUI.cs` (typewriter/ticker text) 🔧 basis for Rook radio lines. `Scripts/Gameplay/FX/ComicPopup.cs` (billboard text pop, no deps) ✅ for "Added to collection"/news-alert style pops. `Scripts/Gameplay/Impact/PropShake.cs` (DOTween punch) ✅ crate wobble. `_AhmedIkram/DevTools/FpsCounter.cs` ✅. `_AhmedIkram/Settings/GraphicsQuality*` 🔧 optional.
- SJ's own `Scripts/Audio/SoundManager`, `_AhmedIkram/Haptics/HapticHandlerSO` (same name and `HapticIntensity` enum as the kit's), `Core/Events`, `Core/Variables`, `Gameplay/Economy` **duplicate kit systems — do not port**; use the kit's.
- **Throwing:** SJ has **no swipe-to-throw** — throws are a button press that hands off to a guided-missile flight (`ThrowController` 672 lines, `ProjectileController`, `GuidedThrowBridge`, slow-mo, chase cam). Not worth porting. The kit's `SwipeSystem` + `GrabbableItem.OnReleased(velocity)` + a ballistic solve covers the junk throw.

### 3.5 Player prefab
`Prefabs/Controller/FPSPlayer.prefab` root: `CharacterController`, `CharacterMotor[FP_MovementConfig]`, `FirstPersonLook[FP_LookConfig, pivot=Main Camera]`, `FirstPersonController`, `InteractionInputProvider`, `HandInteractor[FP_InteractionConfig, aim=Main Camera, holdPoint]`, `MobileMoveInputProvider`, `WorldTapPicker`, `PickupVfx` + 12 flight/shop scripts; child `Main Camera (0, 1.522, 0)` with `Camera`, `AudioListener`, `UniversalAdditionalCameraData`, `CameraBobController`, `CameraShaker` + flight FX; `HoldPoint` is parented to the hand bone of the nested Humanoid hands FBX. Joystick/look-area references are overridden in `Prefabs/Full Controller/Controller And UI.prefab`. **Too entangled to copy — rebuild a lean prefab from the scripts** (root + camera child + `HoldPoint` child of the camera).

### 3.6 Minimal port list (ordered)
**A. Move + look + touch (17 files, no other SJ code):** `Scripts/Core/Base/DescriptionBaseSO.cs`; `FirstPerson/Movement/{ICharacterMotor,CharacterMotor}.cs`; `FirstPerson/Config/{Movement,Look,CameraBob}ConfigSO.cs`; `FirstPerson/Look/{ILookController,FirstPersonLook,CameraBobController}.cs`; `FirstPerson/Input/{IMoveInput,FirstPersonInputProvider,MobileMoveInputProvider}.cs`; `UI/Controls/{VirtualJoystick,TouchLookArea}.cs`; `Core/Interfaces/ISteerStick.cs`; `FirstPerson/FirstPersonController.cs`; assets `ScriptableObjects/FirstPerson/FP_{Movement,Look,CameraBob}Config.asset`.
**B. Interaction (pick → hold point):** `Interaction/{IGrabbable,IGrabGate,IInteractInput,InteractionConfigSO,HighlightConfigSO,HighlightableItem,GrabbableItem,InteractionInputProvider,InteractionButtonBinder,HandInteractor}.cs`; `Core/Interfaces/{IHighlightable,IHighlightTint}.cs`; `Shaders/FirstPersonOutline{Mask,Fill}.shader`; `GameData/FirstPerson/Resources/FP_Outline{Mask,Fill}.mat`; `FP_HighlightConfig.asset`, `FP_InteractionConfig.asset`. Optional `ITapConsumer.cs` + `WorldTapPicker.cs`.
**C. Extras:** `_AhmedIkram/CameraShake/{CameraShaker,CameraShakeProfileSO,GameEventWithCameraShake}.cs`; `_AhmedIkram/PropShop/Scripts/Ads/{IRewardedAdService,FakeRewardedAdService,RewardedAdResolver}.cs`; `Gameplay/Throwing/TrajectoryPreview.cs`; `Gameplay/FX/ComicPopup.cs`; `Gameplay/Impact/PropShake.cs`; `_AhmedIkram/DevTools/FpsCounter.cs`; `Prefabs/UICanvas/EventSystem.prefab`.

### 3.7 Dependencies to swap (SJ → kit)

| SJ construct | Where | Replace with |
|---|---|---|
| `DataManager.EventsOrNull?.OnGamePaused` (`GameEventWithBool`) | `FirstPersonController` | `[SerializeField] GameKit…GameEventWithBool onGamePaused` or delete |
| `DataManager.EventsOrNull?.OnHeldItemChanged`, `OnItemPicked`, `OnThrownItem`, `OnThrew` | `FirstPersonLook`, `HandInteractor` | kit `GameEvent`/`GameEventWithBool` serialized fields (shape-identical API: `Subscribe/UnSubscribe/Invoke/Raise`) |
| `OnSfxRequested` + `SfxRequest/SfxId/SfxClipSet/AmmoSfxConfig/AmmoPickup` | `HandInteractor` | `soundService.Play("Pickup")` |
| `OnCameraShakeRequested` | `CameraShaker` | serialized `GameEventWithCameraShake` or direct `Play()` |
| `DescriptionBaseSO` | every `*ConfigSO` | copy (15 lines) or re-base on `ScriptableObject` |
| `HeldLookConfigSO` + auto-aim lock | `FirstPersonLook` | delete branch |
| `IHandAnimator`/`IHandActionCues` + hands FBX | `HandInteractor` | immediate-cue stub or `ActionFallbackSeconds ≈ 0.05` |
| `PickupFxConfigSO`, strand/tail FX, respawn | `GrabbableItem`, `PickupVfx` | delete |
| global namespace, `_camelCase` serialized fields | all | `OrbitalCargo.FirstPerson`, kit field style |

---

## 4. Compatibility risks

Correction to an assumption in KICKOFF: `activeInputHandler: 2` in both projects means **"Both"** (0 = old, 1 = new, 2 = both), not "Input System only". The new URP template defaults to Input System only; that is fine — zero legacy `UnityEngine.Input` calls exist in the files we copy (the single SJ offender, `Gameplay/Impact/ImpactTester.cs`, is out of scope; `MultiTouchSetter.cs` uses `Input.multiTouchEnabled`, which is a property, not polling — still verify it compiles).

| # | Risk | Severity | Evidence | Resolution |
|---|---|---|---|---|
| 1 | **`GameKit.Runtime` is not self-contained** — 7 folders outside `GameKit/` are asmref-merged into it (`Adjust`, `GDMonetization` 175 cs, `Metica`, `GameContent`, `Scripts`, `SDKsEssentials`, `Numberix/Content`), and 16 kit files `using Monetization.Runtime.*`, plus AppMetrica/Adjust/Metica call sites | **Blocker** (compile) if the whole folder is copied | `NX/Assets/*/GameKit.Runtime.asmref`; `GameKit.Runtime.asmdef` references `AppMetrica, Metica.*, MaxSdk.Scripts, ParticleImage, MoreMountains.Tools, VContainer, Unity.VisualScripting.*, UnityEngine.Purchasing.*, Unity.Services.Core, Unity.Addressables` | **Copy a curated core subset (see §5), not the whole `GameKit/`.** Rewrite the asmdef references down to `Coroutines, UnityEngine.UI, Unity.TextMeshPro, Lofelt.NiceVibrations, DOTween.Modules, Unity.InputSystem, Unity.RenderPipelines.Universal.Runtime`. |
| 2 | Core kit files reference **LiveOps/Leaderboard** types (`MetaLoader`, `GameConfig`, `LevelManager`, `FirebaseRemoteFetcherSO`, `SplashState`, `HomeState/View`, `RemoveAds*`, all of `Rewards/`, `Collection/`, `Shop`) | Blocker if those files are copied | ~20 files listed in agent evidence | None of them are in the §5 copy set. |
| 3 | **Odin Inspector 3.1 (paid)**: 150/611 kit files (59 outside LiveOps/Leaderboard), only 8 `#if ODIN_INSPECTOR` guards; hard runtime uses in core: `FiniteStateMachine : SerializedScriptableObject`, `SharedDictionary : SerializedScriptableObject` | **Blocker unless licensed** | `NX/Assets/Plugins/Sirenix` (defines `ODIN_INSPECTOR;ODIN_INSPECTOR_3;ODIN_INSPECTOR_3_1`) | **Decision for Awais:** (a) the studio has a licence → copy `Plugins/Sirenix` (incl. `Assemblies/link.xml`, `Odin Inspector/Config`) and keep the 3 defines; or (b) strip — in the §5 subset that's ~25 files of attribute deletions + re-basing `FiniteStateMachine` on `ScriptableObject` (skip `SharedDictionary`), ~2 hours. Recommend (a) if licensed, else (b) — `[Button]` debug buttons are the only real loss. |
| 4 | **NX renders with the URP 2D Renderer**; GraphicsSettings default slot is empty. SJ's active renderer points `postProcessData` at a URP **package test asset**; both `DefaultVolumeProfile`s carry missing-script test components | Major if copied | `NX/Assets/Settings/UniversalRP.asset → Renderer2D.asset`; `SJ/Assets/Settings/Mobile_Renderer.asset` | Copy **neither** `Settings/` folder. Keep the template's Universal Renderer and set mobile values by hand: render scale 0.8, MSAA off, HDR off, main-light shadows on at 1024 (we want crate shadows in a lit room — reassess on device), SRP Batcher on, no Decal/SSAO. |
| 5 | **Duplicate `Lofelt.NiceVibrations` asmdef** if both NiceVibrations folders are copied | Blocker (compile) | both v4.1.2 / SDK 1.3.4, identical `Scripts/` and `.aar` | Copy SJ's `_AhmedIkram/Plugins/NiceVibrations` only (lean: no Demo/OlderVersions/`Define` script that needs `MoreMountains`). |
| 6 | DOTween versions differ; kit asmdef requires `DOTween.Modules` asmdef that only NX has; settings differ (NX safe mode off) | Minor | SJ 1.2.760/Pro 1.0.380, NX 1.2.765/Pro 1.0.381, `NX/Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef` | Copy NX's `Plugins/Demigiant` (drop `DOTweenPro Examples`), run DOTween Setup, enable UI + Audio modules, keep `DOTWEEN` define. |
| 7 | `BuildReportTool` exists in both (SJ 3.11.9 with asmdef, NX 3.9.4 without) → duplicate types | Blocker if both copied | `*/Assets/BuildReport` | Copy neither. |
| 8 | **Kit prefabs reference 377 assets outside `GameKit/`** (326 in `Numberix/` — fonts `Praxis-Black.asset` ×69, skins, 31 scripts; 34 in `SDKsEssentials`) → missing refs/missing scripts | Major if `AppFlow/States`, `Art/UI`, `LiveOps` prefabs are copied | guid scan | Not in the §5 subset. For `UICollector`/`CoinsWidget` prefabs we build our own prefabs (the scripts are clean); bring one TMP font (`Numberix/Content/Fonts/New/Praxis-Black.asset` + material) or use TMP's LiberationSans. |
| 9 | **Pink materials**: only 2 in `GameKit/` (`Art/Particles/IceExplosion/Materials/PolygonParticleFX_Ice_01.mat` = Built-in Standard; `Art/Particles/HeavyRealisticExplosion/Mirza Beig/.../*realisticSmokeBurst*.mat` = built-in custom shader). 20 legacy `fileID 200/211` particle mats still render in URP. SJ FP/controls prefabs: 0 pink. `Feel/` ships built-in/HDRP shaders (not copied) | Minor | material scan | Run Render Pipeline Converter on the first; swap the second to `URP/Particles/Unlit` — or just don't copy those two FX folders. |
| 10 | **SJ FP code is not self-contained**: transitive closure of `FirstPerson/` + `UI/Controls/` = 174 of 259 SJ scripts, via `DataManager` → `GameEvents` hub and `HandInteractor` → `ThrowController` | Major | 6 `DataManager.EventsOrNull` sites, `HandInteractor.cs` throw/SFX refs | Copy only the §3.6 list and patch the swap table (§3.7). Rebuild the player prefab. |
| 11 | **Android gradle templates collide and are SDK-specific** (SJ: AGP 8.10, Unity Ads; NX: manifest with Adjust receiver + AD_ID permission, 40 SDK deps, Firebase/GMA androidlibs) | Major (build) | `*/Assets/Plugins/Android` | Copy none; the pitch build has no SDKs. |
| 12 | **Portrait vs landscape**: all kit UI prefabs are authored for portrait canvases | Major (UX) | `NX ProjectSettings` autorotate portrait only | We author every canvas ourselves (1920×1080, match 0.5 as in SJ). |
| 13 | Project identity/keystore leakage (`com.gdm.numberix`, `PlayblissKeystore`, `com.GDMain.SillyJustice`) | Minor | `ProjectSettings.asset` | Never copy `ProjectSettings/`. New identifier. |
| 14 | VContainer / Visual Scripting / Addressables / Roslyn DLLs / Feel / Newtonsoft are referenced by the asmdef or manifest but unused by the core subset | Minor | asmdef + 7 stray `using Unity.VisualScripting;` | Don't add those packages; delete stray usings as they surface. |
| 15 | Scripting backend/arch: SJ IL2CPP+ARM64, NX default Mono + ARMv7/ARM64; NX uses Swappy + GameActivity entry | Minor | `ProjectSettings.asset` | IL2CPP + ARM64 for the Android build (Mono is fine for fast iteration). If Odin is kept with IL2CPP, keep its `link.xml`. |
| 16 | Unity 6000.3.6f1 → 6000.3.15f1 | Minor | `ProjectVersion.txt` | Create the project on 6000.3.15f1 (SJ's version, already installed). |
| 17 | Kit `Resources.Load` string ids (`"UIStates"`, `"GameFlow"`, `"SoundSettings"`) and `AppBase` boot expectations | Minor | `SplashState.cs`, `SoundExtensions.cs` | Our own bootstrap + `UIStates` root prefab; create a `Resources/SoundSettings.asset`. |

---

## 5. Recommendation

**Strategy: build a clean `GameKit.Runtime` core subset (not the whole folder) + a lean FP port, on a fresh Universal 3D template project.** Copying `GameKit/` wholesale would pull in 7 SDK folders, Firebase/ads/analytics stubs, LiveOps type leaks and ~377 dangling Numberix references — days of cleanup we don't have before Monday. The subset below compiles against only DOTween, NiceVibrations, TMP, Input System and URP, and preserves every convention and system KICKOFF asks us to reuse.

### 5.1 Copy order

**Step 0 — project.** Unity 6000.3.15f1, Universal 3D template, Android, landscape, Input System only, Gamma (both sources are Gamma). Set identifier/company. Git init.

**Step 1 — plugins (ask-before-adding applies; these are the only ones).**
1. `NX/Assets/Plugins/Demigiant/` → `Assets/Plugins/Demigiant/` (minus `DOTweenPro Examples`). Run DOTween Setup; `DOTWEEN` define.
2. `SJ/Assets/_AhmedIkram/Plugins/NiceVibrations/` → `Assets/Plugins/NiceVibrations/`.
3. **If licensed:** `NX/Assets/Plugins/Sirenix/` → `Assets/Plugins/Sirenix/` + the 3 Odin defines. Otherwise strip attributes in step 2.
4. Packages: Input System and URP come with the template; TMP via `com.unity.ugui`. **No** VContainer, Addressables, Cinemachine, Feel, Visual Scripting.

**Step 2 — GameKit core subset** → `Assets/GameKit/` (keep the module folder names so docs/conventions carry over; copy each module's `.md` too):
- `GameKit.Runtime.asmdef` (rewrite `references` to `Coroutines, UnityEngine.UI, Unity.TextMeshPro, Lofelt.NiceVibrations, DOTween.Modules, Unity.InputSystem, Unity.RenderPipelines.Universal.Runtime`), `Editor/GameKit.Editor.asmdef` (references `GameKit.Runtime, Coroutines, Unity.TextMeshPro, UnityEngine.UI, DOTween.Modules`).
- `Architecture/Utilities/CoroutineHandler-master/` (whole, own asmdef).
- `Architecture/Utilities/Events/`, `Architecture/Utilities/Variables/` (skip `SharedDictionary*.cs`, dead `Helper/KeyRegistry.cs`), `Architecture/Utilities/PoolSystem/` (skip `PoolMonitor.cs`), `Architecture/Utilities/SoundSystem/`, `Architecture/Utilities/SwipeSystem/`, `Architecture/Utilities/TimeSystem/Scripts/TimeMachine.cs` + `Scriptables/e_Tick`, `Architecture/Utilities/UIValueAnimator.cs`, `Architecture/Utilities/CustomEditorTools/`.
- `Haptics/` (script + `HapticHandlerSO.asset`) and `Art/Sounds/Scriptables/{Sound,Music,Vibrations}.asset` toggles + their `e_` events.
- `StateMachine/StateMachine/Scripts/*`, `StateMachine/Base/Scripts/*` (skip dead `UiConfig/UiAnimations/AnimationEventHandler`), `StateMachine/GameFlow/Scripts/{BaseApplicationFlowController,StateRootManager}.cs`, `AppFlow/States/Game/GamePlay/Scripts/GameState.cs`, `AppFlow/GameFlow/Prefabs/Resources/UIStates.prefab`.
- `UI/Animations/CoinTrail/Scripts/UICollector.cs`, `UI/ScreenTransition/ScreenFadeTransitionSO.cs`, `Art/UI/Scripts/{Header/*, UISequentialScaler.cs, UISequentialScaleConfigSO.cs}`, `AppFlow/Architecture/View/Scripts/VerticalGroupAnimator.cs`.
- `AppFlow/GameFlow/Scripts/{FrameRateSetter,MultiTouchSetter}.cs`.
- FX prefabs that render in URP: `Art/Particles/{ConfettiRainbow,GoldenHit,StarExplosion,StarTrail,ShopCoinsStars}` (for coin pops / launch).
- Optional: `StudioSplash/` (dependency-free) if a studio splash is wanted in the pitch build.
- One TMP font: `Numberix/Content/Fonts/New/Praxis-Black.asset` (+ its material) into `GameKit/UI/fonts/`, or use TMP defaults.
- **Fix-ups expected:** delete stray `using Unity.VisualScripting;` (`UICollector.cs`), delete `using Sirenix…` if Odin is dropped, re-base `FiniteStateMachine` on `ScriptableObject` if Odin is dropped, create `Resources/SoundSettings.asset` + an `AudioMixer` with `volume/sfx/ui/music` params.
- **Not copied (and why):** `AppFlow/States/*` (portrait screens, SDK-coupled), `AppFlow/GameFlow/Scripts/{AppBase,GameFlow,BannerController}.cs` (Firebase/IAP/ads), `Architecture/AdPackage`, `Analytics`, `Architecture/FirebaseRemoteArchitecture`, `Architecture/Design Patterns`, `Architecture/Learning`, `Architecture/Utilities/Json`, `Boosters`, `Collection`, `Rewards`, `Economy` (LiveOps-coupled; we write `CreditsService`), `Core/Fail`, `Core/DI`, `LevelSystem`, `LevelCompleteFeedback`, `LiveOps`, `Leaderboard`, `MetaLoader`, `Tutorials`, `UI/Onboarding`, `UI/Theme`, `Config`, `InApp`, `Art/*` except the listed particles.

**Step 3 — FP port** → `Assets/OrbitalCargo/FirstPerson/` (game assembly), following §3.6 A→B→C and the §3.7 swap table. Add namespace `OrbitalCargo.FirstPerson`, convert serialized fields to kit style, replace `DataManager.EventsOrNull` with kit `GameEvent` fields, strip held-look lock / hands / SFX / strand FX. Rebuild `FPSPlayer` prefab (root + `Main Camera` child + `HoldPoint` child of camera) and a landscape `Controls` canvas (TouchLookArea first → joystick bottom-left → context button bottom-right → crosshair → HUD). Keep `_allowKeyboardMouse` on in the Editor via a `#if UNITY_EDITOR` default so WASD + mouse work alongside touch.

**Step 4 — extras from SJ:** `CameraShaker` + profile SO, `FakeRewardedAdService` trio, `TrajectoryPreview`, `ComicPopup`, `PropShake`, `FpsCounter`, `EventSystem.prefab`.

**Step 5 — verify in Editor:** compile clean, walk around a greybox room with mouse/keyboard and with Device Simulator touch, play a kit sound, fire a haptic (no-op in Editor), pool a particle. Commit. (This is Phase 2 slice 1.)

### 5.2 What to adapt vs. write new (preview for PLAN.md)
- **Adapt:** `HandInteractor` (+Place branch, context-button state), `GrabbableItem` (trimmed → `Crate` carries it), `TouchLookArea` (+swipe event or feed the kit `SwipeDetector`), `UICollector` (coin pops to the HUD credits), `CoinsWidget` (credits), `TaskBriefTickerUI` → `RadioLineView`.
- **New (game assembly `OrbitalCargo.*`):** `CrateTypeSO`, `ShiftConfigSO` (all tunables), `Crate`/`CrateVisuals`, `Bay`/`Slot`, `ShuttleLauncher`, `Conveyor` (kinematic path + queue), `CrateSpawner` (schedule), `ScannerBooth`, `Chute`, `CreditsService`, `ShiftController` (timer + state), `UpgradeService` (grav-sled), `GameFlow` (our 3-state flow), `HudView`, `ShiftSummaryView`, `RookRadio`.

### 5.3 Decisions needed from Awais before Phase 1
1. **Odin Inspector** — does the studio licence cover the new project (copy `Plugins/Sirenix`), or should the kit subset be stripped of Odin?
2. **Studio splash** — include `StudioSplash/` in the pitch build, or skip?
3. Confirm the **core-subset approach** (vs. copying all of `GameKit/` and stubbing SDKs).
