using System.IO;
using OrbitalCargo.Config;
using OrbitalCargo.Interaction;
using OrbitalCargo.Room.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Crates.EditorTools
{
    /// <summary>
    /// Creates the crate data and prefab: one <see cref="CrateTypeSO"/> per type (values from the GDD
    /// and PLAN — created once, then owned by the designer), the crate event channels, and the
    /// greybox <c>Crate.prefab</c> (rebuilt every run so code changes to its structure always land).
    /// <para>Crate layout: root pivot at the crate's BOTTOM centre (sits on belts and pads without
    /// offsets) → Casing (0.8 m cube, collider) + Stripe (colour band, no collider).</para>
    /// </summary>
    public static class CrateAssetFactory
    {
        public const string CratePrefabPath = "Assets/OrbitalCargo/Prefabs/Crates/Crate.prefab";
        public const string CratesFolder = "Assets/OrbitalCargo/ScriptableObjects/Crates";
        public const string EventsFolder = "Assets/OrbitalCargo/ScriptableObjects/Events";
        private const string HighlightConfigPath = "Assets/OrbitalCargo/ScriptableObjects/Config/FP_HighlightConfig.asset";

        private const float CrateSize = 0.8f;

        [MenuItem("Tools/OrbitalCargo/Create Crate Assets")]
        public static void CreateAll()
        {
            EnsureCrateTypes();
            EnsureCrateEvent("e_CratePicked");
            EnsureCrateEvent("e_CratePlaced");
            BuildCratePrefab();
            AssetDatabase.SaveAssets();
        }

        // ── Crate types ────────────────────────────────────────────────────────

        public static void EnsureCrateTypes()
        {
            EnsureType("CT_Standard", "standard", "Standard", new Color(0.42f, 0.50f, 0.60f), new Color(0.10f, 0.10f, 0.12f), BayId.A, 10);
            EnsureType("CT_Cryo", "cryo", "Cryo", new Color(0.92f, 0.95f, 0.98f), new Color(0.20f, 0.85f, 1.00f), BayId.B, 15,
                       t => t.CryoSeconds = 45f);
            EnsureType("CT_Junk", "junk", "Junk", new Color(0.50f, 0.28f, 0.14f), new Color(0.85f, 0.10f, 0.10f), BayId.Chute, 5,
                       t => t.IsJunk = true);
            EnsureType("CT_Volatile", "volatile", "Volatile", new Color(1.00f, 0.78f, 0.08f), new Color(0.05f, 0.05f, 0.05f), BayId.C, 20,
                       t => { t.VolatileSlowMultiplier = 0.6f; t.WobbleAmplitude = 0.04f; });
        }

        public static CrateTypeSO LoadType(string fileName) =>
            AssetDatabase.LoadAssetAtPath<CrateTypeSO>($"{CratesFolder}/{fileName}.asset");

        private static void EnsureType(string fileName, string id, string displayName, Color casing, Color accent,
                                       BayId destination, int payout, System.Action<CrateTypeSO> twist = null)
        {
            if (LoadType(fileName) != null) return; // designer owns it after creation

            Directory.CreateDirectory(CratesFolder);
            var type = ScriptableObject.CreateInstance<CrateTypeSO>();
            type.Id = id;
            type.DisplayName = displayName;
            type.CasingMaterial = GreyboxMaterials.Get($"Crate_{displayName}", casing);
            type.AccentColor = accent;
            type.Destination = destination;
            type.Payout = payout;
            twist?.Invoke(type);
            AssetDatabase.CreateAsset(type, $"{CratesFolder}/{fileName}.asset");
        }

        // ── Events ─────────────────────────────────────────────────────────────

        public static GameEventWithCrate EnsureCrateEvent(string fileName)
        {
            string path = $"{EventsFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameEventWithCrate>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(EventsFolder);
            var evt = ScriptableObject.CreateInstance<GameEventWithCrate>();
            AssetDatabase.CreateAsset(evt, path);
            return evt;
        }

        // ── Prefab ─────────────────────────────────────────────────────────────

        public static GameObject BuildCratePrefab()
        {
            var root = new GameObject("Crate");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var carryable = root.AddComponent<CarryableCrate>();
            var visuals = root.AddComponent<CrateVisuals>();
            var crate = root.AddComponent<Crate>();
            var highlight = root.AddComponent<HighlightableItem>();

            var casing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            casing.name = "Casing";
            casing.transform.SetParent(root.transform, false);
            casing.transform.localPosition = new Vector3(0f, CrateSize * 0.5f, 0f);
            casing.transform.localScale = Vector3.one * CrateSize;

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Stripe";
            stripe.transform.SetParent(root.transform, false);
            stripe.transform.localPosition = new Vector3(0f, CrateSize * 0.72f, 0f);
            stripe.transform.localScale = new Vector3(CrateSize * 1.02f, CrateSize * 0.14f, CrateSize * 1.02f);
            stripe.GetComponent<Renderer>().sharedMaterial = GreyboxMaterials.Get("CrateStripe", Color.white);
            Object.DestroyImmediate(stripe.GetComponent<Collider>());

            var standard = LoadType("CT_Standard");
            if (standard != null) casing.GetComponent<Renderer>().sharedMaterial = standard.CasingMaterial;

            Assign(visuals, "casing", casing.GetComponent<Renderer>());
            Assign(visuals, "stripe", stripe.GetComponent<Renderer>());
            Assign(crate, "type", standard);
            Assign(crate, "visuals", visuals);
            Assign(crate, "carryable", carryable);
            Assign(highlight, "config", AssetDatabase.LoadAssetAtPath<HighlightConfigSO>(HighlightConfigPath));

            Directory.CreateDirectory(Path.GetDirectoryName(CratePrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CratePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[CrateAssetFactory] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
