# Orbital Cargo Dispatch — Pitch Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A playable ~5-minute scripted "pitch shift" of Orbital Cargo Dispatch on Android landscape (and in the Editor with mouse/keyboard), built on the NumberixPuzzle GameKit core and the Silly-Justice first-person controller, in 8 slices, each committed and runnable.

**Architecture:** `GameKit.Runtime` (curated kit subset: SO events, SO variables, pooling, sound, haptics, SO FSM + pooled uGUI views, coroutine handler, swipe) ← `Assembly-CSharp` game code in `Assets/OrbitalCargo/` namespaced `OrbitalCargo.<Area>`. Gameplay follows the kit's "façade over a pure model" shape: pure C# models (`BayModel`, `ShiftClock`, `SpawnScheduler`, `ConveyorQueue`, `PayoutRules`) are unit-tested in edit mode; MonoBehaviours adapt them and publish SO `GameEvent`s; a `JuicePresenter` turns the same events into sound/haptics/particles. No physics for carrying or conveying; raycast from screen centre + parent to hold point + snap to slot.

**Tech Stack:** Unity 6000.3.15f1, URP 17.3 (Universal 3D template, mobile settings), Input System only, uGUI + TMP, DOTween Pro (from NumberixPuzzle), Lofelt NiceVibrations 4.1.2 (from Silly-Justice), Odin Inspector (imported by Awais from his own package), NUnit edit-mode tests. No ad/analytics SDKs, no Addressables, no VContainer, no Cinemachine, no Feel.

**Spec:** `KICKOFF.md` (scope, slices, technical rules), `docs/GDD.md` (intent), `docs/ANALYSIS.md` (what to copy, from where, with what edits — §3.6/§3.7 FP port list and swap table, §5.1 kit copy list).

## Global Constraints

- Source projects `D:\Projects\Silly-Justice` and `D:\Projects\NumberixPuzzle` are **read-only**: copy out of them, never write into them.
- **Ask Awais before** adding any package/plugin beyond DOTween + NiceVibrations, changing project-wide settings beyond this plan, or touching the source projects.
- **Unity MCP** currently targets `D:\Awais\Backup\Core Game Template` — remind Awais to repoint it at `D:\Projects\OrbitalCargoDispatch` the first time Editor access is needed (Slice 1).
- **Odin**: kit code keeps its Odin attributes; Awais imports Odin before Slice 1 Task 1.3 (kit won't compile without it). Scripting defines: `DOTWEEN;ODIN_INSPECTOR;ODIN_INSPECTOR_3;ODIN_INSPECTOR_3_1`.
- Platform: Android, **landscape only**, Input System only, Gamma colour space, IL2CPP + ARM64 for the device build (Mono OK in Editor).
- **No physics carrying, no physics conveyor.** Crates are kinematic; movement is code-driven.
- **All tuning in ScriptableObjects**: shift length, spawn schedule, conveyor speed, cryo timer (45 s), scan duration (2.5 s), payouts per crate type, chute bonus, fine amount, walk speed (`MovementConfigSO.WalkSpeed`), volatile slowdown multiplier.
- **Data-driven crates**: one `CrateTypeSO` per type (Standard, Cryo, Junk, Volatile; Suspicious is a flag on a spawn entry, not a type).
- Mouse/keyboard must keep working in the Editor alongside touch (WASD + mouse look + `E` pick/place + `Space`/swipe throw).
- Kit conventions from `CLAUDE.md` apply to every new file (namespaces, `[SerializeField] private camelCase`, `_camelCase` runtime privates, `*SO` suffix, `e_`/`v_`/`vDBInt_` asset prefixes, SO events over singletons, XML summaries).
- Commit after every slice (and after any task that leaves the project compiling and runnable). Commit messages end with the Co-Authored-By trailer for the model doing the work (currently `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- Out of scope: real ads/IAP, save beyond `DB*` variables, multiple stations, drones, missions, full collection book, story beyond Rook's tutorial lines.

## Review Focus

Inputs the spec implies but no slice's acceptance test exercises directly; each gets a test in the owning task.

1. **Placing a crate in a full bay** — context button must read "Bay full" (disabled), not place or drop the crate. (Task 2.1 `BayModel` tests; Task 2.3 play test.)
2. **Shift timer hitting 0 while the player is carrying crates** — summary must still open; carried crates are discarded without payout and without a fine. (Task 6.3.)
3. **Spawn schedule entries with the same timestamp / conveyor full** — spawner must queue, not overlap crates at the airlock. (Task 4.1 `ConveyorQueue` tests: `Advance_WhenEndBlocked_StopsWithSpacing`; Task 4.2 `SpawnScheduler` tests.)
4. **Swipe while not holding junk** (or holding a non-junk crate) — must do nothing; look-drag must not be mistaken for a swipe. (Task 4.5: swipe gated by `Carrier.TopIsJunk` and by minimum upward velocity; play test.)
5. **Launching with an unscanned bad crate vs. an unscanned clean-suspicious crate** — only the bad one triggers the news alert + fine; a scanned-clean crate in its bay pays normally. (Task 5.4 `PayoutRules` tests `Launch_UnscannedBad_AppliesFineOnce`, `Launch_ScannedClean_NoFine`.)

---

## Feature → system map

| Prototype feature (KICKOFF scope) | Existing kit / SJ system (reuse) | New script(s) and responsibility |
|---|---|---|
| Project, kit import, plugins | `GameKit/*` subset (ANALYSIS §5.1), `Plugins/Demigiant`, `Plugins/NiceVibrations` | — |
| First-person move/look, touch joystick + drag look | SJ `FirstPersonController`, `CharacterMotor`, `FirstPersonLook`, `CameraBobController`, `IMoveInput`, `FirstPersonInputProvider`, `MobileMoveInputProvider`, `VirtualJoystick`, `TouchLookArea`, `*ConfigSO` (ANALYSIS §3.6-A) | `OrbitalCargo.FirstPerson` namespace; `DataManager` → kit `GameEventWithBool` swap |
| Greybox room | — | `RoomLayoutSO` (positions of conveyor, bays, booth, chute, terminal, spawn), `RoomBuilder` (editor tool that builds/re-skins the room from the layout — also the art-pass hook) |
| Carry & place (raycast, context button, hold point, snap to slot, ghost) | SJ `HandInteractor` (targeting + input seam), `GrabbableItem` (attach lerp + parent), `HighlightableItem` + outline shaders, `InteractionInputProvider`, `InteractionButtonBinder` | `IInteractable` (Prompt/CanInteract/Interact), `CrateInteractor` (adapted `HandInteractor`: focus → crate or `IInteractable`, drives `ContextButtonView`), `CrateCarrier` (hold points, capacity 1→3), `CarryableCrate` (trimmed `GrabbableItem`), `Slot`, `Bay : IInteractable`, `BayModel` (pure), `PlacementGhost` |
| Bays & launch (3 bays × 6 slots, fill counter, launch button, launch FX, payout + coin pops) | kit `PoolManagerSO` (particles, coins), `SoundService`, `HapticHandlerSO`, `UICollector` (coin trail), `CoinsWidget` (HUD credits), SJ `CameraShaker` + `CameraShakeProfileSO`, DOTween | `LaunchButton : IInteractable`, `ShuttleLauncher` (tween up + particles + shake + haptic + sound + payout), `CreditsService` SO (`Add/TrySpend` over `DBInt` + `e_CreditsChanged`), `PayoutRules` (pure), `BayFillCounterView` (world-space "4 / 6") |
| Conveyor & spawner | kit `TimeMachine` (optional), `PoolManagerSO` (crates) | `ConveyorQueue` (pure path/spacing), `Conveyor` (moves crates along waypoints, queues at end), `SpawnScheduler` (pure), `CrateSpawner` (reads `SpawnScheduleSO`, pools crates), `CrateTypeSO`, `Crate`, `CrateVisuals` |
| Crate twists: cryo ring, volatile slowdown + wobble, junk swipe-throw | kit `SwipeSystem` (`SwipeDetector`), SJ `TrajectoryPreview`, SJ `TutorialAimAssist` pattern, `MovementConfigSO` | `CryoTimer` (+ `CryoRingView` world-space ring), `VolatileEffect` (speed multiplier via `CarrierSpeedModifier` + hold-point wobble), `JunkThrower` (swipe up while holding junk → ballistic arc to chute, homing assist) |
| Scanner booth & chute | URP Lit transparent material swap; kit pool/sound/haptics | `SuspiciousTell` (shake/sparks/drip), `ScannerBooth : IInteractable` (scan sequence, reveal, red/green), `HiddenItemSO`, `Chute` (trigger + `IInteractable` "Drop"; destroys, rewards, "Added to collection"), `CollectionPopupView`, `NewsAlertView` + fine |
| Shift flow & UI (HUD, pause, summary, mock 2× ad, grav-sled upgrade, next shift) | kit FSM (`FiniteStateMachine`, `GameState`, `UIViewState`, `Transition`, `BaseApplicationFlowController`, `UIBase`, `StateRootManager`), SJ `FakeRewardedAdService` + `IRewardedAdService` | `ShiftClock` (pure), `ShiftController` (timer, start/end, raises events), `ShiftConfigSO`, `GameBootstrap` (init sound/pools/FSM), `GameFlow`, `ShiftGameState`, `PauseState/View`, `ShiftSummaryState/View`, `HudView : GameHud`, `UpgradeService` SO (grav-sled → `CrateCarrier.Capacity = 3`), `ShiftStats` |
| Juice & polish, Rook radio | kit `SoundService`, `HapticHandlerSO`, pools, DOTween, `UISequentialScaler`, `ScreenFadeTransitionSO`; SJ `PropShake`, `ComicPopup`, `TaskBriefTickerUI` pattern | `JuicePresenter` (events → sound/haptics/FX, one place), `RadioScriptSO` (lines keyed by beat), `RookRadio` (fires lines on beats), `RadioLineView` (ticker) |
| Art pass, Android build | `RoomBuilder` re-skin, Kenney CC0 packs | — |

## File structure

```
Assets/
  GameKit/                                  kit subset (ANALYSIS §5.1), GameKit.Runtime.asmdef, GameKit/Editor/GameKit.Editor.asmdef
  Plugins/Demigiant/, Plugins/NiceVibrations/
  OrbitalCargo/                             game (Assembly-CSharp), namespaces OrbitalCargo.<Area>
    Scenes/Game.unity
    FirstPerson/                            ported SJ controller + touch controls (OrbitalCargo.FirstPerson)
    Interaction/                            IInteractable, CrateInteractor, CrateCarrier, CarryableCrate, PlacementGhost, ContextButtonView
    Crates/                                 Crate, CrateVisuals, CryoTimer, CryoRingView, VolatileEffect, SuspiciousTell, JunkThrower
    Stations/                               Bay, BayModel, Slot, BayFillCounterView, LaunchButton, ShuttleLauncher, Conveyor, ConveyorQueue,
                                            CrateSpawner, SpawnScheduler, ScannerBooth, Chute
    Economy/                                CreditsService, PayoutRules, UpgradeService, ShiftStats
    Shift/                                  ShiftClock, ShiftController, GameBootstrap
    Flow/                                   GameFlow, ShiftGameState, PauseState, ShiftSummaryState
    UI/                                     HudView, PauseView, ShiftSummaryView, CollectionPopupView, NewsAlertView, RadioLineView
    Radio/                                  RadioScriptSO, RookRadio
    Juice/                                  JuicePresenter
    ScriptableObjects/Scripts/              CrateTypeSO, HiddenItemSO, ShiftConfigSO, SpawnScheduleSO, RoomLayoutSO
    ScriptableObjects/{Config,Crates,Events,Variables,States,Room}/   SO instances (e_*, v_*, vDBInt_*, t_*, CT_*)
    Prefabs/{Player,Crates,Stations,UI}/    Resources/ sub-folder for FSM view prefabs
    Materials/, Audio/, Particles/
    Editor/Tools/                           RoomBuilder, OcdAssetFactory (creates default SO assets)
    Editor/Tests/                           NUnit edit-mode tests (Assembly-CSharp-Editor)
```

---

## Slice 1 — Setup (project, kit, FP controller, greybox room)

### Task 1.1: Create the Unity project and repository

**Files:** `ProjectSettings/*`, `Packages/manifest.json`, `.gitignore`, `Assets/OrbitalCargo/Scenes/Game.unity`

- [ ] **Step 1:** Remind Awais to repoint Unity MCP at `D:\Projects\OrbitalCargoDispatch`; ask him to create the project (Unity Hub → 6000.3.15f1 → **Universal 3D** template → location `D:\Projects\OrbitalCargoDispatch`) if the CLI/MCP route is unavailable. The existing `KICKOFF.md`/`docs/` stay in the root.
- [ ] **Step 2:** Player settings: company `GameDistrict`, product `Orbital Cargo Dispatch`, identifier `com.gamedistrict.orbitalcargo`, Android target, default orientation Landscape Left, autorotate landscape L/R only, Active Input Handling = Input System, colour space Gamma, min SDK 25. Scripting defines (Android + Standalone): `DOTWEEN;ODIN_INSPECTOR;ODIN_INSPECTOR_3;ODIN_INSPECTOR_3_1`.
- [ ] **Step 3:** URP mobile values on the template's `PC_RPAsset`/`Mobile_RPAsset` (use the Mobile one for all quality levels): render scale 0.8, MSAA off, HDR off, main-light shadows on at 1024 / distance 20 / 1 cascade, soft shadows off, SRP Batcher on, no Decal/SSAO features. Delete the template sample assets (`TutorialInfo`, sample scene readme).
- [ ] **Step 4:** Create `Assets/OrbitalCargo/Scenes/Game.unity` (camera, directional light) and set it as the only scene in Build Settings.
- [ ] **Step 5:** `git init`, Unity `.gitignore` (Library, Temp, Logs, obj, UserSettings, `*.csproj`, `*.sln`), commit `chore: create Unity 6 URP project`.

### Task 1.2: Plugins

**Files:** `Assets/Plugins/Demigiant/` (from `NX/Assets/Plugins/Demigiant`, minus `DOTweenPro Examples`), `Assets/Plugins/NiceVibrations/` (from `SJ/Assets/_AhmedIkram/Plugins/NiceVibrations`), `Assets/Resources/DOTweenSettings.asset`

- [ ] **Step 1:** Copy the two folders with their `.meta` files (preserve GUIDs). Awais imports Odin now.
- [ ] **Step 2:** Open the Editor, run Tools → Demigiant → DOTween Utility Panel → Setup; enable UI + Audio modules. Confirm `Lofelt.NiceVibrations.asmdef` compiles.
- [ ] **Step 3:** Commit `chore: add DOTween and NiceVibrations`.

### Task 1.3: GameKit core subset

**Files:** `Assets/GameKit/**` per ANALYSIS §5.1 (copy with `.meta`); modify `Assets/GameKit/GameKit.Runtime.asmdef`, `Assets/GameKit/Editor/GameKit.Editor.asmdef`; create `Assets/GameKit/Architecture/Utilities/SoundSystem/Resources/SoundSettings.asset` + `Assets/OrbitalCargo/Audio/MainMixer.mixer`

**Interfaces produced (used by every later task):** `GameKit.Architecture.Events.GameEvent { Subscribe, UnSubscribe, Invoke, HasListeners }`, `GameEventWithParam<T>.Raise(T)`, `GameEventWithBool/Int/String`; `GameKit.Architecture.Variables.{Int, Float, Bool, DBInt, DBBool, DBBoolWithEvent}`; `GameKit.Architecture.PoolSystem.PoolManagerSO { Initialize(Transform), Get(string), Release(string, GameObject), ReleaseAfterDelay(string, GameObject, float) }`; `THEBADDEST.SoundSystem.SoundService { Initialize(), Play(string), Play(string, Vector3), Stop(string), FadeIn/FadeOut }`; `GameKit.Haptics.HapticHandlerSO.ApplyHapticFeedback(HapticIntensity)`; FSM types `FiniteStateMachine { IEnumerator Init(); TransitionTo(Transition, bool pauseCurrent) }`, `State`, `UIViewState { stateId, usePooling, OnBindView(UIBase), OnUnbindView(UIBase) }`, `GameState { gameplayPrefabId, hudPrefabId, statePooler, GameStateEnter/Paused/Resumed/Exit }`, `Transition`, `BaseApplicationFlowController<T> { BootFlow(T, UICloseReasons), GoTo(T, UICloseReasons), RegisterFlowEvents(), UnregisterFlowEvents() }`, `UIBase { Show(bool, bool), IEnumerator Hide(bool) }`, `StateRootManager.Initialize(states, uiStates)`; `GameKit.Architecture.SwipeSystem.SwipeDetector.OnSwipeDetected(SwipeData)`; `UICollector.PlayTrailAction(poolId, from, to, count)`; `GameKit.UI.Header.CoinsWidget`; `ScreenFadeTransitionSO.Play(Action)`; `THEBADDEST.Coroutines.CoroutineHandler`.

- [ ] **Step 1:** Copy exactly the §5.1 Step 2 list (CoroutineHandler, Events, Variables minus `SharedDictionary*`/`KeyRegistry`, PoolSystem minus `PoolMonitor`, SoundSystem, SwipeSystem, `TimeMachine.cs` + `e_Tick`, `UIValueAnimator`, `CustomEditorTools`, Haptics + `Art/Sounds/Scriptables` toggles and events, FSM core + `UIBase` family + `BaseApplicationFlowController` + `StateRootManager` + `GameState.cs` + `GameHud.cs` (`AppFlow/States/Game/GameHud/Scripts/GameHud.cs`, needed by `GameState`) + `UIStates.prefab`, `UICollector`, `ScreenFadeTransitionSO`, Header widgets, `UISequentialScaler`, `VerticalGroupAnimator`, `FrameRateSetter`, `MultiTouchSetter`, particles `ConfettiRainbow/GoldenHit/StarExplosion/StarTrail/ShopCoinsStars`, each module's `.md`). **Not** `StudioSplash`.
- [ ] **Step 2:** Rewrite `GameKit.Runtime.asmdef` `references` to `["Coroutines","UnityEngine.UI","Unity.TextMeshPro","Lofelt.NiceVibrations","DOTween.Modules","Unity.InputSystem","Unity.RenderPipelines.Universal.Runtime"]`; `GameKit.Editor.asmdef` to `["GameKit.Runtime","Coroutines","Unity.TextMeshPro","UnityEngine.UI","DOTween.Modules"]`.
- [ ] **Step 3:** Fix compile errors by deletion only: stray `using Unity.VisualScripting;`, references to excluded modules (e.g. `GameHud` → check its `using`s; `CoinsWidget.openShopEvent` stays, it's a kit event). Any kit file needing an excluded type and not in the Interfaces list above → delete the file, note it in the commit.
- [ ] **Step 4:** Create `MainMixer.mixer` with groups `SFX`, `UI`, `Music` and exposed params `volume`, `sfx`, `ui`, `music`; create `Resources/SoundSettings.asset` pointing at it; create `Assets/OrbitalCargo/Audio/OcdSounds.asset` (`SoundService`) with placeholder entries `Click`, `Pickup`, `Place`, `Launch`, `CoinPop`, `ScanLoop`, `ScanBad`, `ScanGood`, `Chute`, `Alarm`, `Radio` (empty clips allowed for now).
- [ ] **Step 5:** Verify: no compile errors; `Window ▸ General ▸ Test Runner` shows no kit errors. Commit `feat: import GameKit core subset`.

### Task 1.4: First-person controller port

**Files:** Create `Assets/OrbitalCargo/FirstPerson/{FirstPersonController, CharacterMotor, ICharacterMotor, FirstPersonLook, ILookController, CameraBobController, IMoveInput, FirstPersonInputProvider, MobileMoveInputProvider, VirtualJoystick, TouchLookArea, MovementConfigSO, LookConfigSO, CameraBobConfigSO}.cs` (from ANALYSIS §3.6-A), `Assets/OrbitalCargo/ScriptableObjects/Config/{FP_MovementConfig, FP_LookConfig, FP_CameraBobConfig}.asset`, `Assets/OrbitalCargo/Prefabs/Player/Player.prefab`, `Assets/OrbitalCargo/Prefabs/UI/Controls.prefab`, `Assets/OrbitalCargo/Prefabs/UI/EventSystem.prefab` (from SJ `Prefabs/UICanvas/EventSystem.prefab`)

**Interfaces produced:** `OrbitalCargo.FirstPerson.IMoveInput { Vector2 Move; Vector2 Look; bool SprintHeld; bool CrouchHeld; bool JumpPressed; }`, `ICharacterMotor { bool IsGrounded; Vector3 Velocity; void Move(Vector2 input, bool sprint, bool crouch); void Jump(); }`, `CharacterMotor.SpeedMultiplier { get; set; }` (new, default 1 — used by `VolatileEffect`), `ILookController { float Pitch; void Look(Vector2 delta); }`, `CameraBobController.Kick(float amplitude, float duration)`, `TouchLookArea { Vector2 Delta; event Action<Vector2> Tapped; }`, `VirtualJoystick.Value`.

- [ ] **Step 1:** Copy the files; add `namespace OrbitalCargo.FirstPerson`; rename serialized fields to kit style (`[SerializeField] private movementConfig`); re-base `*ConfigSO` on `ScriptableObject` with `[CreateAssetMenu(menuName = "OrbitalCargo/FirstPerson/...")]`; drop `ISteerStick` from `VirtualJoystick` and the 4 non-Normal modes; drop `HeldLookConfigSO` and the auto-aim lock + `OnHeldItemChanged` from `FirstPersonLook`; replace `DataManager.EventsOrNull?.OnGamePaused` in `FirstPersonController` with `[SerializeField] private GameEventWithBool onGamePaused` (kit type); remove `IsAutoAiming`.
- [ ] **Step 2:** Add `public float SpeedMultiplier { get; set; } = 1f;` to `CharacterMotor`, applied to the target speed in `Move`. In `MobileMoveInputProvider` default `allowKeyboardMouse` to `true` under `#if UNITY_EDITOR` (field initialiser) so Editor gets WASD + mouse; guard so mouse delta is only added when no touch finger is active.
- [ ] **Step 3:** Set `FP_LookConfig.LimitYaw = false`; walk 4.5, sprint unused on mobile.
- [ ] **Step 4:** Build `Player.prefab`: root `Player` (`CharacterController` h 1.8 r 0.35, `CharacterMotor`, `FirstPersonLook` pivot = camera, `FirstPersonController`, `MobileMoveInputProvider`), child `Main Camera` (y 1.6; `Camera`, `AudioListener`, `UniversalAdditionalCameraData`, `CameraBobController`), grandchild `HoldPoint` (0.35, −0.35, 0.8). Build `Controls.prefab`: Canvas Overlay, `CanvasScaler` 1920×1080 match 0.5; children in order `TouchLookArea` (stretch full, alpha 0), `Joystick` (bottom-left, 280 px, `VirtualJoystick`), `ContextButton` (bottom-right 221 px, uGUI Button, label TMP — wired in Slice 2), `Crosshair` (80 px centre).
- [ ] **Step 5:** Place Player, Controls, EventSystem (`InputSystemUIInputModule`) in `Game.unity` over a temporary 20×20 plane. Play test: WASD + mouse look in Editor; in Device Simulator (Pixel 7 landscape) joystick moves, right-side drag looks, both fingers at once work. Commit `feat: port first-person controller with touch controls`.

### Task 1.5: Greybox room builder

**Files:** Create `Assets/OrbitalCargo/ScriptableObjects/Scripts/RoomLayoutSO.cs`, `Assets/OrbitalCargo/Editor/Tools/RoomBuilder.cs`, `Assets/OrbitalCargo/ScriptableObjects/Room/RoomLayout_Default.asset`, `Assets/OrbitalCargo/Materials/{Floor,Wall,BayA,BayB,BayC,Booth,Chute,Conveyor}.mat` (URP Lit, flat colours)

**Interfaces produced:** `RoomLayoutSO { Vector2 RoomSize; float WallHeight; Vector3 PlayerSpawn; Vector3 ConveyorStart; Vector3[] ConveyorWaypoints; Vector3 ConveyorEnd; BayAnchor[] Bays /* {string id; Vector3 position; float yaw} ×3 */; Vector3 BoothPosition; Vector3 ChutePosition; Vector3 TerminalPosition; }`. `RoomBuilder` menu `Tools/OrbitalCargo/Build Greybox Room` — destroys and rebuilds the `Room` root in the open scene from the layout (floor, 4 walls, airlock gap, bay pads with 6 slot markers each (2×3), booth box, chute disc, terminal box, conveyor strip, planet window quad); every placed object named by role (`Bay_A`, `Slot_A_0`…) so Slice 2–5 scripts can be added on top.

- [ ] **Step 1:** Write `RoomLayoutSO` and the default asset following GDD "Room layout": conveyor enters a side wall and runs along the floor to the far wall; Bays A/B/C along the far wall; booth beside conveyor end; chute beside booth; terminal by the opposite wall door; player spawn centre. Room 16×12 m, walls 4 m.
- [ ] **Step 2:** Write `RoomBuilder` (editor only, `Undo.RegisterCreatedObjectUndo`, materials by role from a serialized `RoomBuilderSettings` or hard-coded asset paths). Slots: cube markers 0.9 m, 2 rows × 3, under each bay pad, names `Slot_<bay>_<i>`.
- [ ] **Step 3:** Run it; walk the room with the player; verify the layout reads at a glance (colour-coded bays A grey-blue, B white/cyan, C yellow). Commit `feat: greybox room builder and default layout`.

**Slice 1 acceptance (tell Awais to test):** Project opens with no errors; Play → walk around the greybox room with WASD/mouse; Device Simulator → joystick + drag look; bays/booth/chute/conveyor visible and colour-coded; `Tools/OrbitalCargo/Build Greybox Room` rebuilds the room after editing `RoomLayout_Default`.

---

## Slice 2 — Carry and place

### Task 2.1: `BayModel` (pure) with tests

**Files:** Create `Assets/OrbitalCargo/Stations/BayModel.cs`, `Assets/OrbitalCargo/Editor/Tests/BayModelTests.cs`

**Interfaces produced:** `OrbitalCargo.Stations.BayModel(int slotCount)`: `int SlotCount`, `int Count`, `bool IsFull`, `bool IsEmpty`, `bool TryGetNearestFreeSlot(Vector3 point, IReadOnlyList<Vector3> slotPositions, out int slotIndex)`, `void Occupy(int slotIndex, int crateId)`, `bool TryRelease(int slotIndex, out int crateId)`, `IEnumerable<int> OccupiedCrateIds`, `void Clear()`.

- [ ] **Step 1:** Write tests: `NewBay_IsEmpty_NotFull`, `Occupy_SixSlots_IsFull`, `TryGetNearestFreeSlot_SkipsOccupied_ReturnsClosest`, `TryGetNearestFreeSlot_WhenFull_ReturnsFalse`, `Occupy_SameSlotTwice_Throws`, `Clear_ResetsCount`.
- [ ] **Step 2:** Run Test Runner (EditMode) → fail (type missing). **Step 3:** Implement. **Step 4:** Run → pass. **Step 5:** Commit `feat: BayModel with tests`.

### Task 2.2: Crate type data and crate prefab

**Files:** Create `ScriptableObjects/Scripts/CrateTypeSO.cs`, `Crates/Crate.cs`, `Crates/CrateVisuals.cs`, `Interaction/CarryableCrate.cs` (trimmed SJ `GrabbableItem`), `Prefabs/Crates/Crate.prefab`, `ScriptableObjects/Crates/{CT_Standard, CT_Cryo, CT_Junk, CT_Volatile}.asset`, `Materials/Crates/*.mat`

**Interfaces produced:** `CrateTypeSO { string Id; string DisplayName; Material CasingMaterial; Color AccentColor; BayId Destination /* enum A, B, C, Chute */; int Payout; bool IsJunk; float CryoSeconds /* 0 = none */; float VolatileSlowMultiplier /* 1 = none */; float WobbleAmplitude; }`. `Crate : MonoBehaviour { int Id; CrateTypeSO Type; CrateState State /* OnConveyor, Carried, Slotted, Scanning, Thrown, Consumed */; bool IsSuspicious; HiddenItemSO HiddenItem /* null = clean */; bool IsBad => HiddenItem != null; bool IsScanned; void Configure(CrateTypeSO type, bool suspicious, HiddenItemSO hidden); event Action<Crate, CrateState> StateChanged; void SetState(CrateState) }`. `CarryableCrate { bool CanGrab; void AttachTo(Transform holdPoint, Action onAttached); void Detach(); void SnapTo(Transform slot, Action onSnapped); void Launch(Vector3 velocity) }` (kinematic Rigidbody; `Launch` enables physics for the junk throw only). `HiddenItemSO` is created in Task 5.1 — stub the class now with `string Id; string DisplayName; GameObject Prefab`.

- [ ] **Step 1:** Write `CrateTypeSO` (`[CreateAssetMenu(fileName = "CT_", menuName = "OrbitalCargo/Crates/Crate Type")]`) and the four assets: Standard → A, payout 10; Cryo → B, payout 15, `CryoSeconds 45`; Junk → Chute, payout 5, `IsJunk`; Volatile → C, payout 20, `VolatileSlowMultiplier 0.6`, `WobbleAmplitude 0.04`.
- [ ] **Step 2:** `Crate.prefab`: root `Crate` (`Crate`, `CarryableCrate`, kinematic `Rigidbody`, `BoxCollider` 0.8 m, layer `Crate`), child `Casing` (cube renderer), child `Decal` (quad, colour strip), child `HiddenItem` (empty, inactive), child `Tells` (inactive; Slice 5). `CrateVisuals` applies `CasingMaterial`/`AccentColor` on `Configure`.
- [ ] **Step 3:** Trim `GrabbableItem` into `CarryableCrate` per ANALYSIS §3.3 (keep attach lerp 0.18 s + `SetParent`, scale pop; drop strand/tail/respawn/SFX). Commit `feat: crate data and prefab`.

### Task 2.3: Interaction — pick up, context button, place with ghost

**Files:** Create `Interaction/IInteractable.cs`, `Interaction/CrateInteractor.cs` (adapted SJ `HandInteractor`), `Interaction/CrateCarrier.cs`, `Interaction/InteractionConfigSO.cs` (from SJ), `Interaction/HighlightableItem.cs` + `HighlightConfigSO.cs` + `Shaders/FirstPersonOutline{Mask,Fill}.shader` + materials (from SJ), `Interaction/PlacementGhost.cs`, `UI/ContextButtonView.cs`, `Stations/Slot.cs`, `Stations/Bay.cs`, `ScriptableObjects/Events/{e_CratePicked, e_CratePlaced}.asset` (`GameEventWithParam<Crate>` subclass `GameEventWithCrate`), `ScriptableObjects/Config/FP_InteractionConfig.asset` (`PickRange 3`, `PickMask = Crate | Interactable`)

**Interfaces produced:** `IInteractable { string Prompt(CrateCarrier carrier); bool CanInteract(CrateCarrier carrier); void Interact(CrateCarrier carrier, RaycastHit hit); }`. `CrateCarrier { int Capacity /* 1 */; IReadOnlyList<Crate> Carried; Crate Top; bool IsFull; bool IsEmpty; bool TopIsJunk; Transform[] HoldPoints; bool TryPickUp(Crate); Crate PopTop(); event Action Changed; }`. `CrateInteractor { IInteractable Focused; Crate FocusedCrate; void RequestInteract(); event Action<string, bool> PromptChanged /* text, enabled */ }` — `Update`: raycast from camera centre (`PickRange`, `PickMask`); if hit `Crate` with `State == OnConveyor` and carrier not full → prompt "Pick up"; else if hit `IInteractable` → its `Prompt/CanInteract`; else prompt "" (button hidden). `E` key / context button → `RequestInteract()`. `Bay : IInteractable { BayId Id; BayModel Model; IReadOnlyList<Slot> Slots; event Action<Bay> Changed; bool TryPlace(Crate, Vector3 nearPoint); Crate[] TakeAll(); }` — `Prompt` = "Place" (or "Bay full"), `CanInteract` = carrier has crate && `!Model.IsFull`; `Interact` snaps `carrier.PopTop()` to the nearest free slot (crate → `Slotted`), raises `e_CratePlaced`. `Slot { int Index; Transform Anchor; Crate Occupant }`. `PlacementGhost` shows a translucent cube at the target slot while `Focused is Bay && carrier has crate`.

- [ ] **Step 1:** Copy SJ `HandInteractor` → `CrateInteractor` applying ANALYSIS §3.7 swaps (no hands: attach completes on `CarryableCrate.AttachTo` callback; no SFX; `IGrabbable` → `Crate`/`IInteractable`); copy highlight files unchanged except namespace.
- [ ] **Step 2:** `ContextButtonView` subscribes to `PromptChanged`, sets TMP label, `interactable`, hides when text empty; its Button calls `RequestInteract`. Add `Bay` + `Slot` components to the `RoomBuilder` output (builder adds them when creating `Bay_*`/`Slot_*` objects; `Bay.Awake` builds `BayModel(6)`).
- [ ] **Step 3:** Place 3 `Crate` prefabs on the conveyor strip in the scene (temporary), `State = OnConveyor`. Play test: look at crate → "Pick up" → crate flies to hold point; look at a bay → ghost at nearest free slot, "Place" → snaps; 6 placed → "Bay full" disabled; Editor `E` and Simulator button both work. Commit `feat: pick up, context button and slot placement`.

**Slice 2 acceptance:** Pick up a crate from the conveyor with the context button (or `E`), see the outline highlight on the targeted crate, walk to a bay, see the ghost on the nearest free slot, place; fill a bay to 6 and see "Bay full".

---

## Slice 3 — Bays and launch

### Task 3.1: `PayoutRules` (pure) + `CreditsService`

**Files:** Create `Economy/PayoutRules.cs`, `Economy/CreditsService.cs`, `Editor/Tests/PayoutRulesTests.cs`, `ScriptableObjects/Scripts/ShiftConfigSO.cs`, `ScriptableObjects/Config/ShiftConfig_Pitch.asset`, `ScriptableObjects/Variables/vDBInt_Credits.asset`, `ScriptableObjects/Events/{e_CreditsChanged (GameEventWithInt), e_CreditsAnimated}.asset`

**Interfaces produced:** `ShiftConfigSO { float ShiftLengthSeconds = 300; float ConveyorSpeed = 0.8; float CryoSecondsDefault = 45; float ScanDurationSeconds = 2.5; float ExpiredCryoMultiplier = 0.4; float WrongBayMultiplier = 0.5; int ChuteBonus = 25; int UnscannedBadFine = 30; float VolatileSlowMultiplier = 0.6; int GravSledPrice = 150; int CarryCapacityBase = 1; int CarryCapacityGravSled = 3; SpawnScheduleSO Schedule; }`. `PayoutRules` (static): `int CratePayout(int basePayout, bool cryoExpired, bool wrongBay, ShiftConfigSO cfg)`; `LaunchResult Launch(IEnumerable<CrateSnapshot> crates, BayId bay, ShiftConfigSO cfg)` → `LaunchResult { int Credits; int FineApplied; int BadShipped; }`; `CrateSnapshot { int BasePayout; BayId Destination; bool CryoExpired; bool IsBad; bool IsScanned; }`. `CreditsService : ScriptableObject { int Balance; void Add(int amount, bool animated); bool TrySpend(int amount); }` over `DBInt credits`, raising `e_CreditsChanged(Balance)` and `e_CreditsAnimated` (for the HUD count-up).

- [ ] **Step 1:** Tests: `CratePayout_Normal_ReturnsBase`, `CratePayout_CryoExpired_AppliesMultiplier(15 → 6)`, `CratePayout_WrongBay_AppliesMultiplier`, `Launch_SixStandard_SumsPayout(60)`, `Launch_UnscannedBad_AppliesFineOnce` (two bad unscanned crates → one fine of 30, `BadShipped 2`), `Launch_ScannedClean_NoFine`, `CreditsService_TrySpend_InsufficientReturnsFalse`.
- [ ] **Step 2:** Run → fail. **Step 3:** Implement. **Step 4:** Pass. **Step 5:** Commit `feat: payout rules and credits service`.

### Task 3.2: Launch button, shuttle launch, fill counters, coin pops

**Files:** Create `Stations/LaunchButton.cs`, `Stations/ShuttleLauncher.cs`, `Stations/BayFillCounterView.cs`, `Juice/CameraShaker.cs` + `CameraShakeProfileSO.cs` (from SJ `_AhmedIkram/CameraShake`), `ScriptableObjects/Config/Shake_Launch.asset`, `Prefabs/Stations/Shuttle.prefab`, `Prefabs/Particles/{LaunchSmoke, CoinPop}.prefab`, `ScriptableObjects/Config/{ParticlesPooler, CratePooler, UIPooler}.asset` (`PoolManagerSO`), `ScriptableObjects/Events/e_ShuttleLaunched (GameEventWithParam<LaunchResult>)`, `UI/HudView.cs` (first version: credits via kit `CoinsWidget`, `UICollector` target)

**Interfaces produced:** `LaunchButton : IInteractable { Bay Bay }` — `Prompt` "Launch", `CanInteract` = `Bay.Model.IsFull`, `Interact` → `ShuttleLauncher.Launch(Bay)`. `ShuttleLauncher { void Launch(Bay bay); bool IsLaunching; }`: takes `bay.TakeAll()` snapshots → `PayoutRules.Launch` → shuttle (with crates parented) `DOMoveY(+30, 1.6 s).SetEase(InQuad)`, `ParticlesPooler.Get("LaunchSmoke")`, `CameraShaker.Play(Shake_Launch)`, `haptics.ApplyHapticFeedback(Success)`, `sound.Play("Launch")`, `credits.Add(result.Credits, animated: true)`, `UICollector.PlayTrailAction("CoinPop", launchScreenRect, hudCreditsRect, count)`, raises `e_ShuttleLaunched(result)`; after 2 s shuttle resets, crates released to `CratePooler`, bay button glows when full (emission on). `BayFillCounterView` world-space TMP "n / 6" above each bay, subscribes `Bay.Changed`. `GameBootstrap` (Slice 6 owns it; create the minimal version now: `Awake` → `soundService.Initialize()`, each `PoolManagerSO.Initialize(poolRoot)`).

- [ ] **Step 1:** Port `CameraShaker` (replace the `DataManager` line with a public `Play(profile)` only). Build pools: `CratePooler` (`"Crate"` → Crate.prefab, 24 prewarmed), `ParticlesPooler` (`"LaunchSmoke"`, `"CoinHitParticles"` → kit `GoldenHit`), `UIPooler` (`"CoinPop"` → a 48 px coin Image prefab).
- [ ] **Step 2:** Implement `LaunchButton`, `ShuttleLauncher`, `BayFillCounterView`; `RoomBuilder` adds a `LaunchButton` pad + `Shuttle` instance per bay. HUD canvas (temporary, becomes the FSM HUD in Slice 6): kit `CoinsWidget` bound to `vDBInt_Credits` + `e_CreditsChanged`/`e_CreditsAnimated`; `UICollector` in scene with `UIPooler`.
- [ ] **Step 3:** Play test: fill Bay A with 6 standard crates (spawn 6 temp crates), launch button prompt enabled only when full, launch → shuttle rises with crates, smoke, shake, sound/haptic, +60 credits coin trail into the HUD counter; fill counter updates. Commit `feat: bays, launch sequence and credits payout`.

**Slice 3 acceptance:** Fill a bay, "Launch" enables, launch plays tween + particles + shake + sound; coins fly to the HUD and the counter counts up by the bay's payout; the bay empties and can be refilled.

---

## Slice 4 — Conveyor, spawner and crate twists

### Task 4.1: `ConveyorQueue` (pure) with tests

**Files:** Create `Stations/ConveyorQueue.cs`, `Editor/Tests/ConveyorQueueTests.cs`

**Interfaces produced:** `ConveyorQueue(IReadOnlyList<Vector3> waypoints, float spacing)`: `float Length`, `int Enqueue(int crateId)` (starts at distance 0; returns false/throws if entry blocked within `spacing`), `bool CanEnqueue`, `void Advance(float dt, float speed)` (each item moves forward, clamped to `min(Length, itemAhead - spacing)`), `Vector3 PositionOf(int crateId)`, `Vector3 ForwardOf(int crateId)`, `bool Remove(int crateId)`, `IEnumerable<int> Items`, `bool IsAtEnd(int crateId)`.

- [ ] **Step 1:** Tests: `Advance_MovesItemBySpeedTimesDt`, `Advance_WhenEndBlocked_StopsWithSpacing` (two items; second stops at `Length - spacing`), `CanEnqueue_FalseWhenEntryOccupied`, `Remove_LetsItemsBehindAdvance`, `PositionOf_FollowsCornerWaypoints` (L-shaped path, item at corner+1 lies on second segment).
- [ ] **Step 2:** Fail → **Step 3:** implement → **Step 4:** pass → **Step 5:** commit `feat: ConveyorQueue with tests`.

### Task 4.2: `SpawnScheduler` (pure) + `SpawnScheduleSO`

**Files:** Create `ScriptableObjects/Scripts/SpawnScheduleSO.cs`, `Stations/SpawnScheduler.cs`, `Editor/Tests/SpawnSchedulerTests.cs`, `ScriptableObjects/Config/{SpawnSchedule_Pitch, SpawnSchedule_Shift2}.asset`

**Interfaces produced:** `SpawnScheduleSO { List<SpawnEntry> Entries; }`, `[Serializable] SpawnEntry { float Time; CrateTypeSO Type; int Count = 1; float Interval = 2; bool Suspicious; HiddenItemSO Hidden; string RadioBeat /* optional, Slice 7 */ }`. `SpawnScheduler(IReadOnlyList<SpawnEntry>)`: `void Advance(float elapsed, List<SpawnRequest> due)` — expands `Count × Interval` into individual `SpawnRequest { CrateTypeSO Type; bool Suspicious; HiddenItemSO Hidden; string RadioBeat }` and returns each exactly once in time order; `bool IsExhausted`.

- [ ] **Step 1:** Tests: `Advance_ReturnsEntriesUpToElapsed_Once`, `Advance_ExpandsCountWithInterval` (Count 3, Interval 2 at t=10 → due at 10, 12, 14), `Advance_SameTimestamp_ReturnsBoth`, `IsExhausted_AfterLastEntry`.
- [ ] **Step 2–4:** TDD cycle. **Step 5:** Author `SpawnSchedule_Pitch` from KICKOFF's table: 0:05 Standard ×1 (`RadioBeat "Intro"`); 1:00 Standard ×4 interval 8; 1:45 Cryo ×4 interval 10 (`"CryoIntro"`); 2:45 Standard suspicious ×1 `Hidden = HI_AlienEgg` (`"Suspicious"`); 3:00 Standard ×2; 3:30 Junk ×3 interval 8 (`"Junk"`); 4:00 Volatile ×3 interval 10 (`"Volatile"`); 4:30 Cryo ×2. `SpawnSchedule_Shift2`: same with all intervals ×0.6 and times ×0.8. Commit `feat: spawn scheduler and pitch schedule`.

### Task 4.3: `Conveyor` + `CrateSpawner` MonoBehaviours

**Files:** Create `Stations/Conveyor.cs`, `Stations/CrateSpawner.cs`, `ScriptableObjects/Events/e_CrateSpawned (GameEventWithCrate)`

**Interfaces produced:** `Conveyor { bool CanAccept; void Place(Crate); void Remove(Crate); }` — builds `ConveyorQueue(layout.ConveyorWaypoints, spacing 1.0)`, `Update` advances by `shiftConfig.ConveyorSpeed`, writes crate transforms (kinematic, no physics), crates at the end wait. `CrateSpawner { void Begin(SpawnScheduleSO); void Stop(); float Elapsed; }` — `Update` calls `scheduler.Advance(elapsed, due)`; for each request when `conveyor.CanAccept`: `cratePooler.Get("Crate")`, `crate.Configure(type, suspicious, hidden)`, `conveyor.Place(crate)`, raise `e_CrateSpawned(crate)`; otherwise holds the request until there's room. Picking a crate up calls `conveyor.Remove(crate)` (in `CrateInteractor` pick path).

- [ ] **Step 1:** Implement; `RoomBuilder` adds `Conveyor` to the conveyor strip with the layout's waypoints; `CrateSpawner` on the airlock object; a temporary `Start` → `spawner.Begin(shiftConfig.Schedule)`.
- [ ] **Step 2:** Play test: crates emerge from the airlock at schedule times, ride the belt, queue at the end with visible spacing, picking the front crate lets the rest advance. Remove the temp crates from Slice 2. Commit `feat: kinematic conveyor and scheduled spawner`.

### Task 4.4: Cryo and volatile twists

**Files:** Create `Crates/CryoTimer.cs`, `Crates/CryoRingView.cs`, `Crates/VolatileEffect.cs`, `Prefabs/Crates/CryoRing.prefab` (world-space canvas, radial `Image` fill), `ScriptableObjects/Events/e_CryoExpired (GameEventWithCrate)`

**Interfaces produced:** `CryoTimer { float Remaining; float Normalized; bool Expired; }` — enabled by `CrateVisuals` when `Type.CryoSeconds > 0` (overridable by `ShiftConfigSO.CryoSecondsDefault` when the type leaves it 0), counts down from spawn until `Slotted`/`Consumed`; ring colour cyan → amber → red, raises `e_CryoExpired` once; `Crate` exposes `bool CryoExpired` for `CrateSnapshot`. `VolatileEffect` — on `Carried`: `motor.SpeedMultiplier = Type.VolatileSlowMultiplier` (via carrier's `CharacterMotor`), hold-point wobble `Mathf.Sin` on local position with `WobbleAmplitude`, amber strobe on the decal; restores multiplier on release (and with multiple carried volatile crates, the lowest multiplier wins).

- [ ] **Step 1:** Implement; `CrateCarrier` exposes `CharacterMotor Motor` and re-evaluates `SpeedMultiplier` from all carried crates on `Changed`.
- [ ] **Step 2:** Play test: cryo crate shows a ring that drains over 45 s (set to 10 s temporarily) and turns red; slotting it stops the ring; expired cryo pays 40% at launch. Carrying volatile slows walking and the crate wobbles; releasing restores speed. Commit `feat: cryo ring timer and volatile slowdown`.

### Task 4.5: Junk swipe-throw into the chute

**Files:** Create `Crates/JunkThrower.cs`, `Stations/Chute.cs` (first version: trigger consumes crates), `Juice/TrajectoryPreview.cs` (from SJ), `ScriptableObjects/Events/{e_CrateThrown, e_CrateChuted} (GameEventWithCrate)`; kit `SwipeDetector` added to the Controls canvas

**Interfaces produced:** `JunkThrower { void TryThrow(SwipeData swipe); }` — listens to `SwipeDetector.OnSwipeDetected`; requires `carrier.TopIsJunk`, `swipe.Direction == Up`, swipe began on the look area (not joystick/button; `TouchLookArea` exposes `bool IsDragging`); computes a ballistic velocity to `Chute.TargetPoint` (solve for apex height 2 m), `carrier.PopTop()`, `crate.Launch(v)` (physics on), `Crate.SetState(Thrown)`, raises `e_CrateThrown`; homing assist: `FixedUpdate` bends velocity toward the chute at 180°/s while airborne (SJ `TutorialAimAssist` pattern). Editor fallback: `Space` throws. `Chute { Vector3 TargetPoint; }` — trigger collider on `Thrown` or dropped crate → `Consume(crate)`: shrink tween, `cratePooler.Release`, raises `e_CrateChuted(crate)`; junk pays `Type.Payout` via `credits.Add`.

- [ ] **Step 1:** Implement; show `TrajectoryPreview` arc while holding junk and looking roughly at the chute (optional, cheap).
- [ ] **Step 2:** Play test: holding junk, quick upward swipe on the right side → crate arcs into the chute and pays; swiping while holding a standard crate does nothing; normal look-dragging never throws. Commit `feat: swipe-throw junk crates into the chute`.

**Slice 4 acceptance:** Start Play and watch the pitch schedule unfold on the conveyor (use a 1-minute debug schedule if needed): standard → cryo with rings → junk → volatile; cryo expires red; volatile slows you; junk flies into the chute on swipe (or `Space`).

---

## Slice 5 — Scanner booth and chute

### Task 5.1: Hidden items and suspicious tells

**Files:** Create `ScriptableObjects/Scripts/HiddenItemSO.cs` (finish the Task 2.2 stub: add `Sprite Icon; string CollectionSet; int ChuteBonusOverride /* 0 = config */`), `ScriptableObjects/Crates/HI_AlienEgg.asset` (+ greybox egg prefab: green sphere), `Crates/SuspiciousTell.cs`, tells under `Crate.prefab/Tells` (`Shake` via DOTween `DOShakePosition` loop, `Sparks` particle, `Drip` green particle)

- [ ] **Step 1:** `SuspiciousTell` enables a random tell when `crate.IsSuspicious` on `Configure`, disables on `Scanning`/`Consumed`. Suspicious + `Hidden == null` = false alarm (clean).
- [ ] **Step 2:** Play test with the 2:45 entry: the crate visibly shakes/sparks/drips on the belt. Commit `feat: hidden items and suspicious crate tells`.

### Task 5.2: Scanner booth reveal

**Files:** Create `Stations/ScannerBooth.cs`, `Materials/Crates/Casing_XRay.mat` (URP Lit, Surface Type Transparent, alpha 0.25, cyan), `Prefabs/Stations/ScanLine.prefab` (emissive quad), `ScriptableObjects/Events/{e_ScanStarted, e_ScanResult (GameEventWithParam<ScanResult>)}`

**Interfaces produced:** `ScannerBooth : IInteractable { bool IsBusy; Transform CratePoint; }` — `Prompt` "Scan", `CanInteract` = carrier has crate && `!IsBusy`; `Interact`: `carrier.PopTop()`, `crate.SnapTo(CratePoint)`, `State = Scanning`, `sound.Play("ScanLoop")`, raise `e_ScanStarted`; over `ShiftConfigSO.ScanDurationSeconds`: swap casing material to `Casing_XRay`, enable `HiddenItem` child (instantiate `HiddenItemSO.Prefab` into it), scan line sweeps top→bottom; result: `IsBad` → booth light red, `sound.Play("ScanBad")`, `haptics Failure`; else green, `ScanGood`, `Success`; `crate.IsScanned = true`; restore casing (hidden item stays visible for bad); crate becomes pickable again from the booth (`State = OnConveyor`-like `Pickable` state: add `CrateState.Pickable` used by booth output and treat it like `OnConveyor` in `CrateInteractor`); raise `e_ScanResult(new ScanResult { Crate, IsBad })`.

- [ ] **Step 1:** Implement; `RoomBuilder` adds `ScannerBooth` to the booth object with a `CratePoint` child.
- [ ] **Step 2:** Play test: scan the suspicious crate → 2.5 s X-ray, egg revealed, red light; scan a normal crate → green. Commit `feat: scanner booth x-ray reveal`.

### Task 5.3: Chute rewards and collection pop-up

**Files:** Modify `Stations/Chute.cs` (add `IInteractable` "Drop" for carried scanned-bad crates and junk), create `UI/CollectionPopupView.cs`, `Prefabs/UI/CollectionPopup.prefab`, `ScriptableObjects/Variables/vDBInt_ItemsCollected.asset`, `ScriptableObjects/Events/e_ItemCollected (GameEventWithParam<HiddenItemSO>)`

- [ ] **Step 1:** `Chute.Consume`: if `crate.IsBad && crate.IsScanned` → `credits.Add(cfg.ChuteBonus)`, `itemsCollected.Increment()`, raise `e_ItemCollected(hidden)`; if junk → `Type.Payout`; if clean non-junk → nothing (lose value). `CollectionPopupView` (on the HUD canvas) shows icon + "Added to collection: Alien Egg" for 2.5 s with `UISequentialScaler` pop.
- [ ] **Step 2:** Play test: scan → carry egg crate to chute → "Drop" → bonus + pop-up. Commit `feat: chute rewards and collection pop-up`.

### Task 5.4: News alert and fine for unscanned bad crates

**Files:** Create `UI/NewsAlertView.cs`, `Prefabs/UI/NewsAlert.prefab`; modify `ShuttleLauncher` to react to `LaunchResult.FineApplied > 0`; extend `Editor/Tests/PayoutRulesTests.cs` with `Launch_UnscannedBad_AppliesFineOnce`, `Launch_ScannedClean_NoFine` if not already present

- [ ] **Step 1:** 1.5 s after launch with `BadShipped > 0`: `NewsAlertView` slides a ticker "BREAKING: Alien egg hatches on colony! Dispatcher fined −30 ¢", `sound.Play("Alarm")`, `credits.Add(-fine)`.
- [ ] **Step 2:** Play test both paths (ship the suspicious crate unscanned vs scanned). Commit `feat: news alert fine for unscanned bad crates`.

**Slice 5 acceptance:** The 2:45 suspicious crate shakes; scanning reveals the alien egg under a transparent casing with a scan line and red light; dropping it in the chute pays the bonus and shows "Added to collection"; shipping it unscanned triggers the news ticker and fine.

---

## Slice 6 — Shift flow and UI

### Task 6.1: `ShiftClock` (pure) + `ShiftStats`

**Files:** Create `Shift/ShiftClock.cs`, `Economy/ShiftStats.cs`, `Editor/Tests/ShiftClockTests.cs`

**Interfaces produced:** `ShiftClock(float lengthSeconds) { float Elapsed; float Remaining; bool IsOver; bool Tick(float dt) /* true on the frame it ends */; void Reset(float length) }`. `ShiftStats { int CreditsEarned; int Launches; int CratesShipped; int ItemsFound; int Fines; void Reset(); }` (plain class on `ShiftController`, filled from events).

- [ ] **Step 1:** Tests: `Tick_AccumulatesElapsed`, `Tick_ReturnsTrueOnlyOnFrameItEnds`, `Remaining_ClampsAtZero`. **Steps 2–4:** TDD. **Step 5:** Commit `feat: ShiftClock with tests`.

### Task 6.2: FSM flow — bootstrap, GameFlow, states, HUD

**Files:** Create `Shift/GameBootstrap.cs` (finish Task 3.2's minimal version), `Flow/GameFlow.cs`, `Flow/ShiftGameState.cs`, `Flow/PauseState.cs`, `Flow/ShiftSummaryState.cs`, `UI/HudView.cs : GameHud`, `UI/PauseView.cs : UIBase`, `UI/ShiftSummaryView.cs : UIBase`, prefabs `Prefabs/UI/Resources/{ShiftHud, PausePanel, ShiftSummary}.prefab`, `Prefabs/UI/Resources/GameFlow.prefab`, `ScriptableObjects/States/{FSM_Main (FiniteStateMachine), ShiftGameState, PauseState, ShiftSummaryState, t_ToShift, t_ToPause, t_ToSummary}.asset`, `ScriptableObjects/Events/{e_PausePressed, e_ResumePressed, e_ShiftEnded, e_NextShiftPressed, e_BackBtnPressed}.asset`, `ScriptableObjects/Variables/v_CurrentStateSortingOrder (Int)`, `ScriptableObjects/Config/UIStatesPooler.asset` (`PoolManagerSO` with `"ShiftHud"`, `"PausePanel"`, `"ShiftSummary"`)

**Interfaces produced:** `GameBootstrap` (scene, `[DefaultExecutionOrder(-100)]`): `Awake` → `soundService.Initialize()`, pools `Initialize(poolRoot)`, `StateRootManager.Initialize(statesRoot, uiStatesRoot)` (instantiate kit `UIStates.prefab`), instantiate `Resources/GameFlow`; `Start` → `StartCoroutine(fsm.Init())` then `gameFlow.BootFlow(toShift, UICloseReasons.Game)`. `GameFlow : BaseApplicationFlowController<Transition>` — `RegisterFlowEvents`: `e_PausePressed → GoTo(toPause, FullScreenPlacement)` (pauses current), `e_ResumePressed → OnBackButtonPressed()` (PopOne), `e_ShiftEnded → GoTo(toSummary, FullScreenPlacement)`, `e_NextShiftPressed → GoTo(toShift, Game)` (ClearAll). `ShiftGameState : GameState` with `hudPrefabId "ShiftHud"`, `gameplayPrefabId` → an empty `ShiftGameplayRoot.prefab` if `GameState` requires one (gameplay lives in the scene); on `Enter` raises `GameStateEnter`, which `ShiftController` listens to. `HudView : GameHud` — credits (`CoinsWidget`), shift timer TMP (`mm:ss`, subscribes `e_ShiftTick (GameEventWithFloat remaining)`), level "Lvl 1", pause Button → `e_PausePressed`; hosts `ContextButtonView` target rect for `UICollector`. `PauseState/PauseView` — Resume → `e_ResumePressed`; `Time.timeScale = 0` while paused via `FirstPersonController` pause event. `ShiftSummaryState/View` — Task 6.3.

- [ ] **Step 1:** Read `GameKit/StateMachine/STATE_MACHINE.md` rules; create assets and prefabs; move the Slice 3 temporary HUD into `ShiftHud.prefab` (root `Canvas`+`CanvasGroup`+`HudView`), keep `Controls.prefab` as a scene object (not FSM-managed).
- [ ] **Step 2:** Implement `GameBootstrap`, `GameFlow`, states, `PauseView`. Pause hides `Controls` (CanvasGroup) and raises `onGamePaused(true)`.
- [ ] **Step 3:** Play test: HUD appears via FSM; pause → panel, game frozen, resume works; sorting orders stack correctly. Commit `feat: FSM game flow, HUD and pause`.

### Task 6.3: Shift controller, summary, mock 2× ad, grav-sled upgrade, next shift

**Files:** Create `Shift/ShiftController.cs`, `Economy/UpgradeService.cs`, `Ads/IRewardedAdService.cs` + `Ads/FakeRewardedAdService.cs` + `Ads/RewardedAdResolver.cs` (from SJ, verbatim + namespace `OrbitalCargo.Ads`), `Prefabs/UI/FakeAdOverlay.prefab` (full-screen "Ad playing… 2 s" panel), `UI/ShiftSummaryView.cs`, `Flow/ShiftSummaryState.cs`, `ScriptableObjects/Variables/{vDBBool_GravSled, vDBInt_ShiftNumber}.asset`, `ScriptableObjects/Events/{e_ShiftStarted, e_ShiftTick (GameEventWithFloat), e_DoublePayoutClaimed, e_UpgradePurchased}.asset`

**Interfaces produced:** `ShiftController { ShiftStats Stats; int ShiftNumber; void BeginShift(); void EndShift(); }` — on `GameStateEnter`: `clock.Reset(cfg.ShiftLengthSeconds)`, `spawner.Begin(ShiftNumber == 1 ? cfg.Schedule : cfg.ScheduleShift2)`, `Stats.Reset()`, raise `e_ShiftStarted`; `Update`: `clock.Tick`, raise `e_ShiftTick(Remaining)` once per second; on end: `spawner.Stop()`, discard carried crates (`carrier` → pool, no payout/fine), clear bays/conveyor to pool, raise `e_ShiftEnded`. Accumulates `Stats` from `e_ShuttleLaunched`, `e_ItemCollected`. `UpgradeService : ScriptableObject { bool GravSledOwned; bool TryBuyGravSled(); int CarryCapacity => owned ? cfg.CarryCapacityGravSled : cfg.CarryCapacityBase }` over `vDBBool_GravSled`; `CrateCarrier.Capacity` reads it on `e_ShiftStarted`/`e_UpgradePurchased` and adds hold points (stack of 3 offsets). `ShiftSummaryState/View`: shows `Stats` (credits earned, launches, items found, fines) with `VerticalGroupAnimator`; **"2× payout (watch ad)"** button → `adService.Show("summary_2x", result => if Completed { credits.Add(Stats.CreditsEarned); mark claimed; })` with `FakeRewardedAdService` 2 s + overlay; **"Buy Grav-Sled (150 ¢)"** button enabled when affordable and not owned → `upgrades.TryBuyGravSled()`; **"Start next shift"** → `e_NextShiftPressed` (`vDBInt_ShiftNumber++`; shift 2 uses the faster schedule).

- [ ] **Step 1:** Implement; wire the ad service as a scene object resolved via `RewardedAdResolver`.
- [ ] **Step 2:** Play test with `ShiftLengthSeconds = 60`: shift ends → summary with stats; 2× → fake ad 2 s → credits double, button disables; buy grav-sled → next shift you carry 3 crates (stacked), faster spawns. Also: end the shift while carrying a crate → no error, no payout. Commit `feat: shift loop, summary, mock rewarded ad and grav-sled upgrade`.

**Slice 6 acceptance:** Full loop: HUD with credits/timer/level/pause; 5-minute shift ends into the summary; mock 2× ad doubles the shift's earnings; buying the grav-sled lets you carry 3 crates in shift 2 with faster spawns; pause/resume works mid-shift.

---

## Slice 7 — Juice and polish

### Task 7.1: `JuicePresenter`

**Files:** Create `Juice/JuicePresenter.cs`, `Juice/PropShake.cs` (from SJ), `Juice/ComicPopup.cs` (from SJ, TMP), `ScriptableObjects/Config/Shake_Place.asset`

- [ ] **Step 1:** One MonoBehaviour subscribing to `e_CratePicked` (Pickup sound, Light haptic, `CameraBobController.Kick` 0.02), `e_CratePlaced` (Place sound, Medium haptic, `PropShake` on the crate, slot flash), `e_ShuttleLaunched` (already juiced in launcher — move those calls here), `e_CryoExpired` (soft alarm beep), `e_ScanStarted/e_ScanResult`, `e_CrateChuted` (Chute sound, `ComicPopup` "+25"), `e_CreditsChanged` (CoinsWidget pulse — already kit), `e_ShiftEnded` (`ScreenFadeTransitionSO` dip). Context button press → Click sound + Selection haptic.
- [ ] **Step 2:** Fill `OcdSounds.asset` with placeholder clips (generated beeps or any CC0 blips Awais has locally); confirm every listed sound plays. Commit `feat: juice presenter — sounds, haptics, tweens`.

### Task 7.2: Rook radio

**Files:** Create `Radio/RadioScriptSO.cs`, `Radio/RookRadio.cs`, `UI/RadioLineView.cs` (typewriter ticker, pattern from SJ `TaskBriefTickerUI`), `ScriptableObjects/Config/RadioScript_Pitch.asset`, `ScriptableObjects/Events/e_RadioBeat (GameEventWithString)`

**Interfaces produced:** `RadioScriptSO { List<RadioLine> Lines; bool TryGet(string beat, out RadioLine line) }`, `RadioLine { string Beat; string Speaker = "Rook"; [TextArea] string Text; float HoldSeconds = 4; }`. `RookRadio` listens to `e_RadioBeat` (raised by `CrateSpawner` when a `SpawnRequest.RadioBeat` is non-empty, and by `ShiftController` on `"ShiftStart"`/`"ShiftEnd"`, by `Bay` on first `"FirstPlace"`, by `ShuttleLauncher` on first `"FirstLaunch"`) and queues lines to `RadioLineView` (top-left of HUD, speaker tag + typewriter, radio blip sound).

- [ ] **Step 1:** Author lines: `Intro` "Evening, rookie. Grey crate on the belt — grab it and walk it to Bay A, the grey one.", `FirstPlace` "Slot's lit. Fill all six and hit the big green button.", `FirstLaunch` "There she goes. Credits in your account — don't spend it all.", `CryoIntro` "White crates are cryo. Bay B, and move — that ring runs out.", `Suspicious` "That one looks off… take it to the booth and scan it.", `Junk` "Rusty junk goes down the chute. Swipe it in.", `Volatile` "Yellow chevrons: volatile. Bay C, slow and steady.", `ShiftEnd` "Shift's over. Not bad for a first night." Also add an arrow/marker to Bay A during `Intro` (simple bobbing arrow prefab toggled by `RookRadio`).
- [ ] **Step 2:** Play test the first 60 s: intro line → crate → arrow to Bay A → launch within a minute. Commit `feat: Rook radio tutorial beats`.

**Slice 7 acceptance:** Every important action has sound + haptic + a tween; Rook's lines appear on cue through the pitch shift; the first launch happens inside 60 s with the arrow guiding the player.

---

## Slice 8 — Art pass (if time) and Android build

### Task 8.1: Android build first

- [ ] **Step 1:** Player settings IL2CPP, ARM64, Release; build to `Builds/OCD_pitch.apk`; install on device; verify touch controls, haptics, 60 fps (`FrameRateSetter`), landscape lock. Commit `chore: Android build settings`.

### Task 8.2: Art pass via `RoomBuilder` (optional)

- [ ] **Step 1:** Ask Awais before downloading: Kenney Space Station Kit / Modular Space Kit / Space Kit / Sci-Fi Sounds (CC0) into `Assets/OrbitalCargo/Art/Kenney/` (own folder, imported as untrusted data — no scripts inside). Extend `RoomLayoutSO` with optional prefab references per role (floor tile, wall, bay pad, booth, chute, shuttle); `RoomBuilder` instantiates them instead of primitives when set. Replace placeholder sounds with Sci-Fi Sounds clips in `OcdSounds.asset`.
- [ ] **Step 2:** Rebuild room, re-test on device, final commit `feat: Kenney art pass`.

---

## Self-review notes

- Spec coverage: KICKOFF slices 1–8 → Slices 1–8 above; every tuning value named in KICKOFF lives in `ShiftConfigSO`, `CrateTypeSO`, `SpawnScheduleSO` or `MovementConfigSO`; GDD controls (joystick left, drag look, context button, swipe-throw) covered in 1.4/2.3/4.5; FTUE 0–1:00 beats in 7.2; out-of-scope items untouched.
- Type consistency: `Crate`, `CrateTypeSO`, `CrateCarrier`, `IInteractable`, `BayModel`, `PayoutRules.Launch`/`LaunchResult`/`CrateSnapshot`, `ShiftConfigSO`, `SpawnScheduleSO.SpawnEntry`/`SpawnRequest`, `ConveyorQueue`, `ShiftClock`, `UpgradeService.CarryCapacity`, event asset names are used with the same names across tasks (added `CrateState.Pickable` in 5.2 and `CryoExpired` on `Crate` in 4.4 — both referenced only after they're introduced).
- Review Focus items each have an owning test/play check (2.1/2.3, 6.3, 4.1/4.2, 4.5, 5.4).
- Proportion: code is limited to signatures and test names; bodies are left to the implementer, who has the kit and SJ sources plus ANALYSIS.md.
