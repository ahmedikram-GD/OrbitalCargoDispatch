using System;
using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Config;
using OrbitalCargo.Crates.EditorTools;
using OrbitalCargo.Economy;
using OrbitalCargo.Interaction;
using OrbitalCargo.Room.EditorTools;
using OrbitalCargo.Shift.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Stations.EditorTools
{
    /// <summary>
    /// Adds gameplay components to the geometry <see cref="RoomBuilder"/> produced, by name: a
    /// <see cref="Bay"/> on each <c>Bay_X</c>, a <see cref="Slot"/> on each <c>Slot_X_i</c>, and a
    /// placement ghost per bay. Kept separate from the builder so the art pass can swap geometry
    /// without touching gameplay wiring. Later slices add the launch button, booth and chute here.
    /// </summary>
    public static class StationWiring
    {
        private const string InteractionConfigPath = "Assets/OrbitalCargo/ScriptableObjects/Config/FP_InteractionConfig.asset";
        private const float CrateSize = 0.8f;

        public static void Wire(RoomLayoutSO layout, Transform room)
        {
            var config = AssetDatabase.LoadAssetAtPath<InteractionConfigSO>(InteractionConfigPath);
            var onPlaced = CrateAssetFactory.EnsureCrateEvent("e_CratePlaced");
            var ghostMaterial = GreyboxMaterials.GetTransparent("PlacementGhost", new Color(0.3f, 1f, 0.6f, 0.35f));

            foreach (var anchor in layout.Bays)
            {
                var bayTransform = room.Find($"Bay_{anchor.Id}");
                if (bayTransform == null)
                {
                    Debug.LogWarning($"[StationWiring] Bay_{anchor.Id} not found under {room.name}.");
                    continue;
                }
                WireBay(anchor, bayTransform, layout.SlotsPerBay, config, onPlaced, ghostMaterial);
            }

            WireConveyor(room);
            WireChute(layout, room, config);
            WireBooth(room, config);
            DockArtPass.FinishStationStyling(layout, room);
        }

        // Slice 5: the scanner booth reveal.
        private static void WireBooth(Transform room, InteractionConfigSO interaction)
        {
            var root = room.Find("ScannerBooth");
            if (root == null)
            {
                Debug.LogWarning("[StationWiring] ScannerBooth not found.");
                return;
            }

            var booth = root.gameObject.AddComponent<ScannerBooth>();
            var so = new SerializedObject(booth);
            so.FindProperty("cratePoint").objectReferenceValue = root.Find("CratePoint");
            so.FindProperty("scanLine").objectReferenceValue = root.Find("ScanLine");
            so.FindProperty("statusLight").objectReferenceValue = root.Find("StatusLight")?.GetComponent<Renderer>();
            so.FindProperty("xrayMaterial").objectReferenceValue = GreyboxMaterials.GetTransparent("XRayCasing", new Color(0.4f, 0.9f, 1f, 0.22f));
            so.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            so.FindProperty("interaction").objectReferenceValue = interaction;
            so.FindProperty("onScanStarted").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_ScanStarted");
            so.FindProperty("onScanResult").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_ScanResult");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Slice 4: belt + spawner on the Conveyor root, fed by the crate pool and the pitch schedule.
        private static void WireConveyor(Transform room)
        {
            var root = room.Find("Conveyor");
            if (root == null)
            {
                Debug.LogWarning("[StationWiring] Conveyor not found.");
                return;
            }

            var waypoints = new System.Collections.Generic.List<Transform>();
            for (int i = 0; ; i++)
            {
                var wp = root.Find($"Waypoint_{i}");
                if (wp == null) break;
                waypoints.Add(wp);
            }

            var shiftConfig = CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            var conveyor = root.gameObject.AddComponent<Conveyor>();
            var cso = new SerializedObject(conveyor);
            var wpProp = cso.FindProperty("waypoints");
            wpProp.arraySize = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++) wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            cso.FindProperty("config").objectReferenceValue = shiftConfig;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var spawner = root.gameObject.AddComponent<CrateSpawner>();
            var sso = new SerializedObject(spawner);
            sso.FindProperty("conveyor").objectReferenceValue = conveyor;
            sso.FindProperty("cratePooler").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.CratePoolerPath);
            sso.FindProperty("onCrateSpawned").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CrateSpawned");
            sso.FindProperty("onRadioBeat").objectReferenceValue = CoreAssetFactory.LoadEvent<GameKit.Architecture.Events.GameEventWithString>("e_RadioBeat");
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        // Slice 4: the chute swallows thrown crates (trigger on the root) and offers "Drop" (solid hole plate).
        private static void WireChute(RoomLayoutSO layout, Transform room, InteractionConfigSO config)
        {
            var root = room.Find("Chute");
            if (root == null)
            {
                Debug.LogWarning("[StationWiring] Chute not found.");
                return;
            }

            float dia = layout.ChuteRadius * 2f;
            var trigger = root.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(dia, 1.4f, dia);
            trigger.center = new Vector3(0f, 0.7f, 0f);

            var hole = root.Find("Hole");
            if (hole != null) hole.gameObject.AddComponent<BoxCollider>(); // thin plate the centre ray can hit

            var chute = root.gameObject.AddComponent<Chute>();
            var so = new SerializedObject(chute);
            so.FindProperty("targetPoint").objectReferenceValue = root.Find("TargetPoint");
            so.FindProperty("interaction").objectReferenceValue = config;
            so.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            so.FindProperty("credits").objectReferenceValue = CoreAssetFactory.Load<CreditsService>(CoreAssetFactory.CreditsServicePath);
            so.FindProperty("cratePooler").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.CratePoolerPath);
            so.FindProperty("onCrateChuted").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_CrateChuted");
            so.FindProperty("itemsCollected").objectReferenceValue = CoreAssetFactory.Load<GameKit.Architecture.Variables.DBInt>(CoreAssetFactory.ItemsCollectedPath);
            so.FindProperty("onItemCollected").objectReferenceValue = CrateAssetFactory.EnsureHiddenItemEvent("e_ItemCollected");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBay(RoomLayoutSO.BayAnchor anchor, Transform bayTransform, int slotCount,
                                    InteractionConfigSO config, UnityEngine.Object onPlaced, Material ghostMaterial)
        {
            var slots = new Slot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var slotTransform = bayTransform.Find($"Slots/Slot_{anchor.Id}_{i}");
                if (slotTransform == null)
                {
                    Debug.LogError($"[StationWiring] Slot_{anchor.Id}_{i} missing.", bayTransform);
                    return;
                }
                var slot = slotTransform.gameObject.AddComponent<Slot>();
                var slotSo = new SerializedObject(slot);
                slotSo.FindProperty("index").intValue = i;
                slotSo.ApplyModifiedPropertiesWithoutUndo();
                slots[i] = slot;
            }

            var ghostGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghostGo.name = "Ghost";
            ghostGo.transform.SetParent(bayTransform, false);
            ghostGo.transform.localScale = Vector3.one * CrateSize;
            UnityEngine.Object.DestroyImmediate(ghostGo.GetComponent<Collider>());
            var ghostRenderer = ghostGo.GetComponent<Renderer>();
            ghostRenderer.sharedMaterial = ghostMaterial;
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.enabled = false;
            var ghost = ghostGo.AddComponent<PlacementGhost>();

            var bay = bayTransform.gameObject.AddComponent<Bay>();
            var so = new SerializedObject(bay);
            so.FindProperty("id").enumValueIndex = (int)Enum.Parse(typeof(BayId), anchor.Id);
            var slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("ghost").objectReferenceValue = ghost;
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("onCratePlaced").objectReferenceValue = onPlaced;
            so.FindProperty("onWrongBay").objectReferenceValue = CrateAssetFactory.EnsureCrateEvent("e_WrongBay");
            so.ApplyModifiedPropertiesWithoutUndo();

            WireLaunch(bayTransform, bay, config);
            WireFillCounter(bayTransform, bay);
        }

        // Slice 3: shuttle launcher on the bay, launch button on its pedestal.
        private static void WireLaunch(Transform bayTransform, Bay bay, InteractionConfigSO config)
        {
            var launcher = bayTransform.gameObject.AddComponent<ShuttleLauncher>();
            var so = new SerializedObject(launcher);
            so.FindProperty("bay").objectReferenceValue = bay;
            so.FindProperty("shuttle").objectReferenceValue = bayTransform.Find("Shuttle");
            so.FindProperty("config").objectReferenceValue = CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath);
            so.FindProperty("credits").objectReferenceValue = CoreAssetFactory.Load<CreditsService>(CoreAssetFactory.CreditsServicePath);
            so.FindProperty("cratePooler").objectReferenceValue = CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.CratePoolerPath);
            so.FindProperty("onShuttleLaunched").objectReferenceValue = CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            so.ApplyModifiedPropertiesWithoutUndo();

            var padTransform = bayTransform.Find("LaunchPad");
            if (padTransform == null) return;
            var button = padTransform.gameObject.AddComponent<LaunchButton>();
            var bso = new SerializedObject(button);
            bso.FindProperty("launcher").objectReferenceValue = launcher;
            bso.FindProperty("config").objectReferenceValue = config;
            bso.FindProperty("lamp").objectReferenceValue = padTransform.Find("Lamp")?.GetComponent<Renderer>();
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        // World-space "0 / 6" on the backboard's room-facing side, above the cradle canopy.
        private static void WireFillCounter(Transform bayTransform, Bay bay)
        {
            var backboard = bayTransform.Find("Backboard");
            float frontZ = backboard != null ? backboard.localPosition.z + backboard.localScale.z * 0.5f + 0.02f : 0f;

            var go = new GameObject("FillCounter");
            go.transform.SetParent(bayTransform, false);
            go.transform.localPosition = new Vector3(0f, 2.75f, frontZ);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // read from the room (bay local +Z)

            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            label.fontSize = 5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "0 / 6";

            var view = go.AddComponent<BayFillCounterView>();
            var so = new SerializedObject(view);
            so.FindProperty("bay").objectReferenceValue = bay;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
