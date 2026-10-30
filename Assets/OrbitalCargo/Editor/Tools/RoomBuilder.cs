using System.IO;
using OrbitalCargo.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Builds the greybox station room in the open scene from a <see cref="RoomLayoutSO"/>.
    /// Destroys and rebuilds the <c>Room</c> root every time, so the layout asset is the single
    /// source of truth. Every object is named by role (<c>Bay_A</c>, <c>Slot_A_0</c>, <c>Conveyor</c>,
    /// <c>ScannerBooth/CratePoint</c>...) so later slices attach gameplay components by name, and the
    /// art pass can swap primitives for kit pieces without touching gameplay.
    /// </summary>
    public static class RoomBuilder
    {
        public const string DefaultLayoutPath = "Assets/OrbitalCargo/ScriptableObjects/Room/RoomLayout_Default.asset";
        public const string RootName = "Room";

        // ── Menu ───────────────────────────────────────────────────────────────

        [MenuItem("Tools/OrbitalCargo/Build Greybox Room")]
        public static void BuildFromMenu()
        {
            var layout = LoadOrCreateDefaultLayout();
            Build(layout);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Loads the default layout asset, creating it with the class defaults if missing.</summary>
        public static RoomLayoutSO LoadOrCreateDefaultLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<RoomLayoutSO>(DefaultLayoutPath);
            if (layout != null) return layout;

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultLayoutPath));
            layout = ScriptableObject.CreateInstance<RoomLayoutSO>();
            AssetDatabase.CreateAsset(layout, DefaultLayoutPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomBuilder] Created default layout at {DefaultLayoutPath}.");
            return layout;
        }

        // ── Build ──────────────────────────────────────────────────────────────

        /// <summary>Rebuilds the <c>Room</c> root in the active scene. Returns the new root.</summary>
        public static GameObject Build(RoomLayoutSO layout)
        {
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Greybox Room");

            BuildShell(layout, root.transform);
            BuildConveyor(layout, root.transform);
            foreach (var bay in layout.Bays) BuildBay(layout, bay, root.transform);
            BuildBooth(layout, root.transform);
            BuildChute(layout, root.transform);
            BuildTerminal(layout, root.transform);

            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetParent(root.transform, false);
            spawn.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));

            AssetDatabase.SaveAssets();
            return root;
        }

        private static void BuildShell(RoomLayoutSO layout, Transform root)
        {
            var shell = Group("Shell", root);
            float w = layout.RoomSize.x, d = layout.RoomSize.y, h = layout.WallHeight, t = layout.WallThickness;
            var wallMat = GreyboxMaterials.Get("Wall", new Color(0.32f, 0.36f, 0.42f));

            Box("Floor", shell, new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), GreyboxMaterials.Get("Floor", new Color(0.18f, 0.19f, 0.22f)));
            Box("Wall_Far", shell, new Vector3(0f, h * 0.5f, d * 0.5f + t * 0.5f), new Vector3(w + 2f * t, h, t), wallMat);
            Box("Wall_Near", shell, new Vector3(0f, h * 0.5f, -d * 0.5f - t * 0.5f), new Vector3(w + 2f * t, h, t), wallMat);
            Box("Wall_Right", shell, new Vector3(w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wallMat);

            // Left wall is split around the airlock where the conveyor enters.
            float gapZ = layout.ConveyorWaypoints.Length > 0 ? layout.ConveyorWaypoints[0].z : 0f;
            float gapHalf = layout.AirlockWidth * 0.5f;
            float x = -w * 0.5f - t * 0.5f;
            float backLen = d * 0.5f - (gapZ + gapHalf);
            float frontLen = (gapZ - gapHalf) + d * 0.5f;
            if (backLen > 0f) Box("Wall_Left_Back", shell, new Vector3(x, h * 0.5f, gapZ + gapHalf + backLen * 0.5f), new Vector3(t, h, backLen), wallMat);
            if (frontLen > 0f) Box("Wall_Left_Front", shell, new Vector3(x, h * 0.5f, -d * 0.5f + frontLen * 0.5f), new Vector3(t, h, frontLen), wallMat);
            Box("Wall_Left_Lintel", shell, new Vector3(x, (h + 2.2f) * 0.5f, gapZ), new Vector3(t, h - 2.2f, layout.AirlockWidth), wallMat);
            Box("Airlock_Dark", shell, new Vector3(x - t, 1.1f, gapZ), new Vector3(0.05f, 2.2f, layout.AirlockWidth), GreyboxMaterials.Get("Airlock", new Color(0.05f, 0.05f, 0.07f)), collider: false);

            // The planet glowing through the bay window — pure mood, no collider.
            Box("PlanetWindow", shell, new Vector3(0f, h * 0.7f, d * 0.5f - 0.01f), new Vector3(w * 0.6f, h * 0.3f, 0.02f),
                GreyboxMaterials.Get("PlanetWindow", new Color(1f, 0.55f, 0.25f), emission: 1.5f), collider: false);
        }

        private static void BuildConveyor(RoomLayoutSO layout, Transform root)
        {
            var conveyor = Group("Conveyor", root);
            var beltMat = GreyboxMaterials.Get("Conveyor", new Color(0.08f, 0.08f, 0.09f));
            var points = layout.ConveyorWaypoints;

            for (int i = 0; i < points.Length; i++)
            {
                var wp = new GameObject($"Waypoint_{i}").transform;
                wp.SetParent(conveyor, false);
                wp.position = points[i];
            }

            // One belt segment per leg; the top face sits at the waypoint height.
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float len = flat.magnitude + layout.ConveyorWidth;
                float top = a.y;
                var seg = Box($"Belt_{i}", conveyor, new Vector3((a.x + b.x) * 0.5f, top * 0.5f, (a.z + b.z) * 0.5f),
                    new Vector3(layout.ConveyorWidth, top, len), beltMat);
                if (flat.sqrMagnitude > 1e-4f) seg.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        private static void BuildBay(RoomLayoutSO layout, RoomLayoutSO.BayAnchor anchor, Transform root)
        {
            var bay = Group($"Bay_{anchor.Id}", root);
            bay.SetPositionAndRotation(anchor.Position, Quaternion.Euler(0f, anchor.Yaw, 0f));

            float padW = layout.SlotColumns * layout.SlotSpacing + 0.3f;
            float padD = layout.SlotRows * layout.SlotSpacing + 0.3f;
            var padMat = GreyboxMaterials.Get($"Bay{anchor.Id}", anchor.Color);
            Box("Pad", bay, new Vector3(0f, layout.PadHeight * 0.5f, 0f), new Vector3(padW, layout.PadHeight, padD), padMat, local: true);

            var slots = Group("Slots", bay, local: true);
            var markerMat = GreyboxMaterials.Get("SlotMarker", new Color(0.9f, 0.9f, 0.9f));
            var locals = layout.GetSlotLocalPositions();
            for (int i = 0; i < locals.Length; i++)
            {
                var slot = new GameObject($"Slot_{anchor.Id}_{i}").transform;
                slot.SetParent(slots, false);
                slot.localPosition = locals[i];
                Box("Marker", slot, new Vector3(0f, 0.005f, 0f), new Vector3(0.85f, 0.01f, 0.85f), markerMat, local: true, collider: false);
            }

            // Tall coloured backboard so the bay reads from across the room.
            Box("Backboard", bay, new Vector3(0f, 1.5f, padD * 0.5f + 0.2f), new Vector3(padW, 3f, 0.2f), padMat, local: true);
        }

        private static void BuildBooth(RoomLayoutSO layout, Transform root)
        {
            var booth = Group("ScannerBooth", root);
            booth.position = layout.BoothPosition;
            var frameMat = GreyboxMaterials.Get("Booth", new Color(0.1f, 0.6f, 0.6f));

            Box("Base", booth, new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 1.4f), frameMat, local: true);
            Box("Back", booth, new Vector3(0f, 1.4f, -0.65f), new Vector3(1.4f, 2.8f, 0.1f), frameMat, local: true);
            Box("PostLeft", booth, new Vector3(-0.65f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 1.4f), frameMat, local: true);
            Box("PostRight", booth, new Vector3(0.65f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 1.4f), frameMat, local: true);
            Box("Roof", booth, new Vector3(0f, 2.85f, 0f), new Vector3(1.4f, 0.1f, 1.4f), frameMat, local: true);

            var cratePoint = new GameObject("CratePoint").transform;
            cratePoint.SetParent(booth, false);
            cratePoint.localPosition = new Vector3(0f, 0.6f, 0f);
        }

        private static void BuildChute(RoomLayoutSO layout, Transform root)
        {
            var chute = Group("Chute", root);
            chute.position = layout.ChutePosition;
            float dia = layout.ChuteRadius * 2f;

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(chute, false);
            rim.transform.localScale = new Vector3(dia + 0.3f, 0.03f, dia + 0.3f);
            rim.GetComponent<Renderer>().sharedMaterial = GreyboxMaterials.Get("ChuteRim", new Color(0.6f, 0.15f, 0.1f));
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hole.name = "Hole";
            hole.transform.SetParent(chute, false);
            hole.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            hole.transform.localScale = new Vector3(dia, 0.02f, dia);
            hole.GetComponent<Renderer>().sharedMaterial = GreyboxMaterials.Get("ChuteHole", new Color(0.02f, 0.02f, 0.02f));
            Object.DestroyImmediate(hole.GetComponent<Collider>());

            var target = new GameObject("TargetPoint").transform;
            target.SetParent(chute, false);
            target.localPosition = new Vector3(0f, 0.2f, 0f);
        }

        private static void BuildTerminal(RoomLayoutSO layout, Transform root)
        {
            var terminal = Group("UpgradeTerminal", root);
            terminal.SetPositionAndRotation(layout.TerminalPosition, Quaternion.Euler(0f, -90f, 0f));
            Box("Body", terminal, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 1.2f, 0.5f), GreyboxMaterials.Get("Terminal", new Color(0.2f, 0.25f, 0.3f)), local: true);
            Box("Screen", terminal, new Vector3(0f, 1.0f, -0.26f), new Vector3(0.6f, 0.4f, 0.02f),
                GreyboxMaterials.Get("TerminalScreen", new Color(0.2f, 1f, 0.5f), emission: 1.2f), local: true, collider: false);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Transform Group(string name, Transform parent, bool local = false)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, local);
            if (local) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; }
            return t;
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material,
                                     bool local = false, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position;
            else go.transform.position = position;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go.transform;
        }
    }
}
