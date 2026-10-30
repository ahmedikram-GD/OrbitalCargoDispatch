using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>
    /// Every tunable number for a shift in one asset (KICKOFF "Tuning"): length, conveyor speed, cryo
    /// timer, scan duration, payout modifiers, chute bonus, fine, volatile slowdown, upgrade price, and
    /// the shuttle launch feel. Designers tweak this asset; no gameplay script hard-codes these values.
    /// Walk speed lives in FP_MovementConfig, per-type payouts in each CrateTypeSO.
    /// </summary>
    [CreateAssetMenu(fileName = "ShiftConfig_", menuName = "OrbitalCargo/Shift/Shift Config")]
    public class ShiftConfigSO : ScriptableObject
    {
        [Header("Shift")]
        [Tooltip("Seconds in one shift (the pitch shift is ~5 minutes).")]
        [Min(10f)] public float ShiftLengthSeconds = 300f;
        [Tooltip("Belt speed in m/s.")]
        [Min(0.05f)] public float ConveyorSpeed = 0.8f;
        [Tooltip("Centre-to-centre gap (m) crates keep on the belt; crates are 0.8 m.")]
        [Min(0.5f)] public float ConveyorSpacing = 1.0f;
        [Tooltip("Spawn timeline for the first (pitch) shift.")]
        public SpawnScheduleSO Schedule;
        [Tooltip("Faster timeline for the shifts after the first.")]
        public SpawnScheduleSO ScheduleShift2;

        [Header("Crate rules")]
        [Tooltip("Seconds the scanner booth takes to reveal a crate. (Cryo time and volatile slowdown live on their CrateTypeSO.)")]
        [Min(0.1f)] public float ScanDurationSeconds = 2.5f;

        [Header("Payouts")]
        [Tooltip("Payout multiplier for a cryo crate whose ring ran out before launch.")]
        [Range(0f, 1f)] public float ExpiredCryoMultiplier = 0.4f;
        [Tooltip("Payout multiplier for a crate launched from the wrong bay.")]
        [Range(0f, 1f)] public float WrongBayMultiplier = 0.5f;
        [Tooltip("Credits for chuting a crate the scanner showed was bad.")]
        [Min(0)] public int ChuteBonus = 25;
        [Tooltip("Fine when a launch ships one or more bad crates. Applied once per launch.")]
        [Min(0)] public int UnscannedBadFine = 30;

        [Header("Upgrades")]
        [Min(0)] public int GravSledPrice = 150;
        [Min(1)] public int CarryCapacityBase = 1;
        [Min(1)] public int CarryCapacityGravSled = 3;

        [Header("Shuttle launch")]
        [Tooltip("Metres the shuttle climbs before it's gone.")]
        [Min(1f)] public float LaunchHeight = 30f;
        [Tooltip("Seconds for the climb (eases in, so it starts slow and rockets away).")]
        [Min(0.1f)] public float LaunchDuration = 1.6f;
        [Tooltip("Seconds before a fresh shuttle drops back into the empty bay.")]
        [Min(0f)] public float ShuttleReturnDelay = 0.6f;
        [Tooltip("Seconds for the fresh shuttle to descend into place.")]
        [Min(0.1f)] public float ShuttleReturnDuration = 0.8f;

        [Header("Payout presentation")]
        [Tooltip("Credits represented by one flying coin.")]
        [Min(1)] public int CreditsPerCoin = 5;
        [Min(1)] public int MinCoins = 4;
        [Min(1)] public int MaxCoins = 14;
    }
}
