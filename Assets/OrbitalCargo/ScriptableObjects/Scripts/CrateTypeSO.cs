using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>Where a crate should end up. One glance at the crate tells the player which.</summary>
    public enum BayId
    {
        A,      // Heavy — standard crates
        B,      // Cryo
        C,      // Hazard — volatile crates
        Chute,  // junk and scanned contraband
    }

    /// <summary>
    /// Data for one crate type (Standard, Cryo, Junk, Volatile): its look, destination, payout and
    /// the single twist it adds. Crates are fully data-driven — gameplay code never switches on a
    /// type name. "Suspicious" is not a type: it is a flag on a spawn entry (any type can hide
    /// something), see <c>SpawnScheduleSO</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "CT_", menuName = "OrbitalCargo/Crates/Crate Type")]
    public class CrateTypeSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "standard";
        public string DisplayName = "Standard";

        [Header("Look")]
        [Tooltip("Casing material (URP Lit). The colour IS the destination cue.")]
        public Material CasingMaterial;
        [Tooltip("Colour of the stripe/decal band on the crate.")]
        public Color AccentColor = Color.black;

        [Header("Rules")]
        [Tooltip("Bay this crate belongs in. Wrong bay still pays, just less (ShiftConfigSO.WrongBayMultiplier).")]
        public BayId Destination = BayId.A;
        [Tooltip("Credits paid when launched in the right bay (or chuted, for junk).")]
        [Min(0)] public int Payout = 10;

        [Header("Twists (0 / false = none)")]
        [Tooltip("Junk: swipe up to throw it into the chute.")]
        public bool IsJunk;
        [Tooltip("Cryo: seconds before the ring timer runs out. 0 = no timer.")]
        [Min(0f)] public float CryoSeconds;
        [Tooltip("Volatile: walk-speed multiplier while carried (1 = no slowdown).")]
        [Range(0.1f, 1f)] public float VolatileSlowMultiplier = 1f;
        [Tooltip("Volatile: hold-point wobble amplitude in metres while carried (0 = steady).")]
        [Min(0f)] public float WobbleAmplitude;

        /// <summary>True when <paramref name="bay"/> is this crate's destination (false = wrong bay, pays less).</summary>
        public bool BelongsIn(BayId bay) => Destination == bay;
    }
}
