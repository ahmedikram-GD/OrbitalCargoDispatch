using System;
using System.Collections.Generic;
using OrbitalCargo.Crates;
using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// The player's hands: which crates are carried, in stack order, and how many fit. Owns attaching
    /// crates to the hold point and popping the top one off for a station. No physics — crates are
    /// parented, see <see cref="CarryableCrate"/>. Capacity starts from <see cref="InteractionConfigSO"/>
    /// and the grav-sled upgrade raises it via <see cref="SetCapacity"/>.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Interaction/Crate Carrier")]
    public sealed class CrateCarrier : MonoBehaviour
    {
        [Tooltip("FP_InteractionConfig. Required.")]
        [SerializeField] private InteractionConfigSO config;
        [Tooltip("Where carried crates are parented (HoldPoint under the Main Camera).")]
        [SerializeField] private Transform holdPoint;

        private readonly List<Crate> _carried = new List<Crate>();

        public int Capacity { get; private set; } = 1;
        public IReadOnlyList<Crate> Carried => _carried;
        public Crate Top => _carried.Count > 0 ? _carried[_carried.Count - 1] : null;
        public bool HasCrate => _carried.Count > 0;
        public bool IsFull => _carried.Count >= Capacity;
        public bool TopIsJunk => Top != null && Top.Type != null && Top.Type.IsJunk;
        public Transform HoldPoint => holdPoint;

        /// <summary>Raised whenever the carried set changes (pick up, pop, capacity).</summary>
        public event Action Changed;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (config == null) Debug.LogError("[CrateCarrier] No InteractionConfigSO assigned.", this);
            if (holdPoint == null) Debug.LogError("[CrateCarrier] No hold point assigned.", this);
            Capacity = config != null ? Mathf.Max(1, config.CarryCapacity) : 1;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetCapacity(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
            Changed?.Invoke();
        }

        /// <summary>Take <paramref name="crate"/> into the hands (stacked on top). False if full or not pickable.</summary>
        public bool TryPickUp(Crate crate)
        {
            if (crate == null || IsFull || !crate.IsPickable || config == null) return false;

            _carried.Add(crate);
            crate.SetState(CrateState.Carried);
            crate.Carryable.AttachTo(holdPoint, StackOffset(_carried.Count - 1), config.CarriedScale,
                                     config.FlyToHandDuration, config.FlyArcHeight);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Remove the top crate from the hands (unparented in place). Null when empty.</summary>
        public Crate PopTop()
        {
            var crate = Top;
            if (crate == null) return null;

            _carried.RemoveAt(_carried.Count - 1);
            crate.Carryable.Detach();
            Changed?.Invoke();
            return crate;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private Vector3 StackOffset(int index) => Vector3.up * (index * config.CarryStackSpacing);
    }
}
