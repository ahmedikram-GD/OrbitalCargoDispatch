using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>The payout-relevant facts about one crate at launch time (no Unity objects, so rules stay testable).</summary>
    public struct CrateSnapshot
    {
        public int BasePayout;
        public BayId Destination;
        public bool CryoExpired;
        public bool IsBad;
        public bool IsScanned;
        public bool IsJunk;
        /// <summary>What a bad crate hides (null when clean) — names the news headline.</summary>
        public HiddenItemSO Hidden;

        /// <summary>The facts about <paramref name="crate"/> right now (destination falls back to <paramref name="fallbackBay"/> for a typeless crate).</summary>
        public static CrateSnapshot Of(Crates.Crate crate, BayId fallbackBay = BayId.Chute)
        {
            var type = crate.Type;
            return new CrateSnapshot
            {
                BasePayout = type != null ? type.Payout : 0,
                Destination = type != null ? type.Destination : fallbackBay,
                CryoExpired = crate.CryoExpired,
                IsBad = crate.IsBad,
                IsScanned = crate.IsScanned,
                IsJunk = type != null && type.IsJunk,
                Hidden = crate.HiddenItem,
            };
        }
    }

    /// <summary>What chuting one crate earns.</summary>
    public struct ChuteResult
    {
        public int Credits;
        /// <summary>True when a scanned contraband item goes into the collection.</summary>
        public bool Collected;
    }

    /// <summary>What one shuttle launch earned. Carried by <c>e_ShuttleLaunched</c> to juice, HUD and stats.</summary>
    public struct LaunchResult
    {
        /// <summary>Gross credits for the cargo (fine not deducted).</summary>
        public int Credits;
        /// <summary>Fine owed for shipping bad cargo (0 if none). Applied by the news alert, not here.</summary>
        public int FineApplied;
        public int BadShipped;
        /// <summary>The first bad item aboard (null if none) — the news alert's headline.</summary>
        public HiddenItemSO FirstBadItem;
        public int CrateCount;
        public int WrongBayCount;
        public BayId Bay;
        /// <summary>Where the shuttle lifted off, for FX and the coin burst.</summary>
        public Vector3 WorldPosition;
    }
}
