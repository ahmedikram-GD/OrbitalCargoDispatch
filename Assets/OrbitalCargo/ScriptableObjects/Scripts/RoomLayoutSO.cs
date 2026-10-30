using System;
using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>
    /// Data-only description of the single station room: where the conveyor, bays, scanner booth,
    /// chute, upgrade terminal and player spawn sit. The editor <c>RoomBuilder</c> turns this into
    /// greybox geometry (and, in the art pass, into Kenney kit pieces), so moving a station is an
    /// asset edit plus one menu click — never hand-placing objects in the scene.
    /// <para>Coordinates are world space, room centred on the origin, +Z towards the far (bay) wall.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RoomLayout_", menuName = "OrbitalCargo/Room/Room Layout")]
    public class RoomLayoutSO : ScriptableObject
    {
        [Serializable]
        public struct BayAnchor
        {
            [Tooltip("Bay letter used in object names (Bay_A, Slot_A_0...).")]
            public string Id;
            [Tooltip("Centre of the loading pad on the floor.")]
            public Vector3 Position;
            [Tooltip("Pad facing in degrees; 180 = slots face the room centre from the far wall.")]
            public float Yaw;
            [Tooltip("Colour code for the pad — the 'one glance' destination cue.")]
            public Color Color;
        }

        [Serializable]
        public struct PropPlacement
        {
            [Tooltip("Kit model or prefab (e.g. Art/Kenney/SpaceStationKit/computer.fbx). Visual only — no collider is added.")]
            public GameObject Prefab;
            public Vector3 Position;
            public float Yaw;
            [Min(0.01f)] public float Scale;
        }

        [Header("Shell")]
        [Tooltip("Inner floor size in metres (X = width, Y = depth along Z).")]
        public Vector2 RoomSize = new Vector2(16f, 12f);
        [Min(1f)] public float WallHeight = 4f;
        [Min(0.05f)] public float WallThickness = 0.3f;

        [Header("Player")]
        public Vector3 PlayerSpawn = Vector3.zero;
        [Tooltip("Spawn facing in degrees. -135 looks toward the conveyor end, where the first crate arrives.")]
        public float PlayerYaw = -135f;

        [Header("Conveyor")]
        [Tooltip("Belt path, airlock first. Crates ride on top of the belt (Y = belt top height).")]
        public Vector3[] ConveyorWaypoints =
        {
            new Vector3(-8.2f, 0.8f, -2.5f),
            new Vector3(-2.6f, 0.8f, -2.5f),
        };
        [Min(0.3f)] public float ConveyorWidth = 1.1f;
        [Tooltip("Width of the opening cut in the side wall where the belt enters.")]
        [Min(0.5f)] public float AirlockWidth = 1.8f;

        [Header("Bays")]
        public BayAnchor[] Bays =
        {
            new BayAnchor { Id = "A", Position = new Vector3(-4.5f, 0f, 4.2f), Yaw = 180f, Color = new Color(0.45f, 0.55f, 0.68f) },
            new BayAnchor { Id = "B", Position = new Vector3( 0.0f, 0f, 4.2f), Yaw = 180f, Color = new Color(0.80f, 0.95f, 1.00f) },
            new BayAnchor { Id = "C", Position = new Vector3( 4.5f, 0f, 4.2f), Yaw = 180f, Color = new Color(1.00f, 0.80f, 0.10f) },
        };
        [Min(1)] public int SlotColumns = 3;
        [Min(1)] public int SlotRows = 2;
        [Tooltip("Centre-to-centre distance between slots (crates are 0.8 m).")]
        [Min(0.5f)] public float SlotSpacing = 1.1f;
        [Tooltip("Height of the pad top; slotted crates rest here.")]
        [Min(0f)] public float PadHeight = 0.1f;

        [Header("Stations")]
        public Vector3 BoothPosition = new Vector3(-1.0f, 0f, -4.6f);
        public Vector3 ChutePosition = new Vector3(1.4f, 0f, -4.3f);
        [Min(0.3f)] public float ChuteRadius = 0.8f;
        [Tooltip("Upgrade terminal, by the door on the side wall opposite the airlock.")]
        public Vector3 TerminalPosition = new Vector3(7.3f, 0f, -3.5f);

        // ── Art skin (optional) ────────────────────────────────────────────────
        // Empty slots keep the greybox. Filled slots hide the matching greybox renderers (their colliders
        // stay, so interaction and walking are unchanged) and tile the kit model in their place.

        [Header("Art skin (optional — empty keeps the greybox)")]
        [Tooltip("Floor tile model, tiled across the room (Kenney SpaceStationKit/floor-panel).")]
        public GameObject FloorTile;
        [Min(0.1f)] public float FloorTileScale = 2f;
        [Tooltip("Optional material for every floor tile (e.g. the kit atlas tinted darker so colour-coded bays and crates pop). Empty = the model's own.")]
        public Material FloorMaterial;
        [Tooltip("Wall panel model, tiled along the four walls (Kenney SpaceStationKit/wall).")]
        public GameObject WallPanel;
        [Tooltip("Panel used for the middle of the far wall, where the planet glows through (SpaceStationKit/wall-window). Empty = WallPanel.")]
        public GameObject WallWindowPanel;
        [Tooltip("Uniform scale for wall panels; Kenney walls are 1 m tall, so WallHeight / 1.")]
        [Min(0.1f)] public float WallPanelScale = 4f;
        [Tooltip("Extra yaw on every wall panel, to flip a kit whose detailed face points the wrong way.")]
        public float WallPanelYaw;
        [Tooltip("Optional material for every wall panel. Empty = the model's own.")]
        public Material WallMaterial;
        [Tooltip("Model for the cargo shuttle hovering over each bay pad (Kenney SpaceKit/craft_cargoA). Replaces the canopy and engine pods.")]
        public GameObject ShuttleModel;
        [Min(0.01f)] public float ShuttleScale = 1.6f;
        [Tooltip("Bay-local position of the shuttle model (above the slots, clear of the crates).")]
        public Vector3 ShuttleOffset = new Vector3(0f, 1.9f, 0f);
        public float ShuttleYaw = 90f;
        [Tooltip("Set dressing: computers, containers, pipes... Visual only.")]
        public PropPlacement[] Props = Array.Empty<PropPlacement>();

        /// <summary>Total slot count per bay (columns × rows).</summary>
        public int SlotsPerBay => SlotColumns * SlotRows;

        /// <summary>
        /// Slot positions relative to a bay anchor (before the anchor's yaw), centred on the pad.
        /// Index order: row by row, front row first, left to right as seen from the pad's local -Z.
        /// The builder and the runtime <c>Bay</c> both use this so they can never disagree.
        /// </summary>
        public Vector3[] GetSlotLocalPositions()
        {
            var positions = new Vector3[SlotsPerBay];
            float halfCols = (SlotColumns - 1) * 0.5f;
            float halfRows = (SlotRows - 1) * 0.5f;
            for (int i = 0; i < positions.Length; i++)
            {
                int col = i % SlotColumns;
                int row = i / SlotColumns;
                positions[i] = new Vector3((col - halfCols) * SlotSpacing, PadHeight, (row - halfRows) * SlotSpacing);
            }
            return positions;
        }
    }
}
