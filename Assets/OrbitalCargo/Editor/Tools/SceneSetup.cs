using GameKit.Architecture.PoolSystem;
using GameKit.Haptics;
using OrbitalCargo.Crates;
using OrbitalCargo.Crates.EditorTools;
using OrbitalCargo.Economy;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.FirstPerson.EditorTools;
using OrbitalCargo.Interaction;
using OrbitalCargo.Juice;
using OrbitalCargo.Room.EditorTools;
using OrbitalCargo.Stations;
using OrbitalCargo.Stations.EditorTools;
using OrbitalCargo.UI;
using OrbitalCargo.UI.EditorTools;
using THEBADDEST.SoundSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrbitalCargo.Shift.EditorTools
{
    /// <summary>
    /// One-click (or batchmode) regeneration of <c>Game.unity</c>: crate assets, the greybox room and
    /// its station wiring, the Player and Controls prefabs, the EventSystem, cross-prefab scene wiring,
    /// and (until the conveyor lands in Slice 4) a few test crates on the belt. Idempotent — re-running
    /// replaces everything it placed. Each slice extends this rather than hand-editing the scene.
    /// <para>Refuses to run over unsaved scene changes so it never silently discards someone's edits.</para>
    /// <para>Batchmode: <c>Unity.exe -batchmode -quit -projectPath . -executeMethod
    /// OrbitalCargo.Shift.EditorTools.SceneSetup.SetupGameScene</c></para>
    /// </summary>
    public static class SceneSetup
    {
        public const string GameScenePath = "Assets/OrbitalCargo/Scenes/Game.unity";
        private const string TempCratesName = "TempCrates";
        private const string SystemsName = "Systems";

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

            var layout = RoomBuilder.LoadOrCreateDefaultLayout();
            var room = RoomBuilder.Build(layout);
            StationWiring.Wire(layout, room.transform);

            PlayerRigBuilder.BuildPrefabs();
            HudBuilder.BuildHudPrefab();

            // Template camera is replaced by the camera inside the Player prefab.
            DestroyIfPresent("Main Camera");
            DestroyIfPresent("Player");
            DestroyIfPresent("Controls");
            DestroyIfPresent("Hud");
            DestroyIfPresent("EventSystem");
            DestroyIfPresent(SystemsName);
            DestroyIfPresent(TempCratesName);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.PlayerPrefabPath));
            player.transform.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));

            var controls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.ControlsPrefabPath));
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.HudPrefabPath));
            PlayerRigBuilder.CreateEventSystem();

            WireControls(player, controls, room.transform);
            BuildSystems(player, room.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] Game.unity regenerated (room + stations, player, controls, HUD, systems).");
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
        }

        // Scene-level services: the bootstrap (sound + pools) and the juice presenter (events → feedback).
        private static void BuildSystems(GameObject player, Transform room)
        {
            var systems = new GameObject(SystemsName);

            var bootstrap = systems.AddComponent<GameBootstrap>();
            var bso = new SerializedObject(bootstrap);
            bso.FindProperty("soundService").objectReferenceValue = CoreAssetFactory.Load<SoundService>(CoreAssetFactory.SoundsPath);
            var pools = bso.FindProperty("pools");
            string[] poolPaths = { CoreAssetFactory.CratePoolerPath, CoreAssetFactory.ParticlesPoolerPath, CoreAssetFactory.UIPoolerPath };
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
        }

        private static void DestroyIfPresent(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
