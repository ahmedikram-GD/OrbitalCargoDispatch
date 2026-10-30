using DG.Tweening;
using GameKit.Architecture.Events;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>
    /// Consequence of shipping contraband (GDD: "ship a bad crate unscanned → news alert after launch +
    /// small fine"). Listens for launches that carried bad cargo and, a beat after lift-off, deducts the
    /// fine and raises e_NewsAlert with the ticker line (headline from the first bad item). Light and
    /// funny, never grim: credits floor at zero.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Economy/News Desk")]
    public sealed class NewsDesk : MonoBehaviour
    {
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;
        [Tooltip("e_NewsAlert (string) — the HUD ticker shows it.")]
        [SerializeField] private GameEventWithString onNewsAlert;
        [SerializeField] private CreditsService credits;
        [Tooltip("ShiftConfig_Pitch — delay and ticker format.")]
        [SerializeField] private ShiftConfigSO config;

        private void OnEnable()
        {
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
        }

        private void OnDisable()
        {
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
        }

        private void HandleShuttleLaunched(LaunchResult result)
        {
            if (result.FineApplied <= 0 || config == null) return;

            string headline = result.FirstBadItem != null ? result.FirstBadItem.NewsHeadline : "Contraband reaches colony!";
            string line = string.Format(config.NewsFormat, headline, result.FineApplied);
            int fine = result.FineApplied;

            // Game time (ignoreTimeScale: false): DelayedCall defaults to real time, which would land the
            // fine and the ticker while the game is paused.
            DOVirtual.DelayedCall(config.NewsAlertDelay, () =>
            {
                if (credits != null) credits.Add(-fine);
                if (onNewsAlert != null) onNewsAlert.Raise(line);
            }, ignoreTimeScale: false).SetLink(gameObject);
        }
    }
}
