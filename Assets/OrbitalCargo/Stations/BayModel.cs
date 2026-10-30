using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Pure occupancy model of one shuttle bay: which slots hold which crate. No Unity objects, so
    /// every rule (nearest free slot, full, release) is unit-tested in edit mode; the <c>Bay</c>
    /// MonoBehaviour adapts it to the scene. Crates are referred to by an int id.
    /// </summary>
    public sealed class BayModel
    {
        private const int Empty = int.MinValue;

        private readonly int[] _occupants;

        public BayModel(int slotCount)
        {
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount), "A bay needs at least one slot.");
            _occupants = new int[slotCount];
            Clear();
        }

        public int SlotCount => _occupants.Length;

        public int Count { get; private set; }

        public bool IsFull => Count == _occupants.Length;

        public bool IsEmpty => Count == 0;

        /// <summary>Ids of every crate currently slotted, in slot order.</summary>
        public IEnumerable<int> OccupiedCrateIds
        {
            get
            {
                for (int i = 0; i < _occupants.Length; i++)
                    if (_occupants[i] != Empty) yield return _occupants[i];
            }
        }

        public bool IsOccupied(int slotIndex) => _occupants[slotIndex] != Empty;

        /// <summary>
        /// Closest free slot to <paramref name="point"/>. <paramref name="slotPositions"/> must be in the
        /// same order (and space) as the slots; only X/Z distance counts so aim height never matters.
        /// </summary>
        public bool TryGetNearestFreeSlot(Vector3 point, IReadOnlyList<Vector3> slotPositions, out int slotIndex)
        {
            slotIndex = -1;
            float best = float.MaxValue;
            int count = Mathf.Min(slotPositions.Count, _occupants.Length);
            for (int i = 0; i < count; i++)
            {
                if (_occupants[i] != Empty) continue;
                float dx = slotPositions[i].x - point.x;
                float dz = slotPositions[i].z - point.z;
                float d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    slotIndex = i;
                }
            }
            return slotIndex >= 0;
        }

        public void Occupy(int slotIndex, int crateId)
        {
            if (_occupants[slotIndex] != Empty)
                throw new InvalidOperationException($"[BayModel] Slot {slotIndex} already holds crate {_occupants[slotIndex]}.");
            _occupants[slotIndex] = crateId;
            Count++;
        }

        public bool TryRelease(int slotIndex, out int crateId)
        {
            crateId = _occupants[slotIndex];
            if (crateId == Empty) return false;
            _occupants[slotIndex] = Empty;
            Count--;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _occupants.Length; i++) _occupants[i] = Empty;
            Count = 0;
        }
    }
}
