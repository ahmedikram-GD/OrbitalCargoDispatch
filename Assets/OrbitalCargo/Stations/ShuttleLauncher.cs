using System.Collections.Generic;
using DG.Tweening;
using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Launches one bay's shuttle: takes the six crates, prices them with <see cref="PayoutRules"/>,
    /// pays out (display deferred to the coin trail), raises <c>e_ShuttleLaunched</c>, and flies the
    /// shuttle (with the crates aboard) up and away before a fresh one drops back in. Feedback —
    /// smoke, shake, haptics, sound, coins — is NOT here: the juice presenter and HUD react to the event.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Shuttle Launcher")]
    public sealed class ShuttleLauncher : MonoBehaviour
    {
        private const string CratePoolId = "Crate";

        [SerializeField] private Bay bay;
        [Tooltip("The shuttle cradle that flies away (Bay_X/Shuttle). Crates ride on it.")]
        [SerializeField] private Transform shuttle;
        [Tooltip("ShiftConfig_Pitch — payouts and launch timing.")]
        [SerializeField] private ShiftConfigSO config;
        [Tooltip("CreditsService asset.")]
        [SerializeField] private CreditsService credits;
        [Tooltip("CratePooler — launched crates are returned here.")]
        [SerializeField] private PoolManagerSO cratePooler;

        [Header("Events")]
        [Tooltip("e_ShuttleLaunched. Raised the moment the shuttle lifts off.")]
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;

        private readonly List<CrateSnapshot> _snapshots = new List<CrateSnapshot>(6);
        private Vector3 _home;

        public bool IsLaunching { get; private set; }
        public bool CanLaunch => !IsLaunching && bay != null && bay.Model != null && bay.Model.IsFull;
        public Bay Bay => bay;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (shuttle != null) _home = shuttle.localPosition;
            if (bay == null || shuttle == null || config == null || credits == null)
                Debug.LogError("[ShuttleLauncher] Missing references — run Tools/OrbitalCargo/Setup Game Scene.", this);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public bool TryLaunch()
        {
            if (!CanLaunch || shuttle == null || config == null) return false;

            IsLaunching = true;
            bay.IsLocked = true;

            List<Crate> cargo = bay.TakeAll();
            _snapshots.Clear();
            foreach (var crate in cargo)
            {
                _snapshots.Add(new CrateSnapshot
                {
                    BasePayout = crate.Type != null ? crate.Type.Payout : 0,
                    Destination = crate.Type != null ? crate.Type.Destination : bay.Id,
                    CryoExpired = crate.CryoExpired,
                    IsBad = crate.IsBad,
                    IsScanned = crate.IsScanned,
                    IsJunk = crate.Type != null && crate.Type.IsJunk,
                    Hidden = crate.HiddenItem,
                });
                crate.Carryable.CompleteMove(); // a crate still snapping in must land before it boards
                crate.SetState(CrateState.Consumed);
                crate.transform.SetParent(shuttle, true);
            }

            LaunchResult result = PayoutRules.Launch(_snapshots, bay.Id, config);
            result.WorldPosition = shuttle.position;
            if (credits != null) credits.Add(result.Credits, deferDisplay: true);
            if (onShuttleLaunched != null) onShuttleLaunched.Raise(result);

            shuttle.DOLocalMoveY(_home.y + config.LaunchHeight, config.LaunchDuration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject)
                .OnComplete(() => HandleShuttleGone(cargo));
            return true;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void HandleShuttleGone(List<Crate> cargo)
        {
            foreach (var crate in cargo)
            {
                if (crate == null) continue;
                crate.transform.SetParent(null, false);
                if (cratePooler != null) cratePooler.Release(CratePoolId, crate.gameObject);
                else Destroy(crate.gameObject);
            }

            // A fresh shuttle drops back in from above.
            shuttle.localPosition = _home + Vector3.up * (config.LaunchHeight * 0.4f);
            shuttle.DOLocalMoveY(_home.y, config.ShuttleReturnDuration)
                .SetDelay(config.ShuttleReturnDelay)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .OnComplete(HandleShuttleBack);
        }

        private void HandleShuttleBack()
        {
            IsLaunching = false;
            bay.IsLocked = false;
        }
    }
}
