using System;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// A cargo crate: its type, its lifecycle state, and whether it hides something. Pure state +
    /// identity — movement lives in <see cref="CarryableCrate"/>, looks in <see cref="CrateVisuals"/>.
    /// Pooled: <see cref="Configure"/> fully resets it for reuse.
    /// </summary>
    [RequireComponent(typeof(CarryableCrate))]
    [AddComponentMenu("OrbitalCargo/Crates/Crate")]
    public sealed class Crate : MonoBehaviour
    {
        [Tooltip("Type used when the crate is placed by hand in a scene; the spawner overrides it via Configure.")]
        [SerializeField] private CrateTypeSO type;
        [SerializeField] private CrateVisuals visuals;
        [SerializeField] private CarryableCrate carryable;

        public int Id => GetInstanceID();
        public CrateTypeSO Type => type;
        public CarryableCrate Carryable => carryable;

        public CrateState State { get; private set; } = CrateState.OnConveyor;
        public bool IsSuspicious { get; private set; }
        public HiddenItemSO HiddenItem { get; private set; }
        public bool IsBad => HiddenItem != null;
        public bool IsScanned { get; set; }

        /// <summary>True once a cryo crate's ring ran out (set by the cryo timer, Slice 4). Pays less at launch.</summary>
        public bool CryoExpired { get; set; }

        /// <summary>True while the player may pick this crate up.</summary>
        public bool IsPickable => State == CrateState.OnConveyor || State == CrateState.Pickable;

        public event Action<Crate, CrateState> StateChanged;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (carryable == null) carryable = GetComponent<CarryableCrate>();
            if (visuals == null) visuals = GetComponent<CrateVisuals>();
            if (type != null && visuals != null) visuals.Apply(type);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>(Re)initialise a pooled crate as a fresh crate of <paramref name="crateType"/> on the belt.</summary>
        public void Configure(CrateTypeSO crateType, bool suspicious, HiddenItemSO hidden)
        {
            type = crateType;
            IsSuspicious = suspicious;
            HiddenItem = hidden;
            IsScanned = false;
            CryoExpired = false;
            if (visuals != null) visuals.Apply(type);
            State = CrateState.OnConveyor;
            StateChanged?.Invoke(this, State);
        }

        public void SetState(CrateState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }
}
