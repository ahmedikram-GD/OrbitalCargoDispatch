using System.IO;
using OrbitalCargo.Config;
using OrbitalCargo.Shift.EditorTools;
using THEBADDEST.SoundSystem;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// The Kenney art pass as one reproducible step: fills the room layout's art skin (Space Station Kit
    /// floor and walls, Space Kit cargo ship, set-dressing props), makes the tinted kit materials, and
    /// points the physical sounds at Sci-Fi Sounds clips. All packs are CC0 (kenney.nl) and live in
    /// <c>Art/Kenney/&lt;Pack&gt;</c> with their licence files. Re-run after changing the choices here;
    /// clear the layout's skin fields to go back to greybox.
    /// </summary>
    public static class KenneyArtFactory
    {
        private const string KenneyFolder = "Assets/OrbitalCargo/Art/Kenney";
        private const string MaterialsFolder = "Assets/OrbitalCargo/Materials/Kenney";
        private const string StationAtlas = KenneyFolder + "/SpaceStationKit/Textures/colormap.png";

        [MenuItem("Tools/OrbitalCargo/Apply Kenney Art Skin")]
        public static void ApplyFromMenu()
        {
            Apply();
            SceneSetup.SetupGameScene();
        }

        public static void Apply()
        {
            CoreAssetFactory.CreateAll(); // sound keys (ScanHum...) must exist before their clips are swapped
            var layout = RoomBuilder.LoadOrCreateDefaultLayout();

            layout.FloorTile = Model("SpaceStationKit/floor-panel");
            layout.FloorTileScale = 2f;
            // The kit atlas is near-white; darker floor and walls keep the colour-coded bays and crates the
            // brightest things in view (the colour coding is the design).
            layout.FloorMaterial = TintedAtlas("M_KenneyFloor", new Color(0.36f, 0.39f, 0.45f));
            layout.WallPanel = Model("SpaceStationKit/wall");
            layout.WallWindowPanel = Model("SpaceStationKit/wall-window");
            layout.WallPanelScale = 4f;
            layout.WallMaterial = TintedAtlas("M_KenneyWall", new Color(0.62f, 0.66f, 0.74f));

            layout.ShuttleModel = Model("SpaceKit/craft_cargoA");
            layout.ShuttleScale = 1.25f;
            layout.ShuttleOffset = new Vector3(0f, 2.6f, 0f);
            layout.ShuttleYaw = 90f;

            layout.Props = new[]
            {
                Prop("SpaceStationKit/computer", 3.6f, 0f, -5.5f, 0f, 2f),
                Prop("SpaceStationKit/computer", 4.6f, 0f, -5.5f, 0f, 2f),
                Prop("SpaceStationKit/container-tall", 7.3f, 0f, 1.6f, 0f, 2f),
                Prop("SpaceStationKit/container", 7.3f, 0f, 0.3f, 15f, 2f),
                Prop("SpaceStationKit/container", 6.1f, 0f, 1.7f, -10f, 2f),
                Prop("SpaceStationKit/container-wide", 7.0f, 0f, 5.3f, 90f, 2f),
                Prop("SpaceStationKit/container", -5.2f, 0f, -5.3f, 5f, 2f),
                Prop("SpaceStationKit/container-tall", -6.4f, 0f, -5.3f, 0f, 2f),
                Prop("SpaceKit/machine_generator", -7.0f, 0f, 5.2f, 90f, 2.5f),
                Prop("SpaceKit/barrels", -7.2f, 0f, -0.8f, 30f, 2.5f),
                Prop("SpaceKit/satelliteDish", 7.3f, 1.8f, 1.6f, -90f, 1.5f),
                Prop("SpaceStationKit/display-wall", 7.85f, 1.4f, -1.2f, -90f, 2.5f),
            };
            EditorUtility.SetDirty(layout);

            ApplySounds();
            AssetDatabase.SaveAssets();
            Debug.Log("[KenneyArtFactory] Art skin and Sci-Fi sounds applied — run Setup Game Scene to rebuild the room.");
        }

        // ── Sounds ─────────────────────────────────────────────────────────────

        // Sci-Fi Sounds covers the physical sounds; UI clicks, coins and alarms keep their placeholders.
        private static void ApplySounds()
        {
            var service = AssetDatabase.LoadAssetAtPath<SoundService>(CoreAssetFactory.SoundsPath);
            if (service == null) return;
            Swap(service, "Pickup", "forceField_000");
            Swap(service, "Place", "impactMetal_002");
            Swap(service, "Throw", "forceField_003");
            Swap(service, "Launch", "spaceEngineLarge_000");
            Swap(service, "ScanHum", "computerNoise_000");
            EditorUtility.SetDirty(service);
        }

        private static void Swap(SoundService service, string soundName, string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{KenneyFolder}/SciFiSounds/{clipName}.ogg");
            if (clip == null)
            {
                Debug.LogWarning($"[KenneyArtFactory] Missing Sci-Fi Sounds clip {clipName}.ogg for '{soundName}'.");
                return;
            }
            foreach (var sound in service)
                if (sound != null && sound.SoundName == soundName) sound.AudioClip = clip;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameObject Model(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyFolder}/{path}.fbx");
            if (model == null) Debug.LogError($"[KenneyArtFactory] Missing Kenney model {path}.fbx.");
            return model;
        }

        private static RoomLayoutSO.PropPlacement Prop(string path, float x, float y, float z, float yaw, float scale) =>
            new RoomLayoutSO.PropPlacement { Prefab = Model(path), Position = new Vector3(x, y, z), Yaw = yaw, Scale = scale };

        private static Material TintedAtlas(string name, Color tint)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialsFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(StationAtlas));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
