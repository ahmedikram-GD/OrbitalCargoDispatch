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

            WireControls(player, controls);
            BuildSystems(player);
            PlaceTempCrates(layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] Game.unity regenerated (room + stations, player, controls, event system, test crates).");
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        // The Player and Controls prefabs are independent, so their links live on the scene instances.
        private static void WireControls(GameObject player, GameObject controls)
        {
            var input = new SerializedObject(player.GetComponent<MobileMoveInputProvider>());
            input.FindProperty("joystick").objectReferenceValue = controls.GetComponentInChildren<VirtualJoystick>(true);
            input.FindProperty("lookArea").objectReferenceValue = controls.GetComponentInChildren<TouchLookArea>(true);
            input.ApplyModifiedPropertiesWithoutUndo();

            var ctx = new SerializedObject(controls.GetComponentInChildren<ContextButtonView>(true));
            ctx.FindProperty("interactor").objectReferenceValue = player.GetComponent<CrateInteractor>();
            ctx.FindProperty("input").objectReferenceValue = player.GetComponent<InteractionInputProvider>();
            ctx.ApplyModifiedPropertiesWithoutUndo();
        }

        // Scene-level services: the bootstrap (sound + pools) and the juice presenter (events → feedback).
        private static void BuildSystems(GameObject player)
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
            jso.FindProperty("launchShake").objectReferenceValue = CoreAssetFactory.Load<CameraShakeProfileSO>(CoreAssetFactory.LaunchShakePath);
            jso.ApplyModifiedPropertiesWithoutUndo();

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

        // Slice 2 stand-in for the spawner: a standard, a cryo and a volatile crate resting on the belt.
        private static void PlaceTempCrates(Config.RoomLayoutSO layout)
        {
            if (layout.ConveyorWaypoints.Length < 2) return;

            var root = new GameObject(TempCratesName).transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrateAssetFactory.CratePrefabPath);
            Vector3 a = layout.ConveyorWaypoints[0];
            Vector3 b = layout.ConveyorWaypoints[layout.ConveyorWaypoints.Length - 1];
            Quaternion facing = Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.z - a.z).normalized, Vector3.up);

            string[] types = { "CT_Standard", "CT_Cryo", "CT_Volatile" };
            float[] along = { 0.55f, 0.72f, 0.89f };
            for (int i = 0; i < types.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = $"Crate_{types[i]}";
                go.transform.SetPositionAndRotation(Vector3.Lerp(a, b, along[i]), facing);

                var type = CrateAssetFactory.LoadType(types[i]);
                var so = new SerializedObject(go.GetComponent<Crate>());
                so.FindProperty("type").objectReferenceValue = type;
                so.ApplyModifiedPropertiesWithoutUndo();
                go.GetComponent<CrateVisuals>().Apply(type); // so the editor view matches play mode
            }
        }

        private static void DestroyIfPresent(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
