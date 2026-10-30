using System.Collections.Generic;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>
    /// Pure payout maths for crates and launches. Failure is soft (GDD): an expired cryo crate or a
    /// wrong-bay crate pays less, never nothing; shipping bad cargo costs one fine per launch.
    /// </summary>
    public static class PayoutRules
    {
        public static int CratePayout(int basePayout, bool cryoExpired, bool wrongBay, ShiftConfigSO cfg)
        {
            float value = basePayout;
            if (cryoExpired) value *= cfg.ExpiredCryoMultiplier;
            if (wrongBay) value *= cfg.WrongBayMultiplier;
            return Mathf.FloorToInt(value + 0.5f); // round half up (Mathf.RoundToInt rounds half to even)
        }

        public static LaunchResult Launch(IReadOnlyList<CrateSnapshot> crates, BayId bay, ShiftConfigSO cfg)
        {
            var result = new LaunchResult { Bay = bay, CrateCount = crates.Count };
            for (int i = 0; i < crates.Count; i++)
            {
                CrateSnapshot c = crates[i];
                bool wrongBay = c.Destination != bay;
                if (wrongBay) result.WrongBayCount++;
                if (c.IsBad) result.BadShipped++;
                result.Credits += CratePayout(c.BasePayout, c.CryoExpired, wrongBay, cfg);
            }
            if (result.BadShipped > 0) result.FineApplied = cfg.UnscannedBadFine;
            return result;
        }
    }
}
