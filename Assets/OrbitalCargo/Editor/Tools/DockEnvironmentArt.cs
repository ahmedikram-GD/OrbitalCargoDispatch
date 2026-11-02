using System.IO;
using OrbitalCargo.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Imports the Blender modular kit and assembles a six-metre benchmark corner beside Bay A.
    /// Geometry and illumination are scene art; gameplay colliders and station references remain owned by RoomBuilder.
    /// </summary>
    public static class DockEnvironmentArt
    {
        public const string Folder = "Assets/OrbitalCargo/Art/EnvironmentKit";
        private const string Source = "ArtSource/DockKit";
        private const string MaterialPath = Folder + "/DockKit_URP.mat";
        private const string LightingPath = Folder + "/CornerLighting.lighting";
        private static readonly string[] Models = { "Dock_Floor_2m", "Dock_Wall_2m", "Dock_Light_160cm" };

        [MenuItem("Tools/OrbitalCargo/Art/Import Environment Corner")]
        public static void ImportAndApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            foreach (string model in Models) File.Copy(Source + "/" + model + ".fbx", Folder + "/" + model + ".fbx", true);
            foreach (string texture in new[] { "DockKit_Color", "DockKit_MetalSmooth", "DockKit_Emission", "DockKit_AO" })
                File.Copy(Source + "/" + texture + ".png", Folder + "/" + texture + ".png", true);
            if (File.Exists(Source + "/DockKit_Color_URP.png"))
                File.Copy(Source + "/DockKit_Color_URP.png", Folder + "/DockKit_Color.png", true);
            AssetDatabase.Refresh();
            foreach (string model in Models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/" + model + ".fbx");
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
            }
            foreach (string name in new[] { "DockKit_Color", "DockKit_MetalSmooth", "DockKit_Emission", "DockKit_AO" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".png");
                importer.sRGBTexture = name == "DockKit_Color" || name == "DockKit_Emission";
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture("DockKit_Color"));
            material.SetTexture("_MetallicGlossMap", Texture("DockKit_MetalSmooth"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", Texture("DockKit_AO"));
            material.SetFloat("_OcclusionStrength", 0.7f);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetTexture("_EmissionMap", Texture("DockKit_Emission"));
            material.SetColor("_EmissionColor", new Color(1f, 0.70f, 0.25f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            Directory.CreateDirectory(Folder + "/Prefabs");
            foreach (string name in Models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
                var instance = Object.Instantiate(model);
                instance.name = name;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                    renderer.receiveGI = ReceiveGI.Lightmaps;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/Prefabs/" + name + ".prefab");
                Object.DestroyImmediate(instance);
            }
            var room = GameObject.Find("Room");
            var root = room != null ? room.transform.Find("DockArt") : null;
            var layout = AssetDatabase.LoadAssetAtPath<RoomLayoutSO>(RoomBuilder.DefaultLayoutPath);
            if (root != null)
            {
                Apply(layout, root);
                EditorSceneManager.MarkSceneDirty(room.scene);
                EditorSceneManager.SaveScene(room.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[DockEnvironmentArt] Authored floor, wall and ceiling-light modules imported and assembled.");
        }

        public static void Apply(RoomLayoutSO layout, Transform dockRoot)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (layout == null || layout.ArtDirection == null || !layout.ArtDirection.UseEnvironmentKit || material == null) return;
            var old = dockRoot.Find("EnvironmentCorner");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("EnvironmentCorner").transform;
            root.SetParent(dockRoot, false);
            TrimReplacedWalls(dockRoot.Find("Shell"));
            for (float x = -layout.RoomSize.x / 2f + 1f; x < layout.RoomSize.x / 2f; x += 2f)
            for (float z = -layout.RoomSize.y / 2f + 1f; z < layout.RoomSize.y / 2f; z += 2f)
                Place(Models[0], root, new Vector3(x, 0.012f, z), 0f, material);
            for (int z = 0; z < 3; z++)
                Place(Models[1], root, new Vector3(-7.98f, 0f, 1f + z * 2f), 90f, material);
            // Rear module is behind Bay A, while the left wall supplies the visible benchmark surface.
            foreach (float x in new[] { -7f, -5f, -3f })
                Place(Models[1], root, new Vector3(x, 0f, 5.98f), 180f, material);
            foreach (float x in new[] { -6f, -4f })
            {
                Place(Models[2], root, new Vector3(x, 3.98f, 0f), 0f, material);
                var lamp = new GameObject("CornerBakedArea", typeof(Light));
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = new Vector3(x, 3.62f, 0f);
                lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = lamp.GetComponent<Light>();
                light.type = LightType.Rectangle;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = new Vector2(1.29f, 0.28f);
                light.intensity = layout.ArtDirection.CornerBakedLightIntensity;
                light.color = new Color(1f, 0.79f, 0.55f);
                light.shadows = LightShadows.Soft;
                var fill = new GameObject("CornerDynamicFill", typeof(Light));
                fill.transform.SetParent(root, false);
                fill.transform.localPosition = lamp.transform.localPosition;
                var point = fill.GetComponent<Light>();
                point.type = LightType.Point;
                point.lightmapBakeType = LightmapBakeType.Realtime;
                point.range = 7f;
                point.intensity = layout.ArtDirection.CornerRealtimeFill;
                point.color = light.color;
                point.shadows = LightShadows.None;
            }
            foreach (float z in new[] { 1f, 3f, 5f })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Models[2] + ".fbx");
                var fixture = Object.Instantiate(model, root, false);
                fixture.name = "WallPractical";
                fixture.transform.localPosition = new Vector3(-7.72f, 2.8f, z);
                fixture.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * model.transform.localRotation;
                fixture.transform.localScale *= 0.38f;
                foreach (var renderer in fixture.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                    renderer.receiveGI = ReceiveGI.Lightmaps;
                }
            }
            // Repaint approach guides over the raised deck surface.
            var paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/OrbitalCargo/Art/Dock/Materials/Amber.mat");
            foreach (float x in new[] { -6.35f, -2.65f })
                DockArtPass.Box("ApproachLane", root, new Vector3(x, 0.022f, 1.5f), new Vector3(0.09f, 0.006f, 4.5f), paint);
            DockArtPass.Box("StopLine", root, new Vector3(-4.5f, 0.023f, 2.3f), new Vector3(3.7f, 0.006f, 0.1f), paint);
            DockArtPass.Box("RoutingStripe", root, new Vector3(0f, 0.022f, -1.8f), new Vector3(0.14f, 0.006f, 3.8f), paint);
            DockArtPass.Box("BayABranch", root, new Vector3(-2.2f, 0.022f, 0f), new Vector3(4.4f, 0.006f, 0.14f), paint);
            var arrow = new GameObject("DispatchArrow", typeof(MeshFilter), typeof(MeshRenderer));
            arrow.transform.SetParent(root, false);
            var mesh = new Mesh { name = "DispatchArrow" };
            mesh.vertices = new[] { new Vector3(-0.32f,0.027f,-0.25f),new Vector3(0f,0.027f,0.25f),new Vector3(0.32f,0.027f,-0.25f) };
            mesh.triangles = new[] { 0,1,2 };
            mesh.RecalculateNormals();
            string arrowPath = Folder + "/DispatchArrow.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(arrowPath);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, arrowPath); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            arrow.GetComponent<MeshFilter>().sharedMesh = saved;
            arrow.GetComponent<MeshRenderer>().sharedMaterial = paint;
            var probes = new GameObject("CornerLightProbes", typeof(LightProbeGroup));
            probes.transform.SetParent(root, false);
            var positions = new System.Collections.Generic.List<Vector3>();
            foreach (float x in new[] { -7f, -5f, -3f })
            foreach (float y in new[] { 0.5f, 1.5f, 2.8f })
            foreach (float z in new[] { 0f, 2f, 4f }) positions.Add(new Vector3(x, y, z));
            probes.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
            foreach (var sceneRoot in root.gameObject.scene.GetRootGameObjects())
            foreach (var camera in sceneRoot.GetComponentsInChildren<Camera>(true))
            {
                var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (data != null)
                {
                    Undo.RecordObject(data, "Environment camera anti-aliasing");
                    data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                    EditorUtility.SetDirty(data);
                }
            }
        }

        [MenuItem("Tools/OrbitalCargo/Art/Bake Environment Corner")]
        public static void BakeCorner()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) return;
            var room = GameObject.Find("Room");
            var corner = room != null ? room.transform.Find("DockArt/EnvironmentCorner") : null;
            if (corner == null) return;
            // Only the authored meshes have lightmap UVs in this benchmark.
            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                bool authored = renderer.transform.IsChildOf(corner) && renderer.GetComponent<MeshFilter>() != null
                    && AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>().sharedMesh).StartsWith(Folder)
                    && AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>().sharedMesh).EndsWith(".fbx");
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, authored ? flags | StaticEditorFlags.ContributeGI : flags & ~StaticEditorFlags.ContributeGI);
                if (authored) renderer.receiveGI = ReceiveGI.Lightmaps;
            }
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution = 20f;
            settings.lightmapMaxSize = 1024;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 64;
            settings.environmentSampleCount = 32;
            settings.maxBounces = 2;
            settings.ao = true;
            settings.aoMaxDistance = 0.35f;
            Lightmapping.lightingSettings = settings;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(room.scene);
            bool started = Lightmapping.BakeAsync();
            Debug.Log("[DockEnvironmentArt] Corner lighting bake started: " + started);
        }

        private static void Place(string name, Transform parent, Vector3 position, float yaw, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + name + ".prefab");
            if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) return;
            var go = Object.Instantiate(model, parent, false);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                renderer.receiveGI = ReceiveGI.Lightmaps;
            }
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png");

        private static void TrimReplacedWalls(Transform shell)
        {
            if (shell == null) return;
            foreach (var filter in shell.GetComponentsInChildren<MeshFilter>())
            {
                // Work from the original combined mesh each time; the source stays intact.
                string originalPath = "Assets/OrbitalCargo/Art/Dock/Meshes/" + filter.name + ".asset";
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(originalPath);
                if (source == null || !source.isReadable) continue;
                var vertices = source.vertices;
                var triangles = source.triangles;
                var kept = new System.Collections.Generic.List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 p = filter.transform.TransformPoint((vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]]) / 3f);
                    bool left = p.x < -7.6f && p.z > 0f && p.z < 6.1f;
                    bool rear = p.z > 5.6f && p.x > -8.1f && p.x < -2f;
                    if (p.y > 0.10f && p.y < 3.95f && (left || rear)) continue;
                    kept.Add(triangles[i]); kept.Add(triangles[i+1]); kept.Add(triangles[i+2]);
                }
                if (kept.Count == triangles.Length) continue;
                var mesh = Object.Instantiate(source);
                mesh.name = filter.name + "_CornerTrim";
                mesh.triangles = kept.ToArray();
                mesh.RecalculateBounds();
                string path = Folder + "/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
                else { EditorUtility.CopySerialized(mesh, existing); existing.vertices = mesh.vertices; existing.UploadMeshData(false); Object.DestroyImmediate(mesh); }
                filter.sharedMesh = existing;
            }
        }
    }
}
