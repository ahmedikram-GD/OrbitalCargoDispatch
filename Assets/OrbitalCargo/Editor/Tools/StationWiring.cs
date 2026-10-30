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
