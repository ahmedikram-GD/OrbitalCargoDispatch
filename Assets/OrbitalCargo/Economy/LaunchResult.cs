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
    }

    /// <summary>What one shuttle launch earned. Carried by <c>e_ShuttleLaunched</c> to juice, HUD and stats.</summary>
    public struct LaunchResult
    {
        /// <summary>Gross credits for the cargo (fine not deducted).</summary>
        public int Credits;
        /// <summary>Fine owed for shipping bad cargo (0 if none). Applied by the news alert, not here.</summary>
        public int FineApplied;
        public int BadShipped;
        public int CrateCount;
        public int WrongBayCount;
        public BayId Bay;
        /// <summary>Where the shuttle lifted off, for FX and the coin burst.</summary>
        public Vector3 WorldPosition;
    }
}
