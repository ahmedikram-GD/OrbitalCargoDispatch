# CLAUDE.md — Orbital Cargo Dispatch (pitch prototype)

Unity 6000.3.15f1 · URP (Universal 3D template, mobile settings) · Android landscape · Input System only · Gamma. Deadline: pitch **Monday 2 November 2026, evening**.

## Read first
- `KICKOFF.md` — scope, phases, technical rules (authoritative).
- `docs/PLAN.md` — slices, tasks, interfaces, acceptance criteria. Work slice by slice; commit after each.
- `docs/ANALYSIS.md` — what was copied from where and why; swap tables for the FP port.
- `docs/GDD.md` — intent and tone only; build what KICKOFF asks.
- Kit module docs live next to the code: `Assets/GameKit/<Module>/<MODULE>.md`, especially `Assets/GameKit/StateMachine/STATE_MACHINE.md` (19 rules) and `Assets/GameKit/Architecture/ARCHITECTURE.md`.

## Hard rules
- **Source projects are READ-ONLY.** Never create, modify, move or delete anything under `D:\Projects\Silly-Justice` or `D:\Projects\NumberixPuzzle`. Copy out of them only.
- **Ask Awais before**: adding any package/plugin (only DOTween + NiceVibrations + Odin are approved), changing project-wide settings beyond PLAN.md, or anything that would touch the source projects.
- **No ad/analytics SDKs.** Rewarded ad is the mock `FakeRewardedAdService`.
- **No physics carrying, no physics conveyor.** Raycast + parent to hold point + snap to slot; conveyor moves crates in code.
- **All tuning lives in ScriptableObjects** (`ShiftConfigSO`, `CrateTypeSO`, `SpawnScheduleSO`, `MovementConfigSO`, `RoomLayoutSO`). No magic numbers in MonoBehaviours.
- **Mobile first, Editor friendly**: every input must work with touch on device and with mouse/keyboard in the Editor (WASD, mouse look, `E` interact, `Space` throw).
- **Unity MCP** is configured for `D:\Awais\Backup\Core Game Template`; remind Awais to repoint it at this project before relying on Editor access.
- Commit after each slice; messages end with the Co-Authored-By trailer for the model doing the work (currently `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).

## Key paths
```
Assets/GameKit/                 kit subset (GameKit.Runtime asmdef). Do not add game code here.
Assets/Plugins/Demigiant/       DOTween Pro (from NumberixPuzzle)
Assets/Plugins/NiceVibrations/  Lofelt NiceVibrations 4.1.2 (from Silly-Justice)
Assets/Plugins/Sirenix/         Odin Inspector (imported by Awais)
Assets/OrbitalCargo/            game code (Assembly-CSharp), see PLAN.md "File structure"
  Scenes/Game.unity             the only scene
  FirstPerson/ Interaction/ Crates/ Stations/ Economy/ Shift/ Flow/ UI/ Radio/ Juice/ Ads/
  ScriptableObjects/Scripts/    SO classes      ScriptableObjects/{Config,Crates,Events,Variables,States,Room}/  SO instances
  Prefabs/UI/Resources/         FSM view prefabs loaded by stateId
  Editor/Tools/                 RoomBuilder, asset factories (MenuItem "Tools/OrbitalCargo/…")
  Editor/Tests/                 NUnit edit-mode tests (Assembly-CSharp-Editor)
docs/                           ANALYSIS.md, PLAN.md, GDD.md
```

## Architecture (inherited from the GameKit)
- **Two layers, one direction:** `GameKit.Runtime` (`GameKit.*`) ← game (`OrbitalCargo.*`). The kit never references game code.
- **Everything shared is an asset, not a singleton.** State in SO variables (`Int/Float/Bool`, persistent `DBInt/DBBool/DBString` → PlayerPrefs). Signals via SO `GameEvent` channels. Services as ScriptableObjects (`SoundService`, `PoolManagerSO`, `HapticHandlerSO`, `CreditsService`, `UpgradeService`) wired in the inspector. Runtime object handles via `*AnchorSO` if ever needed. **No `FindObjectOfType`, no static `Instance`, no UnityEvents, no DI container.**
- **Gameplay = façade over a pure model.** Pure C# models (`BayModel`, `ConveyorQueue`, `SpawnScheduler`, `ShiftClock`, `PayoutRules`) are unit-tested; MonoBehaviours adapt them to the scene and raise events; `JuicePresenter` turns the same events into sound/haptics/FX. Don't sprinkle feedback calls through gameplay code.
- **UI = kit FSM (MVP).** `XxxState : UIViewState/GameState` (SO, the brain) binds a passive `XxxView : UIBase` in `OnBindView/OnUnbindView`. Views expose `Button` properties and never reference the state. States raise navigation `GameEvent`s; only `GameFlow : BaseApplicationFlowController<Transition>` drives the FSM. View prefab root = `Canvas` + `CanvasGroup` + the `UIBase` subclass; `stateId` = pool key = Resources name.
- **Events:** void channels `GameEvent.Invoke()`, typed channels `GameEventWithParam<T>.Raise(t)`. Subscribe in `OnEnable`/`Enter`, unsubscribe in `OnDisable`/`Exit` (and `Pause`), **method groups, never lambdas**. `GameEvent.OnDisable` wipes handlers, so ScriptableObjects don't subscribe to events themselves — a scene MonoBehaviour does and re-asserts.
- **Variables:** `DBInt/DBFloat/DBBool.SetValue/Increment` auto-save; `DBString.SetValue` does **not** — call `Save()`. Set `ResetToDefaultOnPlay` on non-persistent variables or play sessions dirty the asset.
- **Pooling:** `PoolManagerSO.Get(id)` returns `null` for an unknown id — always null-check. Release with `Release(id, go)` or `ReleaseAfterDelay`.
- **Audio:** `soundService.Play("Name")` — names are the keys in `Assets/OrbitalCargo/Audio/OcdSounds.asset`. Mixer params must be `volume/sfx/ui/music`.
- **Coroutines over async.** ScriptableObjects use `CoroutineHandler.StartStaticCoroutine`. DOTween tweens get `.SetLink(gameObject)`.

## Coding conventions (match the kit exactly)
- **Namespaces:** every file. Kit code `GameKit.<Module>`; game code `OrbitalCargo.<Area>` (`OrbitalCargo.FirstPerson`, `.Interaction`, `.Crates`, `.Stations`, `.Economy`, `.Shift`, `.Flow`, `.UI`, `.Radio`, `.Juice`, `.Ads`, `.Config`); editor code `OrbitalCargo.<Area>.EditorTools`; tests `OrbitalCargo.Tests`.
- **Types:** ScriptableObjects end in `SO` (`CrateTypeSO`, `ShiftConfigSO`) except FSM `*State`/`*Transition` and kit variable types. Suffixes: `State`, `View`, `Controller`, `Service`, `Presenter`, `Bootstrap`, `Model`, `Rules`. Interfaces `I*`. Leaf MonoBehaviours may be `sealed`.
- **Fields:** inspector fields `[SerializeField] private camelCase;` (no underscore). Non-serialized private runtime fields `_camelCase`. Public fields only on data-bag SOs / `[Serializable]` DTOs. Constants `PascalCase` (`private const string LogPrefix = "[Conveyor]";`), `static readonly` for non-const. Never `m_`.
- **Members:** expose serialized refs with expression-bodied getters (`public Button LaunchButton => launchButton;`); mutable state `{ get; private set; }`. C# events `public event Action<Crate> Picked;`, invoked `Picked?.Invoke(crate)`; handlers `HandleXxx` / `OnXxxClicked`. Bool queries `Is/Has/Can/Try…`.
- **Attributes:** plain Unity first — `[Header]`, `[Tooltip]` (long, explain *why* and name the asset to wire), `[Range]`, `[Min]`. Odin for author/debug actions and grouping: `[Button]`, `[Title]`, `[Required]` on must-wire refs, `[FoldoutGroup]`, `[ShowIf]`, `[ReadOnly]`. Stay on plain `MonoBehaviour`/`ScriptableObject`. `[DefaultExecutionOrder]` on bootstraps; `[NonSerialized]` on SO runtime caches.
- **Asset names:** `e_<Name>` events, `v_<Name>` variables, `vDBInt_/vDBBool_/vDBString_<Name>` persistent, `t_<Name>` transitions, `CT_<Name>` crate types, `HI_<Name>` hidden items, `Shake_<Name>` shake profiles, `FP_<Name>Config`. `CreateAssetMenu(fileName = "CT_", menuName = "OrbitalCargo/<Area>/<Thing>")`. `MenuItem` roots: `Tools/OrbitalCargo/<Action>`.
- **Style:** Allman braces, 4 spaces, class indented inside namespace. `/// <summary>` on every class explaining intent and trade-offs; `//` comments for traps, not line narration. Sections marked `// ── Lifecycle ─────`. `var` for obvious locals, explicit types for Unity structs/loops. `== null` checks, `?.` for optional SO refs. File order: fields → properties → Unity lifecycle → public API → private helpers.
- **Logging:** `Debug.Log*` with a bracketed class prefix and `this` as context: `Debug.LogError("[ShuttleLauncher] Bay has no shuttle.", this);`. No `print`, no logger wrapper.
- **Tests:** NUnit edit-mode in `Assets/OrbitalCargo/Editor/Tests`, class `<Subject>Tests`, methods `Scenario_ExpectedOutcome` (`TryGetNearestFreeSlot_WhenFull_ReturnsFalse`), nested fakes for ports, `ScriptableObject.CreateInstance` in `SetUp`, destroy in `TearDown`. Test pure models and SO config logic; MonoBehaviours are verified by the slice's play-test acceptance criteria.

## Workflow
- Follow `docs/PLAN.md` task by task; TDD for pure models (test → fail → implement → pass → commit); play-test each slice in the Editor (and Device Simulator) before moving on, then tell Awais what to test.
- Prefer editor tools (`RoomBuilder`, asset factories) over hand-placing scene objects so the room and configs are reproducible.
- When a kit file needs an edit, make the smallest change and note it in the commit; never fork a kit system when one already exists.
