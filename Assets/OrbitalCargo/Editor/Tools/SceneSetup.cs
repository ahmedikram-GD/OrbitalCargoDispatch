using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Haptics;
using GameKit.StateMachine;
using OrbitalCargo.Ads;
using OrbitalCargo.Crates;
using OrbitalCargo.Crates.EditorTools;
using OrbitalCargo.Economy;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.FirstPerson.EditorTools;
using OrbitalCargo.Flow;
using OrbitalCargo.Flow.EditorTools;
using OrbitalCargo.Interaction;
using OrbitalCargo.Juice;
using OrbitalCargo.Radio;
using OrbitalCargo.Radio.EditorTools;
using OrbitalCargo.Room.EditorTools;
using OrbitalCargo.Stations;
using OrbitalCargo.Stations.EditorTools;
using OrbitalCargo.UI;
using OrbitalCargo.UI.EditorTools;
using THEBADDEST.SoundSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.Shift.EditorTools
{
    /// <summary>
    /// One-click (or batchmode) regeneration of <c>Game.unity</c>: crate assets, the greybox room and
    /// its station wiring, the Player and Controls prefabs, the EventSystem, cross-prefab scene wiring,
    /// the landscape FSM UI root, and the flow systems (bootstrap, GameFlow, ShiftController, mock ad
    /// service). Idempotent — re-running replaces everything it placed. Each slice extends this rather
    /// than hand-editing the scene.
    /// <para>Refuses to run over unsaved scene changes so it never silently discards someone's edits.</para>
    /// <para>Batchmode: <c>Unity.exe -batchmode -quit -projectPath . -executeMethod
    /// OrbitalCargo.Shift.EditorTools.SceneSetup.SetupGameScene</c></para>
    /// </summary>
    public static class SceneSetup
    {
        public const string GameScenePath = "Assets/OrbitalCargo/Scenes/Game.unity";
        private const string TempCratesName = "TempCrates";
        private const string SystemsName = "Systems";
        private const string StatesRootName = "States";
        private const string UIStatesRootName = "UIStates";

        [MenuItem("Tools/OrbitalCargo/Setup Game Scene")]
        public static void SetupGameScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"[SceneSetup] '{active.name}' has unsaved changes — save or revert them, then run again.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            CrateAssetFactory.CreateAll();
            CoreAssetFactory.CreateAll();
            FlowAssetFactory.CreateDataAssets();

            var layout = RoomBuilder.LoadOrCreateDefaultLayout();
            var room = RoomBuilder.Build(layout);
            StationWiring.Wire(layout, room.transform);

            PlayerRigBuilder.BuildPrefabs();
            HudBuilder.BuildHudPrefab();
            FlowViewBuilder.BuildAll();
            FlowAssetFactory.WireStates();

            // Template camera is replaced by the camera inside the Player prefab.
            DestroyIfPresent("Main Camera");
            DestroyIfPresent("Player");
            DestroyIfPresent("Controls");
            DestroyIfPresent("Hud");
            DestroyIfPresent("EventSystem");
            DestroyIfPresent(SystemsName);
            DestroyIfPresent(TempCratesName);
            DestroyIfPresent(StatesRootName);
            DestroyIfPresent(UIStatesRootName);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.PlayerPrefabPath));
            player.transform.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));

            var controls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.ControlsPrefabPath));
            PlayerRigBuilder.CreateEventSystem();
            var statesRoot = new GameObject(StatesRootName).transform;
            var uiStatesRoot = BuildUIStatesRoot();

            WireControls(player, controls, room.transform);
            var systems = BuildSystems(player, room.transform);
            BuildFlow(systems, player, room.transform, statesRoot, uiStatesRoot);
            BuildRadio(systems, room.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] Game.unity regenerated (room + stations, player, controls, FSM UI root, systems, flow).");
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        // The Player and Controls prefabs are independent, so their links live on the scene instances.
        private static void WireControls(GameObject player, GameObject controls, Transform room)
        {
            var lookArea = controls.GetComponentInChildren<TouchLookArea>(true);
            var input = new SerializedObject(player.GetComponent<MobileMoveInputProvider>());
            input.FindProperty("joystick").objectReferenceValue = controls.GetComponentInChildren<VirtualJoystick>(true);
            input.FindProperty("lookArea").objectReferenceValue = lookArea;
            input.ApplyModifiedPropertiesWithoutUndo();

            var ctx = new SerializedObject(controls.GetComponentInChildren<ContextButtonView>(true));
            ctx.FindProperty("interactor").objectReferenceValue = player.GetComponent<CrateInteractor>();
            ctx.FindProperty("input").objectReferenceValue = player.GetComponent<InteractionInputProvider>();
            ctx.ApplyModifiedPropertiesWithoutUndo();

            var thrower = new SerializedObject(player.GetComponent<JunkThrower>());
            thrower.FindProperty("lookArea").objectReferenceValue = lookArea;
            thrower.FindProperty("chute").objectReferenceValue = room.GetComponentInChildren<Chute>(true);
            thrower.ApplyModifiedPropertiesWithoutUndo();

            // Pause / summary stand the player down and hide the touch controls.
            var paused = FlowAssetFactory.Event<GameEventWithBool>("e_GamePaused");
            var fpc = new SerializedObject(player.GetComponent<FirstPersonController>());
            fpc.FindProperty("onGamePaused").objectReferenceValue = paused;
            fpc.ApplyModifiedPropertiesWithoutUndo();

            var visibility = new SerializedObject(controls.AddComponent<ControlsVisibility>());
            visibility.FindProperty("group").objectReferenceValue = controls.GetComponent<CanvasGroup>();
            visibility.FindProperty("onGamePaused").objectReferenceValue = paused;
            visibility.ApplyModifiedPropertiesWithoutUndo();
        }

        // Landscape parent for every FSM view (the kit's UIStates.prefab is portrait 1080x1920).
        private static Transform BuildUIStatesRoot()
        {
            var root = new GameObject(UIStatesRootName, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return root.transform;
        }

        // FSM boot, flow driver, shift loop and the mock rewarded-ad service.
        private static void BuildFlow(GameObject systems, GameObject player, Transform room, Transform statesRoot, Transform uiStatesRoot)
        {
            var flow = systems.AddComponent<GameFlow>();
            var flowSo = new SerializedObject(flow);
            flowSo.FindProperty("finiteStateMachine").objectReferenceValue = FlowAssetFactory.Load<FiniteStateMachine>(FlowAssetFactory.FsmPath);
            flowSo.FindProperty("backBtnPressedEvent").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_BackBtnPressed");
            flowSo.FindProperty("toShift").objectReferenceValue = FlowAssetFactory.Load<Transition>(FlowAssetFactory.ToShiftPath);
            flowSo.FindProperty("toPause").objectReferenceValue = FlowAssetFactory.Load<Transition>(FlowAssetFactory.ToPausePath);
            flowSo.FindProperty("toSummary").objectReferenceValue = FlowAssetFactory.Load<Transition>(FlowAssetFactory.ToSummaryPath);
            flowSo.FindProperty("onPausePressed").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_PausePressed");
            flowSo.FindProperty("onResumePressed").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ResumePressed");
            flowSo.FindProperty("onShiftEnded").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftEnded");
            flowSo.FindProperty("onNextShiftPressed").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_NextShiftPressed");
            flowSo.FindProperty("onBackPressed").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_BackBtnPressed");
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            var bso = new SerializedObject(systems.GetComponent<GameBootstrap>());
            bso.FindProperty("fsm").objectReferenceValue = FlowAssetFactory.Load<FiniteStateMachine>(FlowAssetFactory.FsmPath);
            bso.FindProperty("gameFlow").objectReferenceValue = flow;
            bso.FindProperty("statesRoot").objectReferenceValue = statesRoot;
            bso.FindProperty("uiStatesRoot").objectReferenceValue = uiStatesRoot;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var shift = systems.AddComponent<ShiftController>();
            var sso = new SerializedObject(shift);
            sso.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<Config.ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            sso.FindProperty("session").objectReferenceValue = FlowAssetFactory.Load<ShiftSessionSO>(FlowAssetFactory.SessionPath);
            sso.FindProperty("upgrades").objectReferenceValue = FlowAssetFactory.Load<UpgradeService>(FlowAssetFactory.UpgradesPath);
            sso.FindProperty("cratePooler").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.CratePoolerPath);
            sso.FindProperty("spawner").objectReferenceValue = room.GetComponentInChildren<CrateSpawner>(true);
            sso.FindProperty("conveyor").objectReferenceValue = room.GetComponentInChildren<Conveyor>(true);
            sso.FindProperty("carrier").objectReferenceValue = player.GetComponent<CrateCarrier>();
            sso.FindProperty("scanner").objectReferenceValue = room.GetComponentInChildren<ScannerBooth>(true);
            var bays = room.GetComponentsInChildren<Bay>(true);
            var baysProp = sso.FindProperty("bays");
            baysProp.arraySize = bays.Length;
            for (int i = 0; i < bays.Length; i++) baysProp.GetArrayElementAtIndex(i).objectReferenceValue = bays[i];
            sso.FindProperty("onShiftGameEnter").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftGameEnter");
            sso.FindProperty("onShuttleLaunched").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            sso.FindProperty("onItemCollected").objectReferenceValue = CrateAssetFactory.EnsureHiddenItemEvent("e_ItemCollected");
            sso.FindProperty("onCreditsEarned").objectReferenceValue = FlowAssetFactory.Event<GameEventWithInt>("e_CreditsEarned");
            sso.FindProperty("onShiftStarted").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftStarted");
            sso.FindProperty("onShiftTick").objectReferenceValue = FlowAssetFactory.Event<GameEventWithInt>("e_ShiftTick");
            sso.FindProperty("onShiftEnded").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftEnded");
            sso.ApplyModifiedPropertiesWithoutUndo();

            // Mock rewarded ad: SJ FakeRewardedAdService (2 s, real time) + overlay, published to the anchor.
            var adGo = new GameObject("AdService");
            adGo.transform.SetParent(systems.transform, false);
            var overlay = FlowViewBuilder.BuildAdOverlay(adGo.transform);
            var ad = new SerializedObject(adGo.AddComponent<FakeRewardedAdService>());
            ad.FindProperty("_fakeDuration").floatValue = 2f;
            ad.FindProperty("_overlay").objectReferenceValue = overlay;
            ad.ApplyModifiedPropertiesWithoutUndo();
            var registrar = new SerializedObject(adGo.AddComponent<RewardedAdRegistrar>());
            registrar.FindProperty("adServiceSource").objectReferenceValue = adGo;
            registrar.FindProperty("anchor").objectReferenceValue = FlowAssetFactory.Load<RewardedAdAnchorSO>(FlowAssetFactory.AdAnchorPath);
            registrar.ApplyModifiedPropertiesWithoutUndo();
        }

        // Scene-level services: the bootstrap (sound + pools) and the juice presenter (events → feedback).
        private static GameObject BuildSystems(GameObject player, Transform room)
        {
            var systems = new GameObject(SystemsName);

            var bootstrap = systems.AddComponent<GameBootstrap>();
            var bso = new SerializedObject(bootstrap);
            bso.FindProperty("soundService").objectReferenceValue = CoreAssetFactory.Load<SoundService>(CoreAssetFactory.SoundsPath);
            var pools = bso.FindProperty("pools");
            string[] poolPaths = { CoreAssetFactory.CratePoolerPath, CoreAssetFactory.ParticlesPoolerPath, CoreAssetFactory.UIPoolerPath, FlowAssetFactory.UIStatesPoolerPath };
            pools.arraySize = poolPaths.Length;
            for (int i = 0; i < poolPaths.Length; i++)
                pools.GetArrayElementAtIndex(i).objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(poolPaths[i]);
            bso.ApplyModifiedPropertiesWithoutUndo();

            var juice = systems.AddComponent<JuicePresenter>();
            var jso = new SerializedObject(juice);
            jso.FindProperty("sound").objectReferenceValue = CoreAssetFactory.Load<SoundService>(CoreAssetFactory.SoundsPath);
            jso.FindProperty("haptics").objectReferenceValue = CoreAssetFactory.Load<HapticHandlerSO>(CoreAssetFactory.HapticsPath);
            jso.FindProperty("particles").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.ParticlesPoolerPath);
            jso.FindProperty("shaker").objectReferenceValue = player.GetComponentInChildren<CameraShaker>(true);
            jso.FindProperty("bob").objectReferenceValue = player.GetComponentInChildren<CameraBobController>(true);
            jso.FindProperty("onCratePicked").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CratePicked");
            jso.FindProperty("onCratePlaced").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CratePlaced");
            jso.FindProperty("onShuttleLaunched").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            jso.FindProperty("onCryoExpired").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CryoExpired");
            jso.FindProperty("onCrateThrown").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CrateThrown");
            jso.FindProperty("onCrateChuted").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CrateChuted");
            jso.FindProperty("onScanStarted").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_ScanStarted");
            jso.FindProperty("onScanResult").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_ScanResult");
            jso.FindProperty("onItemCollected").objectReferenceValue = CrateAssetFactory.EnsureHiddenItemEvent("e_ItemCollected");
            jso.FindProperty("onNewsAlert").objectReferenceValue = CoreAssetFactory.LoadEvent<GameKit.Architecture.Events.GameEventWithString>("e_NewsAlert");
            jso.FindProperty("launchShake").objectReferenceValue = CoreAssetFactory.Load<CameraShakeProfileSO>(CoreAssetFactory.LaunchShakePath);
            jso.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<Config.ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            jso.FindProperty("radioScript").objectReferenceValue = Radio.EditorTools.RadioAssetFactory.EnsureScript();
            jso.FindProperty("onRadioBeat").objectReferenceValue = CoreAssetFactory.LoadEvent<GameKit.Architecture.Events.GameEventWithString>("e_RadioBeat");
            jso.FindProperty("onShiftEnded").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftEnded");
            AssignEvents(jso.FindProperty("onUiPressed"), "e_PausePressed", "e_ResumePressed", "e_NextShiftPressed");
            AssignEvents(jso.FindProperty("onPurchase"), "e_DoublePayoutClaimed", "e_UpgradePurchased");
            jso.ApplyModifiedPropertiesWithoutUndo();

            // Editor test aid: 1-4 drop a standard / cryo / junk / volatile crate on the belt.
            var keys = systems.AddComponent<SpawnDebugKeys>();
            var kso = new SerializedObject(keys);
            kso.FindProperty("spawner").objectReferenceValue = room.GetComponentInChildren<CrateSpawner>(true);
            string[] keyTypes = { "CT_Standard", "CT_Cryo", "CT_Junk", "CT_Volatile" };
            var keyTypesProp = kso.FindProperty("crateTypes");
            keyTypesProp.arraySize = keyTypes.Length;
            for (int i = 0; i < keyTypes.Length; i++)
                keyTypesProp.GetArrayElementAtIndex(i).objectReferenceValue = CrateAssetFactory.LoadType(keyTypes[i]);
            kso.FindProperty("suspiciousItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Config.HiddenItemSO>(CrateAssetFactory.AlienEggPath);
            kso.ApplyModifiedPropertiesWithoutUndo();

            // Contraband consequence: fine + news ticker after a launch that shipped bad cargo.
            var news = systems.AddComponent<NewsDesk>();
            var nso = new SerializedObject(news);
            nso.FindProperty("onShuttleLaunched").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            nso.FindProperty("onNewsAlert").objectReferenceValue = CoreAssetFactory.LoadEvent<GameKit.Architecture.Events.GameEventWithString>("e_NewsAlert");
            nso.FindProperty("credits").objectReferenceValue = CoreAssetFactory.Load<CreditsService>(CoreAssetFactory.CreditsServicePath);
            nso.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<Config.ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            nso.ApplyModifiedPropertiesWithoutUndo();

            // Editor test aid: F fills the aimed-at bay.
            var filler = systems.AddComponent<Stations.BayDebugFiller>();
            var fso = new SerializedObject(filler);
            fso.FindProperty("interactor").objectReferenceValue = player.GetComponent<CrateInteractor>();
            fso.FindProperty("cratePooler").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.CratePoolerPath);
            string[] types = { "CT_Standard", "CT_Cryo", "CT_Volatile", "CT_Junk" };
            var typesProp = fso.FindProperty("crateTypes");
            typesProp.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++)
                typesProp.GetArrayElementAtIndex(i).objectReferenceValue = CrateAssetFactory.LoadType(types[i]);
            fso.ApplyModifiedPropertiesWithoutUndo();
            return systems;
        }

        private static void AssignEvents(SerializedProperty array, params string[] names)
        {
            array.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = FlowAssetFactory.Event<GameEvent>(names[i]);
        }

        // Rook: beat director + the guide arrow over Bay A.
        private static void BuildRadio(GameObject systems, Transform room)
        {
            GuideArrow arrow = null;
            foreach (var bay in room.GetComponentsInChildren<Bay>(true))
            {
                if (bay.Id != Config.BayId.A) continue;
                arrow = RadioAssetFactory.BuildArrow(room, bay.transform.position + Vector3.up * 2.6f);
                break;
            }

            var radio = new SerializedObject(systems.AddComponent<RookRadio>());
            radio.FindProperty("session").objectReferenceValue = FlowAssetFactory.Load<ShiftSessionSO>(FlowAssetFactory.SessionPath);
            radio.FindProperty("arrow").objectReferenceValue = arrow;
            radio.FindProperty("onRadioBeat").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithString>("e_RadioBeat");
            radio.FindProperty("onShiftStarted").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftStarted");
            radio.FindProperty("onShiftEnded").objectReferenceValue = FlowAssetFactory.Event<GameEvent>("e_ShiftEnded");
            radio.FindProperty("onCratePlaced").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CratePlaced");
            radio.FindProperty("onShuttleLaunched").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            radio.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DestroyIfPresent(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
