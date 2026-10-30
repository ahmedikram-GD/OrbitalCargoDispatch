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
