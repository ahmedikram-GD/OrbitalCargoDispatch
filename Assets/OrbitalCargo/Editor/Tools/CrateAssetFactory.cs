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

        private const string RingSpritePath = "Assets/OrbitalCargo/Art/Sprites/CryoRing.png";
        public const string AlienEggPath = CratesFolder + "/HI_AlienEgg.asset";

        [MenuItem("Tools/OrbitalCargo/Create Crate Assets")]
        public static void CreateAll()
        {
            EnsureCrateTypes();
            EnsureHiddenItems();
            foreach (var evt in new[] { "e_CratePicked", "e_CratePlaced", "e_CrateSpawned", "e_CryoExpired", "e_CrateThrown", "e_CrateChuted", "e_ScanStarted", "e_ScanResult" })
                EnsureCrateEvent(evt);
            EnsureHiddenItemEvent("e_ItemCollected");
            BuildCratePrefab();
            AssetDatabase.SaveAssets();
        }

        private const string AlienEggPrefabPath = "Assets/OrbitalCargo/Prefabs/Crates/Hidden/AlienEgg.prefab";

        private static void EnsureHiddenItems()
        {
            var egg = AssetDatabase.LoadAssetAtPath<HiddenItemSO>(AlienEggPath);
            if (egg == null)
            {
                Directory.CreateDirectory(CratesFolder);
                egg = ScriptableObject.CreateInstance<HiddenItemSO>();
                egg.Id = "alien_egg";
                egg.DisplayName = "Alien Egg";
                AssetDatabase.CreateAsset(egg, AlienEggPath);
            }
            if (egg.Prefab == null)
            {
                egg.Prefab = BuildAlienEggPrefab();
                EditorUtility.SetDirty(egg);
            }
        }

        // Greybox egg: a glowing green ovoid that reads instantly through the X-ray casing.
        private static GameObject BuildAlienEggPrefab()
        {
            var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            egg.name = "AlienEgg";
            egg.transform.localScale = new Vector3(0.34f, 0.46f, 0.34f);
            Object.DestroyImmediate(egg.GetComponent<Collider>());
            egg.GetComponent<Renderer>().sharedMaterial = GreyboxMaterials.Get("AlienEgg", new Color(0.35f, 1f, 0.25f), emission: 1.6f);
            Directory.CreateDirectory(Path.GetDirectoryName(AlienEggPrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(egg, AlienEggPrefabPath);
            Object.DestroyImmediate(egg);
            return prefab;
        }

        public static GameEventWithHiddenItem EnsureHiddenItemEvent(string fileName)
        {
            string path = $"{EventsFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameEventWithHiddenItem>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(EventsFolder);
            var evt = ScriptableObject.CreateInstance<GameEventWithHiddenItem>();
            AssetDatabase.CreateAsset(evt, path);
            return evt;
        }

        /// <summary>A soft white ring (generated once) for the cryo timer's radial fill.</summary>
        private static Sprite EnsureRingSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float centre = (size - 1) * 0.5f;
            float outer = size * 0.5f - 1f;
            float inner = outer * 0.72f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(RingSpritePath));
            File.WriteAllBytes(RingSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RingSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(RingSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
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
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // thrown junk mustn't tunnel
            var carryable = root.AddComponent<CarryableCrate>();
            var visuals = root.AddComponent<CrateVisuals>();
            var crate = root.AddComponent<Crate>();
            var highlight = root.AddComponent<HighlightableItem>();
            root.AddComponent<Juice.PropShake>(); // landing wobble, triggered by JuicePresenter

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

            BuildCryoRing(root, crate);
            BuildVolatileBeacon(root, crate);

            // Where a hidden item's model appears during the X-ray (crate centre).
            var hiddenAnchor = new GameObject("HiddenItem").transform;
            hiddenAnchor.SetParent(root.transform, false);
            hiddenAnchor.localPosition = new Vector3(0f, CrateSize * 0.5f, 0f);
            Assign(visuals, "hiddenAnchor", hiddenAnchor);

            BuildSuspiciousTell(root, crate, casing.transform, stripe.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(CratePrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CratePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Cryo ring: a small world-space canvas lying flat on the lid (front facing up), hidden by default.
        private static void BuildCryoRing(GameObject root, Crate crate)
        {
            var sprite = EnsureRingSprite();

            var ringGo = new GameObject("CryoRing", typeof(RectTransform));
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.SetParent(root.transform, false);
            ringRt.sizeDelta = new Vector2(100f, 100f);
            ringRt.localPosition = new Vector3(0f, CrateSize + 0.005f, 0f);
            ringRt.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringRt.localScale = Vector3.one * (CrateSize * 0.9f / 100f);
            var canvas = ringGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var back = NewImage("Back", ringRt, sprite, new Color(0f, 0f, 0f, 0.45f));
            var fill = NewImage("Fill", ringRt, sprite, new Color(0.2f, 0.9f, 1f));
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            fill.fillClockwise = false;
            ringGo.SetActive(false);

            var view = root.AddComponent<CryoRingView>();
            Assign(view, "root", ringGo);
            Assign(view, "fill", fill);

            var timer = root.AddComponent<CryoTimer>();
            Assign(timer, "crate", crate);
            Assign(timer, "ring", view);
            Assign(timer, "onCryoExpired", EnsureCrateEvent("e_CryoExpired"));
        }

        // Volatile beacon: a small amber lamp on a lid corner; VolatileEffect strobes it for volatile types only.
        private static void BuildVolatileBeacon(GameObject root, Crate crate)
        {
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(CrateSize * 0.33f, CrateSize + 0.07f, CrateSize * 0.33f);
            beacon.transform.localScale = Vector3.one * 0.14f;
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            var renderer = beacon.GetComponent<Renderer>();
            renderer.sharedMaterial = GreyboxMaterials.Get("Beacon", new Color(1f, 0.6f, 0.05f), emission: 3f);
            renderer.enabled = false;

            var effect = root.AddComponent<VolatileEffect>();
            Assign(effect, "crate", crate);
            Assign(effect, "beacon", renderer);
        }

        // Suspicious tells: body shake plus sparks (crackling on the lid) or green goo (dripping off the edges).
        private static void BuildSuspiciousTell(GameObject root, Crate crate, Transform casing, Transform stripe)
        {
            var tells = new GameObject("Tells").transform;
            tells.SetParent(root.transform, false);

            var sparks = NewParticles("Sparks", tells, new Vector3(0f, CrateSize + 0.02f, 0f), GreyboxMaterials.GetParticle("Spark", new Color(1f, 0.85f, 0.3f)));
            var sm = sparks.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            sm.startColor = new Color(1f, 0.85f, 0.3f);
            sm.gravityModifier = 1.2f;
            var se = sparks.emission;
            se.rateOverTime = 0f;
            se.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 10, -1, 0.5f) }); // crackle every half second
            var ss = sparks.shape;
            ss.shapeType = ParticleSystemShapeType.Box;
            ss.scale = new Vector3(CrateSize * 0.9f, 0.02f, CrateSize * 0.9f);

            var drip = NewParticles("Drip", tells, new Vector3(0f, CrateSize * 0.92f, 0f), GreyboxMaterials.GetParticle("Goo", new Color(0.35f, 1f, 0.2f)));
            var dm = drip.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            dm.startColor = new Color(0.35f, 1f, 0.2f);
            dm.gravityModifier = 0.9f;
            var de = drip.emission;
            de.rateOverTime = 7f;
            var ds = drip.shape;
            ds.shapeType = ParticleSystemShapeType.BoxEdge;
            ds.scale = new Vector3(CrateSize * 1.02f, 0.02f, CrateSize * 1.02f);

            var tell = root.AddComponent<SuspiciousTell>();
            var so = new SerializedObject(tell);
            so.FindProperty("crate").objectReferenceValue = crate;
            var parts = so.FindProperty("shakeParts");
            parts.arraySize = 2;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = casing;
            parts.GetArrayElementAtIndex(1).objectReferenceValue = stripe;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.FindProperty("drip").objectReferenceValue = drip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem NewParticles(string name, Transform parent, Vector3 localPosition, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        private static UnityEngine.UI.Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
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
