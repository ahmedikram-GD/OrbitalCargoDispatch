using OrbitalCargo.Config;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Art pass on top of the greybox: for every kit model set on the <see cref="RoomLayoutSO"/> art skin,
    /// hides the matching greybox renderers and places the kit pieces under <c>Room/Skin</c>. Greybox
    /// colliders are never touched, so walking, aiming and every station's interaction stay exactly as
    /// play-tested — the skin is pure looks and can be removed by clearing the layout fields. Piece sizes
    /// are measured from the models' bounds at build time, so kit scale and pivot quirks don't matter.
    /// </summary>
    public static class RoomSkinner
    {
        private const string SkinName = "Skin";

        public static void Apply(RoomLayoutSO layout, Transform room)
        {
            var skin = new GameObject(SkinName).transform;
            skin.SetParent(room, false);

            if (layout.FloorTile != null) SkinFloor(layout, room, skin);
            if (layout.WallPanel != null) SkinWalls(layout, room, skin);
            if (layout.ShuttleModel != null) SkinShuttles(layout, room);
            SkinProps(layout, skin);
        }

        // ── Floor ──────────────────────────────────────────────────────────────

        private static void SkinFloor(RoomLayoutSO layout, Transform room, Transform skin)
        {
            HideRenderer(room, "Shell/Floor");
            var group = Group("Floor", skin);

            Bounds b = Measure(layout.FloorTile);
            float s = layout.FloorTileScale;
            float w = layout.RoomSize.x, d = layout.RoomSize.y;
            int nx = Mathf.Max(1, Mathf.RoundToInt(w / (b.size.x * s)));
            int nz = Mathf.Max(1, Mathf.RoundToInt(d / (b.size.z * s)));
            // Stretch a hair so the tiles meet the walls exactly.
            var scale = new Vector3(w / (nx * b.size.x), s, d / (nz * b.size.z));
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var pos = new Vector3(-w * 0.5f + (x + 0.5f) * w / nx, 0f, -d * 0.5f + (z + 0.5f) * d / nz);
                // Top of the tile flush with y = 0, where everything stands.
                pos -= new Vector3(b.center.x * scale.x, b.max.y * scale.y, b.center.z * scale.z);
                Recolour(Place(layout.FloorTile, group, $"Floor_{x}_{z}", pos, Quaternion.identity, scale), layout.FloorMaterial);
            }
        }

        // ── Walls ──────────────────────────────────────────────────────────────

        private static void SkinWalls(RoomLayoutSO layout, Transform room, Transform skin)
        {
            foreach (var wall in new[] { "Wall_Far", "Wall_Near", "Wall_Right", "Wall_Left_Back", "Wall_Left_Front" })
                HideRenderer(room, "Shell/" + wall);
            var group = Group("Walls", skin);

            float w = layout.RoomSize.x, d = layout.RoomSize.y;
            float gapZ = layout.ConveyorWaypoints.Length > 0 ? layout.ConveyorWaypoints[0].z : 0f;
            float gapHalf = layout.AirlockWidth * 0.5f;

            // Each run: start → end along the wall's inner face, and the direction that faces into the room.
            Run(layout, group, "Far", new Vector3(-w * 0.5f, 0f, d * 0.5f), new Vector3(w * 0.5f, 0f, d * 0.5f), Vector3.back, windowMiddle: true);
            Run(layout, group, "Near", new Vector3(w * 0.5f, 0f, -d * 0.5f), new Vector3(-w * 0.5f, 0f, -d * 0.5f), Vector3.forward, windowMiddle: false);
            Run(layout, group, "Right", new Vector3(w * 0.5f, 0f, d * 0.5f), new Vector3(w * 0.5f, 0f, -d * 0.5f), Vector3.left, windowMiddle: false);
            // Left wall splits around the conveyor airlock; the greybox lintel and dark airlock stay visible.
            Run(layout, group, "LeftBack", new Vector3(-w * 0.5f, 0f, gapZ + gapHalf), new Vector3(-w * 0.5f, 0f, d * 0.5f), Vector3.right, windowMiddle: false);
            Run(layout, group, "LeftFront", new Vector3(-w * 0.5f, 0f, -d * 0.5f), new Vector3(-w * 0.5f, 0f, gapZ - gapHalf), Vector3.right, windowMiddle: false);

            // With an opaque kit wall in front of it, the planet glow moves just outside the window panels.
            var planet = room.Find("Shell/PlanetWindow");
            if (planet != null && layout.WallWindowPanel != null)
            {
                Bounds pb = Measure(layout.WallPanel);
                planet.position += Vector3.forward * (pb.size.z * layout.WallPanelScale + 0.3f);
                planet.localScale = new Vector3(planet.localScale.x, planet.localScale.y * 1.6f, planet.localScale.z);
            }
        }

        private static void Run(RoomLayoutSO layout, Transform group, string name, Vector3 from, Vector3 to, Vector3 inward, bool windowMiddle)
        {
            float length = Vector3.Distance(from, to);
            if (length < 0.1f) return;

            Bounds b = Measure(layout.WallPanel);
            float s = layout.WallPanelScale;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / (b.size.x * s)));
            var scale = new Vector3(length / (n * b.size.x), s, s);
            Vector3 along = (to - from).normalized;
            // Panel local +Z faces into the room (WallPanelYaw flips kits that face the other way).
            var rotation = Quaternion.LookRotation(inward, Vector3.up) * Quaternion.Euler(0f, layout.WallPanelYaw, 0f);
            // Push the panel outward so its inner face sits on the room boundary, never inside the room.
            Vector3 outward = -inward * (b.size.z * s * 0.5f);

            for (int i = 0; i < n; i++)
            {
                bool middle = windowMiddle && n >= 3 && i > 0 && i < n - 1;
                var model = middle && layout.WallWindowPanel != null ? layout.WallWindowPanel : layout.WallPanel;
                Vector3 pos = from + along * ((i + 0.5f) * length / n) + outward;
                pos -= rotation * new Vector3(b.center.x * scale.x, b.min.y * scale.y, b.center.z * scale.z);
                Recolour(Place(model, group, $"{name}_{i}", pos, rotation, scale), layout.WallMaterial);
            }
        }

        // ── Shuttles ───────────────────────────────────────────────────────────

        private static void SkinShuttles(RoomLayoutSO layout, Transform room)
        {
            foreach (var anchor in layout.Bays)
            {
                var shuttle = room.Find($"Bay_{anchor.Id}/Shuttle");
                if (shuttle == null) continue;
                // The cargo rails stay (they hold the crates); the canopy and engine pods become the ship.
                foreach (var part in new[] { "Canopy", "EngineLeft", "EngineRight", "StrutLeft", "StrutRight" })
                    HideRenderer(shuttle, part);

                Bounds b = Measure(layout.ShuttleModel);
                var rotation = Quaternion.Euler(0f, layout.ShuttleYaw, 0f);
                var ship = Place(layout.ShuttleModel, shuttle, "Ship", Vector3.zero, Quaternion.identity, Vector3.one * layout.ShuttleScale);
                ship.localRotation = rotation;
                ship.localPosition = layout.ShuttleOffset - rotation * (new Vector3(b.center.x, b.min.y, b.center.z) * layout.ShuttleScale);
                foreach (var r in ship.GetComponentsInChildren<Renderer>()) r.gameObject.isStatic = false; // it launches
            }
        }

        // ── Props ──────────────────────────────────────────────────────────────

        private static void SkinProps(RoomLayoutSO layout, Transform skin)
        {
            if (layout.Props == null || layout.Props.Length == 0) return;
            var group = Group("Props", skin);
            for (int i = 0; i < layout.Props.Length; i++)
            {
                var prop = layout.Props[i];
                if (prop.Prefab == null) continue;
                float s = prop.Scale > 0f ? prop.Scale : 1f;
                Place(prop.Prefab, group, $"{prop.Prefab.name}_{i}", prop.Position, Quaternion.Euler(0f, prop.Yaw, 0f), Vector3.one * s);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Transform Place(GameObject model, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            // Looks only: kit pieces must never block the crosshair ray or the player.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            return go.transform;
        }

        private static void Recolour(Transform piece, Material material)
        {
            if (material == null) return;
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
        }

        // Model-space bounds of a kit model (renderers combined), at scale 1 and identity rotation.
        private static Bounds Measure(GameObject model)
        {
            var temp = (GameObject)Object.Instantiate(model);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            var renderers = temp.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(temp);
            return b;
        }

        private static void HideRenderer(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}
