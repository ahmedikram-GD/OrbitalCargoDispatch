using System.Collections.Generic;
using System.IO;
using OrbitalCargo.Config;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// A reversible visual skin for the first playable dock section. Retains station transforms,
    /// colliders, cargo slots and event wiring; repeated geometry is combined by material per section.
    /// Generated meshes and materials are real project assets, shared by every rebuilt scene.
    /// </summary>
    public static class DockArtPass
    {
        public const string ThemePath = "Assets/OrbitalCargo/ScriptableObjects/Room/DockArtDirection.asset";
        private const string ArtFolder = "Assets/OrbitalCargo/Art/Dock";
        private const string RootName = "DockArt";
        private static Mesh _box;
        private static DockArtDirectionSO _theme;

        [MenuItem("Tools/OrbitalCargo/Art/Apply First Dock Pass")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[DockArtPass] Leave Play mode before applying the art pass.");
                return;
            }
            var room = GameObject.Find(RoomBuilder.RootName);
            var layout = AssetDatabase.LoadAssetAtPath<RoomLayoutSO>(RoomBuilder.DefaultLayoutPath);
            if (room == null || layout == null) return;
            var theme = AssetDatabase.LoadAssetAtPath<DockArtDirectionSO>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<DockArtDirectionSO>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            Undo.RecordObject(layout, "Enable dock art");
            layout.ArtDirection = theme;
            EditorUtility.SetDirty(layout);
            ApplyRoom(layout, room.transform);
            var prefab = PrefabUtility.LoadPrefabContents(Crates.EditorTools.CrateAssetFactory.CratePrefabPath);
            try
            {
                StyleCrate(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, Crates.EditorTools.CrateAssetFactory.CratePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var standard = Crates.EditorTools.CrateAssetFactory.LoadType("CT_Standard");
            if (standard != null)
            {
                standard.AccentColor = theme.Cyan;
                EditorUtility.SetDirty(standard);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            EditorSceneManager.SaveScene(room.gameObject.scene);
            Debug.Log("[DockArtPass] First dock skin applied. Gameplay wiring and colliders retained.");
        }

        /// <summary>Called by the existing room builder when its layout has an enabled dock theme.</summary>
        public static void ApplyRoom(RoomLayoutSO layout, Transform room)
        {
            _theme = layout.ArtDirection;
            if (_theme == null || !_theme.Enabled) return;
            Directory.CreateDirectory(ArtFolder + "/Meshes");
            Directory.CreateDirectory(ArtFolder + "/Materials");
            _box = BevelMesh();
            var old = room.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(room, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Apply first dock art");
            Hide(room.Find("Skin/Floor"));
            Hide(room.Find("Skin/Walls"));
            Hide(room.Find("Shell"));
            Shell(layout, root);
            Conveyor(layout, room, root);
            HeroDockArt.Apply(room, root);
            Lighting(root);
            DockEnvironmentArt.Apply(layout, root);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BlenderCrateArtPass.ModelPath) != null)
                BlenderCrateArtPass.EnsureDockReflection(root);
            // Later bays retain their original shapes and colour coding during this first section pass.
            foreach (string id in new[] { "B", "C" })
            {
                var board = room.Find("Bay_" + id + "/Backboard");
                if (board != null) board.GetComponent<Renderer>().sharedMaterial = Mat("Wall", _theme.Wall);
            }
        }

        private static void Shell(RoomLayoutSO layout, Transform root)
        {
            var shell = Group("Shell", root);
            float w = layout.RoomSize.x, d = layout.RoomSize.y, h = layout.WallHeight;
            var dark = Mat("Frame", _theme.Frame);
            var wall = Mat("Wall", _theme.Wall);
            var panel = Mat("Panel", _theme.Panel);
            var floor = Mat("Floor", _theme.Floor);
            var amber = Mat("Amber", _theme.Amber);
            var glow = Mat("WarmLight", _theme.Amber, _theme.Emission);
            var intake = Group("Intake", shell);
            intake.localPosition = new Vector3(-w / 2f, 0f, layout.ConveyorWaypoints[0].z);
            intake.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box("IntakeDark", intake, new Vector3(0f, 1.15f, -0.5f), new Vector3(layout.AirlockWidth, 2.3f, 0.1f), dark);
            foreach (float x in new[] { -layout.AirlockWidth / 2f, layout.AirlockWidth / 2f })
            {
                Box("IntakePost", intake, new Vector3(x, 1.3f, 0f), new Vector3(0.22f, 2.6f, 0.35f), dark);
                Box("IntakeLamp", intake, new Vector3(x, 1.8f, 0.19f), new Vector3(0.06f, 0.7f, 0.02f), glow);
            }
            Box("IntakeHeader", intake, new Vector3(0f, 2.6f, 0f), new Vector3(layout.AirlockWidth + 0.3f, 0.45f, 0.35f), dark);
            Label("IntakeLabel", intake, "INTAKE", new Vector3(0f, 2.6f, 0.2f), 0.20f, 1.5f);
            Box("FloorBase", shell, new Vector3(0f, -0.045f, 0f), new Vector3(w, 0.08f, d), dark);
            for (float x = -w * 0.5f + 1f; x < w * 0.5f; x += 2f)
            for (float z = -d * 0.5f + 1f; z < d * 0.5f; z += 2f)
                Box("FloorPanel", shell, new Vector3(x, -0.012f, z), new Vector3(1.975f, 0.025f, 1.975f), floor);

            for (int side = 0; side < 4; side++)
            {
                float length = side < 2 ? w : d;
                var run = Group("WallRun" + side, shell);
                run.localPosition = side == 0 ? new Vector3(0f, 0f, d / 2f) : side == 1 ? new Vector3(0f, 0f, -d / 2f) : side == 2 ? new Vector3(-w / 2f, 0f, 0f) : new Vector3(w / 2f, 0f, 0f);
                run.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : side == 1 ? 180f : side == 2 ? -90f : 90f, 0f);
                for (float x = -length / 2f + 1f; x < length / 2f; x += 2f)
                {
                    // Preserve the conveyor's original side-wall opening.
                    bool intakeOpening = side == 2 && Mathf.Abs(x - layout.ConveyorWaypoints[0].z) < 1.8f;
                    if (!intakeOpening)
                    {
                        Box("Wall", run, new Vector3(x, h / 2f, 0.04f), new Vector3(1.97f, h, 0.18f), wall);
                        Box("Inset", run, new Vector3(x, 2.15f, -0.075f), new Vector3(1.67f, 2.45f, 0.07f), panel);
                        Box("LowerPanel", run, new Vector3(x, 0.45f, -0.10f), new Vector3(1.72f, 0.55f, 0.10f), dark);
                    }
                    Box("Pillar", run, new Vector3(x - 0.95f, h / 2f, -0.1f), new Vector3(0.14f, h, 0.24f), dark);
                    Box("LightHousing", run, new Vector3(x - 0.95f, 2.8f, -0.25f), new Vector3(0.20f, 0.55f, 0.12f), dark);
                    Box("Lamp", run, new Vector3(x - 0.95f, 2.8f, -0.32f), new Vector3(0.08f, 0.36f, 0.025f), glow);
                }
                Box("TopBeam", run, new Vector3(0f, h - 0.15f, -0.15f), new Vector3(length, 0.25f, 0.35f), dark);
            }
            // Keep launch shafts open over the original vertical shuttle trajectories.
            Box("Ceiling", shell, new Vector3(0f, h + 0.08f, -1.6f), new Vector3(w, 0.16f, d - 3.2f), wall);
            for (float z = -4.5f; z <= 1.5f; z += 3f)
            {
                Box("CeilingBeam", shell, new Vector3(0f, h - 0.12f, z), new Vector3(w, 0.22f, 0.20f), dark);
                foreach (float x in new[] { -4.5f, 0f, 4.5f })
                {
                    Box("CeilingFixture", shell, new Vector3(x, h - 0.26f, z), new Vector3(1.6f, 0.15f, 0.50f), dark);
                    Box("CeilingDiffuser", shell, new Vector3(x, h - 0.345f, z), new Vector3(1.35f, 0.03f, 0.32f), glow);
                }
            }
            for (float x = -2.2f; x <= 2.2f; x += 4.4f)
                Box("ShaftBeam", shell, new Vector3(x, h - 0.1f, 4.3f), new Vector3(0.25f, 0.25f, 3.4f), dark);
            Box("LaunchShaftRoof", shell, new Vector3(0f, h + 4f, 4.4f), new Vector3(w, 0.2f, 3.5f), dark);
            Box("LaunchShaftRear", shell, new Vector3(0f, h + 2f, d / 2f), new Vector3(w, 4f, 0.2f), dark);
            Box("LaunchShaftFront", shell, new Vector3(0f, h + 2f, 2.7f), new Vector3(w, 4f, 0.2f), dark);
            foreach (float x in new[] { -w / 2f, w / 2f })
                Box("LaunchShaftSide", shell, new Vector3(x, h + 2f, 4.4f), new Vector3(0.2f, 4f, 3.5f), dark);
            // Painted approach lanes are mesh strips: no decal renderer or extra package needed.
            foreach (float x in new[] { -6.35f, -2.65f })
                Box("BayLane", shell, new Vector3(x, 0.006f, 1.5f), new Vector3(0.055f, 0.006f, 4.5f), amber);
            Box("StopLine", shell, new Vector3(-4.5f, 0.007f, 2.3f), new Vector3(3.7f, 0.006f, 0.10f), amber);
            Combine(shell, "Shell");
            // The key acts as broad interior fill. Props cast contact shadows; the closed
            // ceiling must not block this light, since the practical fixtures are unshadowed.
            foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Conveyor(RoomLayoutSO layout, Transform room, Transform root)
        {
            var art = Group("Conveyor", root);
            var metal = Mat("Metal", _theme.Metal);
            var dark = Mat("Frame", _theme.Frame);
            var amber = Mat("Amber", _theme.Amber);
            var glow = Mat("WarmLight", _theme.Amber, _theme.Emission);
            for (int i = 0; i < layout.ConveyorWaypoints.Length - 1; i++)
            {
                Vector3 a = layout.ConveyorWaypoints[i], b = layout.ConveyorWaypoints[i + 1];
                var segment = Group("Segment" + i, art);
                segment.position = (a + b) / 2f;
                segment.rotation = Quaternion.LookRotation((b - a).normalized);
                float length = Vector3.Distance(a, b), half = layout.ConveyorWidth * 0.5f;
                for (float z = -length / 2f; z < length / 2f; z += 0.22f)
                    Box("BeltSlat", segment, new Vector3(0f, 0.006f, z), new Vector3(layout.ConveyorWidth - 0.12f, 0.025f, 0.19f), metal);
                foreach (float x in new[] { -half, half })
                {
                    Box("BeltFrame", segment, new Vector3(x, -0.13f, 0f), new Vector3(0.09f, 0.24f, length), dark);
                    Box("SafetyTrim", segment, new Vector3(x, -0.025f, 0f), new Vector3(0.065f, 0.055f, length), amber);
                    for (float z = -length / 2f + 0.5f; z < length / 2f; z += 1.5f)
                    {
                        Box("Leg", segment, new Vector3(x, -a.y / 2f, z), new Vector3(0.12f, a.y, 0.16f), dark);
                        Box("StatusLamp", segment, new Vector3(x * 1.08f, -0.15f, z), new Vector3(0.025f, 0.07f, 0.18f), glow);
                    }
                }
            }
            // Belt collider remains the original kinematic gameplay surface.
            var belt = room.Find("Conveyor");
            if (belt != null) foreach (var r in belt.GetComponentsInChildren<Renderer>()) r.sharedMaterial = dark;
            Combine(art, "Conveyor");
        }

        /// <summary>Repositions the live counter after station wiring creates it on a rebuilt room.</summary>
        public static void FinishStationStyling(RoomLayoutSO layout, Transform room)
        {
            if (layout == null || layout.ArtDirection == null || !layout.ArtDirection.Enabled) return;
            var counter = room.Find("Bay_A/FillCounter");
            if (counter == null) return;
            counter.localPosition = new Vector3(0f, 1.85f, -1.30f);
            var label = counter.GetComponent<TextMeshPro>();
            if (label != null) label.fontSize = 2.5f;
        }

        private static void Lighting(Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.51f, 0.57f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.36f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.19f, 0.24f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            foreach (var sceneRoot in root.gameObject.scene.GetRootGameObjects())
            foreach (var camera in sceneRoot.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.025f, 0.05f);
            }
            DynamicGI.UpdateEnvironment();
            foreach (var sceneRoot in root.gameObject.scene.GetRootGameObjects())
            foreach (var light in sceneRoot.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Interior dock light");
                light.intensity = _theme.KeyIntensity;
                light.color = new Color(1f, 0.86f, 0.68f);
                light.shadows = _theme.ContactShadows ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = _theme.ShadowStrength;
                light.shadowBias = 0.08f;
                light.shadowNormalBias = 0.25f;
                light.transform.rotation = Quaternion.Euler(65f, -25f, 0f);
            }
            foreach (Vector3 pos in new[] { new Vector3(-4.5f, 3.6f, 0f), new Vector3(3f, 3.6f, -2.5f), new Vector3(-4.5f, 3.6f, 4f) })
            {
                var go = new GameObject("WarmFixtureLight");
                go.transform.SetParent(root, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.49f);
                light.intensity = _theme.FixtureIntensity;
                light.range = 10f;
                light.spotAngle = 120f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Decorates the existing collider-bearing casing, retaining scanner and pooling references.</summary>
        public static void StyleCrate(GameObject root)
        {
            _theme = AssetDatabase.LoadAssetAtPath<DockArtDirectionSO>(ThemePath);
            if (_theme == null || !_theme.Enabled) return;
            _box = BevelMesh();
            var casing = root.transform.Find("Casing");
            if (casing == null) return;
            casing.GetComponent<MeshFilter>().sharedMesh = _box;
            var old = casing.Find("DockCrateSkin");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var art = Group("DockCrateSkin", casing);
            var trim = Mat("CargoTrim", _theme.Frame);
            var labelMat = Mat("CargoLabel", _theme.Label);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Box("Corner", art, new Vector3(x * 0.43f, y * 0.43f, z * 0.43f), Vector3.one * 0.16f, trim);
            foreach (float x in new[] { -0.37f, 0.37f })
                Box("LidBrace", art, new Vector3(x, 0.49f, 0f), new Vector3(0.09f, 0.06f, 0.92f), trim);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("LabelPlate", art, new Vector3(0f, -0.10f, side * 0.503f), new Vector3(0.38f, 0.22f, 0.015f), labelMat);
                for (int i = 0; i < 9; i++)
                    Box("Barcode", art, new Vector3(-0.14f + i * 0.033f, -0.1f, side * 0.514f), new Vector3(i % 3 == 0 ? 0.022f : 0.01f, 0.13f, 0.006f), trim);
            }
            Combine(art, "Cargo");
            foreach (var t in art.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            BlenderCrateArtPass.ApplyToRoot(root);
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            if (_box == null) _box = BevelMesh();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = _box;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;
        }

        private static Material Mat(string name, Color color, float emission = 0f)
        {
            string path = ArtFolder + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetFloat("_Metallic", 0.08f);
            if (emission > 0f)
            {
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                mat.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static void Label(string name, Transform parent, string text, Vector3 position, float height, float width)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var label = go.GetComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = height * 10f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = _theme.Label;
            label.rectTransform.sizeDelta = new Vector2(width, height * 1.5f);
        }

        private static void Hide(Transform target)
        {
            if (target == null) return;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                Undo.RecordObject(r, "Hide original visual");
                r.enabled = false;
            }
        }

        internal static void Combine(Transform root, string prefix)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var parts = root.GetComponentsInChildren<MeshFilter>();
            foreach (var part in parts)
            {
                if (part.GetComponent<TMP_Text>() != null) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer == null || part.sharedMesh == null) continue;
                Material mat = renderer.sharedMaterial;
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = part.sharedMesh, transform = root.worldToLocalMatrix * part.transform.localToWorldMatrix });
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = prefix + "_" + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                mesh = SaveMesh(mesh, ArtFolder + "/Meshes/" + mesh.name + ".asset");
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
                go.isStatic = true;
            }
            // Generated primitive children contain no gameplay components or colliders.
            foreach (var part in parts)
                if (part.GetComponent<TMP_Text>() == null) Object.DestroyImmediate(part.gameObject);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            // CopySerialized preserves the GUID but can leave the editor's GPU buffer stale.
            // Reassign vertices to invalidate that buffer before capturing or entering Play.
            existing.vertices = mesh.vertices;
            existing.UploadMeshData(false);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh BevelMesh()
        {
            string path = ArtFolder + "/Meshes/BevelBox.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            const float a = 0.43f;
            for (int axis = 0; axis < 3; axis++)
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 n = axes[axis] * sign, u = axes[(axis + 1) % 3] * a, v = axes[(axis + 2) % 3] * a;
                Face(vertices, triangles, uvs, n, new[] { n * 0.5f - u - v, n * 0.5f + u - v, n * 0.5f + u + v, n * 0.5f - u + v });
            }
            for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
            foreach (float si in new[] { -1f, 1f })
            foreach (float sj in new[] { -1f, 1f })
            {
                Vector3 u = axes[i] * si, v = axes[j] * sj, along = axes[3 - i - j] * a;
                Face(vertices, triangles, uvs, u + v, new[] { u * 0.5f + v * a - along, u * a + v * 0.5f - along, u * a + v * 0.5f + along, u * 0.5f + v * a + along });
            }
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Face(vertices, triangles, uvs, new Vector3(x, y, z), new[] { new Vector3(x * 0.5f, y * a, z * a), new Vector3(x * a, y * 0.5f, z * a), new Vector3(x * a, y * a, z * 0.5f) });
            var mesh = new Mesh { name = "BevelBox" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void Face(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 normal, Vector3[] polygon)
        {
            if (Vector3.Dot(Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]), normal) < 0f) System.Array.Reverse(polygon);
            int start = vertices.Count;
            foreach (var point in polygon) { vertices.Add(point); uvs.Add(new Vector2(point.x + 0.5f, point.z + 0.5f)); }
            for (int i = 1; i < polygon.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }
    }
}
