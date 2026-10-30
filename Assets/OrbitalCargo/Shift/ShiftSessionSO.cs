using System;
using GameKit.Architecture.Variables;
using OrbitalCargo.Economy;
using UnityEngine;

namespace OrbitalCargo.Shift
{
    /// <summary>
    /// The shift in progress (or just finished), shared as an asset so the summary FSM state — a
    /// ScriptableObject that can't reference the scene — can read what <see cref="ShiftController"/>
    /// recorded. Runtime fields are [NonSerialized]; only the shift number persists
    /// (<c>vDBInt_ShiftNumber</c>, 1-based).
    /// </summary>
    [CreateAssetMenu(fileName = "ShiftSession", menuName = "OrbitalCargo/Shift/Shift Session")]
    public class ShiftSessionSO : ScriptableObject
    {
        [Tooltip("vDBInt_ShiftNumber — which shift the player is on (1 = the pitch schedule, 2+ = the faster one).")]
        [SerializeField] private DBInt shiftNumber;

        [NonSerialized] private ShiftStats _stats;

        public ShiftStats Stats => _stats ??= new ShiftStats();
        public int ShiftNumber => shiftNumber != null ? Mathf.Max(1, shiftNumber.GetValue()) : 1;

        /// <summary>The summary's 2× reward has been taken for the shift just ended.</summary>
        [NonSerialized] public bool DoublePayoutClaimed;

        public void BeginShift()
        {
            Stats.Reset();
            DoublePayoutClaimed = false;
        }

        public void AdvanceShift()
        {
            if (shiftNumber != null) shiftNumber.SetValue(ShiftNumber + 1);
        }
    }
}
