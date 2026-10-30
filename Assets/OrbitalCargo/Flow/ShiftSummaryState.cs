using System.Collections;
using GameKit.Architecture.Events;
using GameKit.StateMachine;
using GameKit.UI;
using OrbitalCargo.Ads;
using OrbitalCargo.Economy;
using OrbitalCargo.Shift;
using OrbitalCargo.UI;
using UnityEngine;

namespace OrbitalCargo.Flow
{
    /// <summary>
    /// End-of-shift screen and its three choices. Reads the tallies <c>ShiftController</c> left in
    /// <see cref="ShiftSessionSO"/>; "2× payout" plays a rewarded ad through the scene service published
    /// in <see cref="RewardedAdAnchorSO"/> and on Completed adds the shift's earnings again (once);
    /// "Buy Grav-Sled" goes through <see cref="UpgradeService"/>; "Start next shift" advances the shift
    /// number and raises e_NextShiftPressed. The world is left running (the ad and the list animation
    /// need time) but the player and the touch controls are stood down via e_GamePaused.
    /// </summary>
    [CreateAssetMenu(fileName = "ShiftSummaryState", menuName = "OrbitalCargo/Flow/Shift Summary State")]
    public class ShiftSummaryState : UIViewState
    {
        private const string AdPlacement = "summary_2x";

        [SerializeField] private ShiftSessionSO session;
        [SerializeField] private CreditsService credits;
        [SerializeField] private UpgradeService upgrades;
        [Tooltip("RewardedAdAnchor — filled by the scene's RewardedAdRegistrar.")]
        [SerializeField] private RewardedAdAnchorSO ads;

        [Header("Events")]
        [SerializeField] private GameEvent onNextShiftPressed;
        [SerializeField] private GameEvent onDoublePayoutClaimed;
        [Tooltip("e_GamePaused (bool) — stands the player and touch controls down while the summary is up.")]
        [SerializeField] private GameEventWithBool onGamePaused;

        private ShiftSummaryView _view;
        private bool _adPlaying;
        private bool _playerHeld;

        public override IEnumerator Enter(IState previous)
        {
            HoldPlayer(true);
            _adPlaying = false;
            yield return base.Enter(previous);
        }

        public override IEnumerator Exit()
        {
            yield return base.Exit();
            HoldPlayer(false);
        }

        public override IEnumerator ExitImmediate()
        {
            yield return base.ExitImmediate();
            HoldPlayer(false);
        }

        // ── View binding ───────────────────────────────────────────────────────

        protected override void OnBindView(UIBase view)
        {
            _view = view as ShiftSummaryView;
            if (_view == null)
            {
                Debug.LogError("[ShiftSummaryState] ShiftSummary prefab has no ShiftSummaryView.");
                return;
            }

            var stats = session != null ? session.Stats : new ShiftStats();
            _view.SetStats(session != null ? session.ShiftNumber : 1, stats.CreditsEarned, stats.Launches,
                           stats.CratesShipped, stats.ItemsFound, stats.Fines);
            Refresh();

            _view.DoubleButton.onClick.AddListener(OnDoubleClicked);
            _view.BuyButton.onClick.AddListener(OnBuyClicked);
            _view.NextButton.onClick.AddListener(OnNextClicked);
        }

        protected override void OnUnbindView(UIBase view)
        {
            if (_view == null) return;
            _view.DoubleButton.onClick.RemoveListener(OnDoubleClicked);
            _view.BuyButton.onClick.RemoveListener(OnBuyClicked);
            _view.NextButton.onClick.RemoveListener(OnNextClicked);
            _view = null;
        }

        // ── Buttons ────────────────────────────────────────────────────────────

        private void OnDoubleClicked()
        {
            if (_adPlaying || !CanDouble()) return;
            _adPlaying = true;
            Refresh();
            ads.Service.Show(AdPlacement, HandleAdFinished);
        }

        private void HandleAdFinished(RewardedAdResult result)
        {
            _adPlaying = false;
            if (result == RewardedAdResult.Completed && session != null && !session.DoublePayoutClaimed)
            {
                session.DoublePayoutClaimed = true;
                if (credits != null) credits.Add(session.Stats.CreditsEarned);
                if (onDoublePayoutClaimed != null) onDoublePayoutClaimed.Invoke();
            }
            Refresh();
        }

        private void OnBuyClicked()
        {
            if (upgrades != null) upgrades.TryBuyGravSled();
            Refresh();
        }

        private void OnNextClicked()
        {
            if (_adPlaying) return;
            if (session != null) session.AdvanceShift();
            if (onNextShiftPressed != null) onNextShiftPressed.Invoke();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool CanDouble() =>
            session != null && !session.DoublePayoutClaimed && session.Stats.CreditsEarned > 0 && ads != null && ads.IsReady;

        private void Refresh()
        {
            if (_view == null) return;

            bool claimed = session != null && session.DoublePayoutClaimed;
            int earned = session != null ? session.Stats.CreditsEarned : 0;
            _view.SetDouble(!_adPlaying && CanDouble(), claimed, _adPlaying, earned);

            bool owned = upgrades != null && upgrades.GravSledOwned;
            _view.SetBuy(!_adPlaying && upgrades != null && upgrades.CanBuyGravSled, owned, upgrades != null ? upgrades.GravSledPrice : 0);
            _view.SetBalance(credits != null ? credits.Balance : 0);
            _view.NextButton.interactable = !_adPlaying;
        }

        private void HoldPlayer(bool held)
        {
            if (held == _playerHeld && !held) return;
            _playerHeld = held;
            if (onGamePaused != null) onGamePaused.Raise(held);
        }
    }
}
