using System.Collections.Generic;
using System.IO;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Architecture.Variables;
using GameKit.Haptics;
using OrbitalCargo.Config;
using OrbitalCargo.Crates.EditorTools;
using OrbitalCargo.Economy;
using OrbitalCargo.Juice;
using OrbitalCargo.Room.EditorTools;
using THEBADDEST.SoundSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.Shift.EditorTools
{
    /// <summary>
    /// Creates the shared assets the shift runs on: ShiftConfig_Pitch, the persistent credits variable
    /// and CreditsService, economy events, the three kit pools (crates, particles, UI coins) with their
    /// greybox prefabs, the launch camera shake, and the OcdSounds library (kit SoundService) on the
    /// placeholder clips. Data assets are created once and then belong to the designer; generated
    /// prefabs are rebuilt each run.
    /// </summary>
    public static class CoreAssetFactory
    {
        public const string ConfigFolder = "Assets/OrbitalCargo/ScriptableObjects/Config";
        public const string VariablesFolder = "Assets/OrbitalCargo/ScriptableObjects/Variables";
        public const string EventsFolder = "Assets/OrbitalCargo/ScriptableObjects/Events";
        public const string ServicesFolder = "Assets/OrbitalCargo/ScriptableObjects/Services";
        public const string PoolsFolder = "Assets/OrbitalCargo/ScriptableObjects/Pools";

        public const string ShiftConfigPath = ConfigFolder + "/ShiftConfig_Pitch.asset";
        public const string CreditsPath = VariablesFolder + "/vDBInt_Credits.asset";
        public const string CreditsServicePath = ServicesFolder + "/CreditsService.asset";
        public const string SoundsPath = "Assets/OrbitalCargo/Audio/OcdSounds.asset";
        public const string LaunchShakePath = ConfigFolder + "/Shake_Launch.asset";
        public const string CratePoolerPath = PoolsFolder + "/CratePooler.asset";
        public const string ParticlesPoolerPath = PoolsFolder + "/ParticlesPooler.asset";
        public const string UIPoolerPath = PoolsFolder + "/UIPooler.asset";
        public const string HapticsPath = "Assets/GameKit/Haptics/Scriptables/HapticHandlerSO.asset";

        private const string LaunchSmokePrefabPath = "Assets/OrbitalCargo/Prefabs/Particles/LaunchSmoke.prefab";
        private const string UICoinPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/UICoin.prefab";
        private const string SoundSettingsPath = "Assets/GameKit/Art/Sounds/SoundSettings.asset";
        private const string SoundTogglePath = "Assets/GameKit/Art/Sounds/Scriptables/Sound.asset";
        private const string ClipsFolder = "Assets/OrbitalCargo/Audio/Placeholder";

        [MenuItem("Tools/OrbitalCargo/Create Core Assets")]
        public static void CreateAll()
        {
            EnsureAsset<ShiftConfigSO>(ShiftConfigPath);
            EnsureCredits();
            EnsureEvent<GameEvent>("e_CreditsChanged");
            EnsureEvent<GameEventWithLaunchResult>("e_ShuttleLaunched");
            EnsureEvent<GameEventWithString>("e_RadioBeat");
            EnsureSchedules();
            EnsureCreditsService();
            EnsureLaunchShake();
            EnsurePools();
            EnsureSounds();
            AssetDatabase.SaveAssets();
        }

        // ── Loaders (for builders) ─────────────────────────────────────────────

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        public static T LoadEvent<T>(string fileName) where T : Object => Load<T>($"{EventsFolder}/{fileName}.asset");

        // ── Data ───────────────────────────────────────────────────────────────

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = Load<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T EnsureEvent<T>(string fileName) where T : ScriptableObject => EnsureAsset<T>($"{EventsFolder}/{fileName}.asset");

        private static void EnsureCredits()
        {
            if (Load<DBInt>(CreditsPath) != null) return;
            var credits = EnsureAsset<DBInt>(CreditsPath);
            var so = new SerializedObject(credits);
            so.FindProperty("key").stringValue = "OCD_Credits";
            so.FindProperty("DefaultValue").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureCreditsService()
        {
            if (Load<CreditsService>(CreditsServicePath) != null) return;
            var service = EnsureAsset<CreditsService>(CreditsServicePath);
            var so = new SerializedObject(service);
            so.FindProperty("credits").objectReferenceValue = Load<DBInt>(CreditsPath);
            so.FindProperty("balanceChanged").objectReferenceValue = LoadEvent<GameEvent>("e_CreditsChanged");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLaunchShake()
        {
            if (Load<CameraShakeProfileSO>(LaunchShakePath) != null) return;
            var shake = EnsureAsset<CameraShakeProfileSO>(LaunchShakePath);
            shake.Configure(0.9f, new Vector3(0.05f, 0.06f, 0f), new Vector3(1.2f, 0.8f, 1.6f), 22f);
            EditorUtility.SetDirty(shake);
        }

        // ── Schedules ──────────────────────────────────────────────────────────

        public const string PitchSchedulePath = ConfigFolder + "/SpawnSchedule_Pitch.asset";
        public const string Shift2SchedulePath = ConfigFolder + "/SpawnSchedule_Shift2.asset";

        /// <summary>
        /// The ~5-minute pitch timeline (KICKOFF table), adjusted so Bay A can be filled and launched
        /// inside the first minute: one intro crate, then five more by ~0:40. Shift 2 is the same mix
        /// arriving sooner and closer together. Created once, then owned by the designer.
        /// </summary>
        private static void EnsureSchedules()
        {
            var config = Load<ShiftConfigSO>(ShiftConfigPath);
            var standard = CrateAssetFactory.LoadType("CT_Standard");
            var cryo = CrateAssetFactory.LoadType("CT_Cryo");
            var junk = CrateAssetFactory.LoadType("CT_Junk");
            var volatileType = CrateAssetFactory.LoadType("CT_Volatile");
            var egg = Load<HiddenItemSO>(CrateAssetFactory.AlienEggPath);

            var pitch = Load<SpawnScheduleSO>(PitchSchedulePath);
            if (pitch == null)
            {
                pitch = EnsureAsset<SpawnScheduleSO>(PitchSchedulePath);
                pitch.Entries = new List<SpawnEntry>
                {
                    new SpawnEntry { Time = 3f,   Type = standard,     Count = 1, RadioBeat = "Intro" },
                    new SpawnEntry { Time = 15f,  Type = standard,     Count = 5, Interval = 5f },
                    new SpawnEntry { Time = 60f,  Type = standard,     Count = 6, Interval = 6f },
                    new SpawnEntry { Time = 105f, Type = cryo,         Count = 6, Interval = 8f, RadioBeat = "CryoIntro" },
                    new SpawnEntry { Time = 165f, Type = standard,     Count = 1, Suspicious = true, Hidden = egg, RadioBeat = "Suspicious" },
                    new SpawnEntry { Time = 175f, Type = standard,     Count = 5, Interval = 6f },
                    new SpawnEntry { Time = 210f, Type = junk,         Count = 3, Interval = 8f, RadioBeat = "Junk" },
                    new SpawnEntry { Time = 240f, Type = volatileType, Count = 6, Interval = 7f, RadioBeat = "Volatile" },
                    new SpawnEntry { Time = 270f, Type = cryo,         Count = 3, Interval = 8f },
                };
                EditorUtility.SetDirty(pitch);
            }

            var shift2 = Load<SpawnScheduleSO>(Shift2SchedulePath);
            if (shift2 == null)
            {
                shift2 = EnsureAsset<SpawnScheduleSO>(Shift2SchedulePath);
                shift2.Entries = new List<SpawnEntry>();
                foreach (var e in pitch.Entries)
                {
                    shift2.Entries.Add(new SpawnEntry
                    {
                        Time = e.Time * 0.75f, Type = e.Type, Count = e.Count, Interval = e.Interval * 0.7f,
                        Suspicious = e.Suspicious, Hidden = e.Hidden, RadioBeat = null,
                    });
                }
                EditorUtility.SetDirty(shift2);
            }

            if (config != null && (config.Schedule == null || config.ScheduleShift2 == null))
            {
                if (config.Schedule == null) config.Schedule = pitch;
                if (config.ScheduleShift2 == null) config.ScheduleShift2 = shift2;
                EditorUtility.SetDirty(config);
            }
        }

        // ── Pools ──────────────────────────────────────────────────────────────

        private static void EnsurePools()
        {
            var crate = AssetDatabase.LoadAssetAtPath<GameObject>(CrateAssetFactory.CratePrefabPath);
            ConfigurePool(CratePoolerPath, "Crate", crate, 24, 64, prewarm: false);
            ConfigurePool(ParticlesPoolerPath, "LaunchSmoke", BuildLaunchSmokePrefab(), 3, 6, prewarm: true);
            ConfigurePool(UIPoolerPath, "Coin", BuildUICoinPrefab(), 16, 48, prewarm: true);
        }

        private static void ConfigurePool(string path, string poolId, GameObject prefab, int capacity, int max, bool prewarm)
        {
            var pooler = EnsureAsset<PoolManagerSO>(path);
            var so = new SerializedObject(pooler);
            var configs = so.FindProperty("_poolConfigs");
            configs.arraySize = 1;
            var entry = configs.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("PoolID").stringValue = poolId;
            entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("DefaultCapacity").intValue = capacity;
            entry.FindPropertyRelative("MaxSize").intValue = max;
            entry.FindPropertyRelative("Prewarm").boolValue = prewarm;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildLaunchSmokePrefab()
        {
            var go = new GameObject("LaunchSmoke");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startColor = new Color(0.85f, 0.85f, 0.9f, 0.85f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45), new ParticleSystem.Burst(0.2f, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 1.4f;
            shape.rotation = new Vector3(90f, 0f, 0f); // cone points down: exhaust

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.6f, 0.65f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GreyboxMaterials.GetParticle("Smoke", Color.white);

            return SavePrefab(go, LaunchSmokePrefabPath);
        }

        private static GameObject BuildUICoinPrefab()
        {
            var go = new GameObject("UICoin", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(64f, 64f);
            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            image.color = new Color(1f, 0.82f, 0.2f);
            image.raycastTarget = false;
            return SavePrefab(go, UICoinPrefabPath);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ── Sounds ─────────────────────────────────────────────────────────────

        private static void EnsureSounds()
        {
            if (Load<SoundService>(SoundsPath) != null) return;

            var service = EnsureAsset<SoundService>(SoundsPath);
            // Names are the keys gameplay uses (CLAUDE.md); CollectSpawn is hard-coded by the kit UICollector.
            AddSound(service, "Click", "Click", SoundType.UI, 0.8f);
            AddSound(service, "Pickup", "Pickup", SoundType.SFX, 0.8f);
            AddSound(service, "Place", "Place", SoundType.SFX, 0.9f);
            AddSound(service, "Launch", "Launch", SoundType.SFX, 0.9f);
            AddSound(service, "CollectSpawn", "CoinSpawn", SoundType.UI, 0.35f, randomPitch: true);
            AddSound(service, "CoinsCollect", "CoinsCollect", SoundType.UI, 0.8f);
            AddSound(service, "Alarm", "Alarm", SoundType.SFX, 0.8f);
            AddSound(service, "ScanGood", "ScanGood", SoundType.SFX, 0.8f);
            AddSound(service, "ScanBad", "ScanBad", SoundType.SFX, 0.8f);

            var so = new SerializedObject(service);
            so.FindProperty("Sound").objectReferenceValue = Load<DBBoolWithEvent>(SoundTogglePath);
            so.FindProperty("settings").objectReferenceValue = Load<SoundSettings>(SoundSettingsPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(service);
        }

        private static void AddSound(SoundService service, string name, string clipFile, SoundType type, float volume, bool randomPitch = false)
        {
            var clip = Load<AudioClip>($"{ClipsFolder}/{clipFile}.mp3");
            if (clip == null) Debug.LogWarning($"[CoreAssetFactory] Missing clip {ClipsFolder}/{clipFile}.mp3 for sound '{name}'.");
            service.Add(new Sound
            {
                SoundName = name,
                Type = type,
                AudioClip = clip,
                Volume = volume,
                UseRandomPitch = randomPitch,
            });
        }
    }
}
