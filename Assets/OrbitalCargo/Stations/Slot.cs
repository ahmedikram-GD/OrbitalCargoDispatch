using OrbitalCargo.Crates;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>One crate position on a bay pad. Its transform is the crate's anchor (pad top, centred).</summary>
    [AddComponentMenu("OrbitalCargo/Stations/Slot")]
    public sealed class Slot : MonoBehaviour
    {
        [Tooltip("Index in the bay's slot list; matches RoomLayoutSO.GetSlotLocalPositions order.")]
        [SerializeField] private int index;

        public int Index => index;
        public Transform Anchor => transform;

        /// <summary>Crate currently on this slot (set by the bay).</summary>
        public Crate Occupant { get; set; }
    }
}
