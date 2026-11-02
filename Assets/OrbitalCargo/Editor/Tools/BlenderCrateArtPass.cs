using System.IO;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Crates.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Transfers the authored Blender atlas to URP and wires a standard-crate exterior.
    /// Keeps the existing casing collider, X-ray proxy, hidden anchor and other crate types.
    /// </summary>
    public static class BlenderCrateArtPass
    {
        public const string Folder = "Assets/OrbitalCargo/Art/BlenderBenchmark/URP";
        private const string Source = "ArtSource/CargoCrate";
        public const string ModelPath = Folder + "/CargoCrate_URP.fbx";
        public const string MaterialPath = Folder + "/CargoCrate_URP.mat";

        [MenuItem("Tools/OrbitalCargo/Art/Apply Blender Crate")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            foreach (string file in new[] { "CargoCrate_URP.fbx", "Crate_Atlas_Color.png", "Crate_Atlas_MetalSmooth.png", "Crate_Atlas_Emission.png" })
                File.Copy(Source + "/" + file, Folder + "/" + file, true);
            // Artist-calibrated navy palette for this project's Gamma-space dock lighting.
            if (File.Exists(Source + "/Crate_Atlas_Color_URP.png"))
                File.Copy(Source + "/Crate_Atlas_Color_URP.png", Folder + "/Crate_Atlas_Color.png", true);
            AssetDatabase.Refresh();
            ConfigureTexture("Crate_Atlas_Color", true);
            ConfigureTexture("Crate_Atlas_Emission", true);
            ConfigureTexture("Crate_Atlas_MetalSmooth", false);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.isReadable = true; // Existing highlight system reads normals/triangles once per mesh.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture("Crate_Atlas_Color"));
            material.SetTexture("_MetallicGlossMap", Texture("Crate_Atlas_MetalSmooth"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_EmissionMap", Texture("Crate_Atlas_Emission"));
            material.SetColor("_EmissionColor", Color.white * 0.65f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            var root = PrefabUtility.LoadPrefabContents(CrateAssetFactory.CratePrefabPath);
            try
            {
                ApplyToRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, CrateAssetFactory.CratePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[BlenderCrateArtPass] Standard exterior uses one URP material; scanner proxy and other types retained.");
        }

        /// <summary>Also called by crate factories so rebuilding cannot silently remove the authored variant.</summary>
        public static void ApplyToRoot(GameObject root)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (model == null || material == null) return;
            var casing = root.transform.Find("Casing");
            if (casing == null) return;
            var old = casing.Find("BlenderExterior");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var skin = Object.Instantiate(model, casing, false);
            skin.name = "BlenderExterior";
            var renderers = skin.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
                foreach (var mesh in renderer.GetComponents<MeshFilter>())
                {
                    var b = mesh.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var point = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        point = skin.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            float scale = 1f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            skin.transform.localScale = Vector3.one * scale;
            // Bounds are in the FBX root's space, which is rotated (Blender Z-up), so rotate the offset
            // into casing space. The casing collider is a unit cube on its origin; -0.5 is the crate's
            // floor, so the mesh sits on belts/slots instead of centring (and floating) inside the box.
            var center = skin.transform.localRotation * bounds.center * scale;
            float halfHeight = Mathf.Abs((skin.transform.localRotation * bounds.size).y) * scale * 0.5f;
            skin.transform.localPosition = new Vector3(-center.x, -0.5f + halfHeight - center.y, -center.z);
            foreach (var t in skin.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            var visuals = root.GetComponent<CrateVisuals>();
            var so = new SerializedObject(visuals);
            so.FindProperty("detailedType").objectReferenceValue = CrateAssetFactory.LoadType("CT_Standard");
            so.FindProperty("detailedModel").objectReferenceValue = skin;
            var legacy = casing.Find("DockCrateSkin");
            var details = legacy != null ? legacy.GetComponentsInChildren<Renderer>() : new Renderer[0];
            var array = so.FindProperty("legacyDetails");
            array.arraySize = details.Length;
            for (int i = 0; i < details.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = details[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            // Prefab preview matches the default standard type before Awake applies its runtime look.
            casing.GetComponent<Renderer>().enabled = false;
            var stripe = root.transform.Find("Stripe");
            if (stripe != null) stripe.GetComponent<Renderer>().enabled = false;
            foreach (var detail in details) detail.enabled = false;
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png");

        [MenuItem("Tools/OrbitalCargo/Art/Bake Dock Reflection")]
        public static void BakeDockReflection()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = GameObject.Find("Room/DockArt");
            if (root == null) return;
            var probe = EnsureDockReflection(root.transform);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            bool baked = Lightmapping.BakeReflectionProbe(probe, Folder + "/DockReflection.exr");
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(Folder + "/DockReflection.exr", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + "/DockReflection.exr");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
            Debug.Log("[BlenderCrateArtPass] Dock reflection baked: " + baked);
        }

        public static ReflectionProbe EnsureDockReflection(Transform root)
        {
            var child = root.Find("DockReflection");
            if (child == null)
            {
                child = new GameObject("DockReflection", typeof(ReflectionProbe)).transform;
                child.SetParent(root, false);
            }
            child.localPosition = new Vector3(-3f, 1.8f, 0f);
            var probe = child.GetComponent<ReflectionProbe>();
            // Explicit custom binding survives Play/domain reload without a LightingDataAsset.
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.size = new Vector3(20f, 6f, 16f);
            probe.center = new Vector3(3f, 0f, 0f);
            probe.resolution = 128;
            probe.boxProjection = true;
            probe.hdr = true;
            probe.intensity = 0.7f;
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 30f;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + "/DockReflection.exr");
            return probe;
        }

        private static void ConfigureTexture(string name, bool srgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".png");
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
