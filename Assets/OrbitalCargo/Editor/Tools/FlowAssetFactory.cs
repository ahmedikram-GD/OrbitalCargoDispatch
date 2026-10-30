using System.IO;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Architecture.Variables;
using GameKit.StateMachine;
using OrbitalCargo.Ads;
using OrbitalCargo.Config;
using OrbitalCargo.Economy;
using OrbitalCargo.Shift;
using OrbitalCargo.Shift.EditorTools;
using OrbitalCargo.UI.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Flow.EditorTools
{
    /// <summary>
    /// Creates and wires everything the shift loop's FSM runs on: flow/shift events, the sorting-order
    /// cursor, the persistent grav-sled and shift-number variables, the ShiftSession / UpgradeService /
    /// RewardedAdAnchor assets, the UIStatesPooler (ShiftHud, PausePanel, ShiftSummary) and FSM_Main
    /// with its three states and transitions. Idempotent: assets are created once, and their wiring is
    /// re-applied every run so a renamed field or a new event can't leave a stale link. Needs the view
    /// prefabs built first (<see cref="FlowViewBuilder"/> / <see cref="HudBuilder"/>).
    /// </summary>
    public static class FlowAssetFactory
    {
        public const string StatesFolder = "Assets/OrbitalCargo/ScriptableObjects/States";
        public const string FsmPath = StatesFolder + "/FSM_Main.asset";
        public const string ShiftStatePath = StatesFolder + "/ShiftGameState.asset";
        public const string PauseStatePath = StatesFolder + "/PauseState.asset";
        public const string SummaryStatePath = StatesFolder + "/ShiftSummaryState.asset";
        public const string ToShiftPath = StatesFolder + "/t_ToShift.asset";
        public const string ToPausePath = StatesFolder + "/t_ToPause.asset";
        public const string ToSummaryPath = StatesFolder + "/t_ToSummary.asset";

        public const string UIStatesPoolerPath = CoreAssetFactory.PoolsFolder + "/UIStatesPooler.asset";
        public const string SortingOrderPath = CoreAssetFactory.VariablesFolder + "/v_CurrentStateSortingOrder.asset";
        public const string GravSledPath = CoreAssetFactory.VariablesFolder + "/vDBBool_GravSled.asset";
        public const string ShiftNumberPath = CoreAssetFactory.VariablesFolder + "/vDBInt_ShiftNumber.asset";
        public const string SessionPath = CoreAssetFactory.ServicesFolder + "/ShiftSession.asset";
        public const string UpgradesPath = CoreAssetFactory.ServicesFolder + "/UpgradeService.asset";
        public const string AdAnchorPath = CoreAssetFactory.ServicesFolder + "/RewardedAdAnchor.asset";

        public const string HudPoolId = "ShiftHud";
        public const string PausePoolId = "PausePanel";
        public const string SummaryPoolId = "ShiftSummary";

        [MenuItem("Tools/OrbitalCargo/Create Flow Assets")]
        public static void CreateAll()
        {
            CreateDataAssets();
            FlowViewBuilder.BuildAll();
            HudBuilder.BuildHudPrefab();
            WireStates();
        }

        /// <summary>Events, variables and services — everything the view prefabs reference.</summary>
        public static void CreateDataAssets()
        {
            // ── Events ──
            foreach (var name in new[]
                     {
                         "e_PausePressed", "e_ResumePressed", "e_ShiftEnded", "e_NextShiftPressed", "e_BackBtnPressed",
                         "e_ShiftGameEnter", "e_ShiftGameExit", "e_ShiftGamePaused", "e_ShiftGameResumed",
                         "e_ShiftStarted", "e_DoublePayoutClaimed", "e_UpgradePurchased",
                     })
                Ensure<GameEvent>(EventPath(name));
            Ensure<GameEventWithInt>(EventPath("e_ShiftTick"));
            Ensure<GameEventWithInt>(EventPath("e_CreditsEarned"));
            Ensure<GameEventWithBool>(EventPath("e_GamePaused"));

            // ── Variables ──
            var sorting = Ensure<Int>(SortingOrderPath);
            Set(sorting, "DefaultValue", 10);
            Set(sorting, "Value", 10);
            Set(sorting, "ResetToDefaultOnPlay", true);
            EnsurePersistent<DBBool>(GravSledPath, "OCD_GravSled");
            var shiftNumber = EnsurePersistent<DBInt>(ShiftNumberPath, "OCD_ShiftNumber");
            Set(shiftNumber, "DefaultValue", 1);

            // ── Services ──
            var credits = CoreAssetFactory.Load<CreditsService>(CoreAssetFactory.CreditsServicePath);
            Assign(credits, "creditsEarned", Event<GameEventWithInt>("e_CreditsEarned"));

            var session = Ensure<ShiftSessionSO>(SessionPath);
            Assign(session, "shiftNumber", shiftNumber);

            var upgrades = Ensure<UpgradeService>(UpgradesPath);
            Assign(upgrades, "gravSled", Load<DBBool>(GravSledPath));
            Assign(upgrades, "config", CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath));
            Assign(upgrades, "credits", credits);
            Assign(upgrades, "onUpgradePurchased", Event<GameEvent>("e_UpgradePurchased"));

            Ensure<RewardedAdAnchorSO>(AdAnchorPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>UIStatesPooler, states, transitions and FSM_Main. Run after the view prefabs exist.</summary>
        public static void WireStates()
        {
            var credits = CoreAssetFactory.Load<CreditsService>(CoreAssetFactory.CreditsServicePath);
            var session = Load<ShiftSessionSO>(SessionPath);
            var upgrades = Load<UpgradeService>(UpgradesPath);
            var adAnchor = Load<RewardedAdAnchorSO>(AdAnchorPath);
            var sorting = Load<Int>(SortingOrderPath);

            // ── UI states pool ──
            var pooler = Ensure<PoolManagerSO>(UIStatesPoolerPath);
            ConfigureViewPool(pooler,
                (HudPoolId, HudBuilder.HudPrefabPath),
                (PausePoolId, FlowViewBuilder.PausePrefabPath),
                (SummaryPoolId, FlowViewBuilder.SummaryPrefabPath));

            // ── States ──
            var shiftState = Ensure<ShiftGameState>(ShiftStatePath);
            Set(shiftState, "gameplayPrefabId", string.Empty);
            Set(shiftState, "hudPrefabId", HudPoolId);
            Set(shiftState, "poolGameplay", false);
            Set(shiftState, "poolGameHud", true);
            Assign(shiftState, "statePooler", pooler);
            Assign(shiftState, "GameStateEnter", Event<GameEvent>("e_ShiftGameEnter"));
            Assign(shiftState, "GameStatePaused", Event<GameEvent>("e_ShiftGamePaused"));
            Assign(shiftState, "GameStateResumed", Event<GameEvent>("e_ShiftGameResumed"));
            Assign(shiftState, "GameStateExit", Event<GameEvent>("e_ShiftGameExit"));
            Assign(shiftState, "onPausePressed", Event<GameEvent>("e_PausePressed"));
            Assign(shiftState, "session", session);

            var pauseState = Ensure<PauseState>(PauseStatePath);
            ConfigureViewState(pauseState, PausePoolId, pooler);
            Assign(pauseState, "onResumePressed", Event<GameEvent>("e_ResumePressed"));
            Assign(pauseState, "onGamePaused", Event<GameEventWithBool>("e_GamePaused"));

            var summaryState = Ensure<ShiftSummaryState>(SummaryStatePath);
            ConfigureViewState(summaryState, SummaryPoolId, pooler);
            Assign(summaryState, "session", session);
            Assign(summaryState, "credits", credits);
            Assign(summaryState, "upgrades", upgrades);
            Assign(summaryState, "ads", adAnchor);
            Assign(summaryState, "onNextShiftPressed", Event<GameEvent>("e_NextShiftPressed"));
            Assign(summaryState, "onDoublePayoutClaimed", Event<GameEvent>("e_DoublePayoutClaimed"));
            Assign(summaryState, "onGamePaused", Event<GameEventWithBool>("e_GamePaused"));

            // ── Transitions + FSM ──
            Assign(Ensure<Transition>(ToShiftPath), "ToState", shiftState);
            Assign(Ensure<Transition>(ToPausePath), "ToState", pauseState);
            Assign(Ensure<Transition>(ToSummaryPath), "ToState", summaryState);

            var fsm = Ensure<FiniteStateMachine>(FsmPath);
            Assign(fsm, "BootState", shiftState);
            Assign(fsm, "currentStateSortingOrder", sorting);
            Set(fsm, "baseSortingOrder", 10);

            AssetDatabase.SaveAssets();
        }

        // ── Loaders (for builders) ─────────────────────────────────────────────

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        public static T Event<T>(string name) where T : Object => Load<T>(EventPath(name));

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string EventPath(string name) => $"{CoreAssetFactory.EventsFolder}/{name}.asset";

        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            var existing = Load<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T EnsurePersistent<T>(string path, string key) where T : ScriptableObject
        {
            var variable = Ensure<T>(path);
            var so = new SerializedObject(variable);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
            return variable;
        }

        private static void ConfigureViewState(ScriptableObject state, string stateId, PoolManagerSO pooler)
        {
            Set(state, "stateId", stateId);
            Set(state, "usePooling", true);
            Assign(state, "uIStatesPooler", pooler);
        }

        private static void ConfigureViewPool(PoolManagerSO pooler, params (string id, string prefabPath)[] views)
        {
            var so = new SerializedObject(pooler);
            var configs = so.FindProperty("_poolConfigs");
            configs.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                var entry = configs.GetArrayElementAtIndex(i);
                var prefab = Load<GameObject>(views[i].prefabPath);
                if (prefab == null) Debug.LogError($"[FlowAssetFactory] Missing view prefab {views[i].prefabPath} — build the UI prefabs first.");
                entry.FindPropertyRelative("PoolID").stringValue = views[i].id;
                entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("DefaultCapacity").intValue = 1;
                entry.FindPropertyRelative("MaxSize").intValue = 2;
                entry.FindPropertyRelative("Prewarm").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[FlowAssetFactory] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[FlowAssetFactory] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            switch (value)
            {
                case int i: prop.intValue = i; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
