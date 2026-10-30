using DG.Tweening;
using GameKit.Architecture.Events;
using GameKit.UI.Animations;
using OrbitalCargo.Config;
using OrbitalCargo.Economy;
using TMPro;
using UnityEngine;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// HUD half of a launch payout: a "+60 ¢" pop at the shuttle's screen position and the kit coin
    /// trail (<see cref="UICollector"/>) flying from there into the credits counter. The first coin to
    /// land triggers the kit <c>CoinsWidget</c> count-up, which reveals the deferred balance. If no
    /// collector is listening, it falls back to snapping the counter so credits never go unshown.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Payout Popup Presenter")]
    public sealed class PayoutPopupPresenter : MonoBehaviour
    {
        private const string CoinPoolId = "Coin";

        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;
        [Tooltip("e_CreditsChanged — fallback snap when no coin trail can play.")]
        [SerializeField] private GameEvent creditsChanged;
        [Tooltip("ShiftConfig_Pitch — credits-per-coin and coin count limits.")]
        [SerializeField] private ShiftConfigSO config;

        [Header("HUD")]
        [SerializeField] private RectTransform canvasRect;
        [Tooltip("Moved to the shuttle's screen point; the coin trail starts here.")]
        [SerializeField] private RectTransform coinSource;
        [Tooltip("Where coins fly to (the credits icon).")]
        [SerializeField] private RectTransform coinTarget;
        [SerializeField] private TextMeshProUGUI payoutLabel;

        [Header("Pop")]
        [Min(0f)] [SerializeField] private float popRise = 120f;
        [Min(0.1f)] [SerializeField] private float popDuration = 1.1f;
        [Tooltip("World offset above the launch point to project from (the cargo, not the floor).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

        private Camera _camera;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (payoutLabel != null) payoutLabel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
        }

        private void OnDisable()
        {
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleShuttleLaunched(LaunchResult result)
        {
            if (result.Credits <= 0 || config == null) return;

            PlaceSourceAt(result.WorldPosition + worldOffset);
            ShowPop(result.Credits);

            int coins = Mathf.Clamp(result.Credits / config.CreditsPerCoin, config.MinCoins, config.MaxCoins);
            if (UICollector.PlayTrailAction != null && coinSource != null && coinTarget != null)
                UICollector.PlayTrailAction.Invoke(CoinPoolId, coinSource, coinTarget, coins);
            else if (creditsChanged != null)
                creditsChanged.Invoke();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void PlaceSourceAt(Vector3 world)
        {
            if (coinSource == null || canvasRect == null) return;
            if (_camera == null) _camera = Camera.main;

            Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.35f);
            if (_camera != null)
            {
                Vector3 p = _camera.WorldToScreenPoint(world);
                if (p.z > 0f) screen = p; // behind the camera: fall back to lower-centre
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                coinSource.anchoredPosition = local;
        }

        private void ShowPop(int credits)
        {
            if (payoutLabel == null || coinSource == null) return;

            var rt = payoutLabel.rectTransform;
            DOTween.Kill(rt);
            DOTween.Kill(payoutLabel);
            payoutLabel.gameObject.SetActive(true);
            payoutLabel.text = $"+{credits} ¢";
            payoutLabel.alpha = 1f;
            rt.anchoredPosition = coinSource.anchoredPosition;
            rt.localScale = Vector3.one * 0.6f;

            rt.DOScale(1.15f, 0.25f).SetEase(Ease.OutBack).SetLink(gameObject);
            rt.DOAnchorPosY(rt.anchoredPosition.y + popRise, popDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
            payoutLabel.DOFade(0f, popDuration * 0.4f).SetDelay(popDuration * 0.6f).SetLink(gameObject)
                .OnComplete(() => payoutLabel.gameObject.SetActive(false));
        }
    }
}
