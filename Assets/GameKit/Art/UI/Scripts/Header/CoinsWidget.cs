using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Architecture.Variables;
using GameKit.Haptics;
using THEBADDEST.SoundSystem;
using GameKit.Architecture;

namespace GameKit.UI.Header
{
    /// <summary>
    /// Self-contained coin balance widget. Shows the soft-currency value, snaps or counts-up on
    /// change, plays a reward pulse, and (optionally) opens the shop when tapped. It depends only on
    /// ScriptableObject data/events — never on other widgets — so it can live on any screen alone.
    /// All visual/FX/interaction references are optional: wire only what a given screen needs.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoinsWidget : MonoBehaviour, IAnimatableHeaderWidget
    {
        [Header("Data")]
        [Tooltip("The soft-currency balance this widget displays.")]
        [SerializeField] private DBInt softCurrency;
        [Tooltip("Raised when the balance changes and the counter should snap to the new value.")]
        [SerializeField] private GameEvent balanceChangedEvent;
        [Tooltip("Raised when the balance changes and the counter should count up smoothly.")]
        [SerializeField] private GameEvent balanceChangedAnimatedEvent;

        [Header("View")]
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private float countUpDuration = 0.75f;

        [Header("Gain animation (optional)")]
        [Tooltip("Root scaled during the gain pulse (e.g. the whole coin section).")]
        [SerializeField] private RectTransform pulseRoot;
        [Tooltip("Coin icon given a secondary pulse.")]
        [SerializeField] private Transform iconPulse;

        [Header("Hit FX (optional)")]
        [SerializeField] private PoolManagerSO poolManager;
        [SerializeField] private string hitParticlePoolId = "CoinHitParticles";
        [SerializeField] private HapticHandlerSO haptics;

        [Header("Tap to open shop (optional)")]
        [SerializeField] private Button button;
        [SerializeField] private GameEvent openShopEvent;
        [Tooltip("Optional guard: when true the tap is ignored (shop already open).")]
        [SerializeField] private Bool shopOpenedFlag;
        [SerializeField] private SoundService soundService;

        private Tween _pulseTween;
        private Tween _iconTween;

        private void OnEnable()
        {
            if (balanceChangedEvent != null) balanceChangedEvent.Subscribe(Refresh);
            if (balanceChangedAnimatedEvent != null) balanceChangedAnimatedEvent.Subscribe(PlayCountUp);
            HeaderSignals.CoinsGained += PlayGainAnimation;
            if (button != null) button.onClick.AddListener(OnTapped);

            Refresh();
        }

        private void OnDisable()
        {
            if (balanceChangedEvent != null) balanceChangedEvent.UnSubscribe(Refresh);
            if (balanceChangedAnimatedEvent != null) balanceChangedAnimatedEvent.UnSubscribe(PlayCountUp);
            HeaderSignals.CoinsGained -= PlayGainAnimation;
            if (button != null) button.onClick.RemoveListener(OnTapped);

            _pulseTween?.Kill();
            _iconTween?.Kill();
        }

        /// <inheritdoc/>
        public void Refresh()
        {
            if (balanceText != null && softCurrency != null)
                balanceText.text = softCurrency.GetValue().ToString();
        }

        /// <inheritdoc/>
        public void PlayGainAnimation(HeaderAnimParams animParams)
        {
            HeaderAnimParams p = animParams.OrDefault();

            PlayCountUp();
            Pulse(ref _pulseTween, pulseRoot, p.ScaleTo, p.Duration, p.Delay, p.PulseCount);
            Pulse(ref _iconTween, iconPulse, 1.1f, p.Duration, p.Delay + 0.1f, p.PulseCount);
            PlayHitParticles();
        }

        private void PlayCountUp()
        {
            if (balanceText == null || softCurrency == null) return;
            int from = int.TryParse(balanceText.text, out int v) ? v : 0;
            UIValueAnimator.AnimateIntValue(this, balanceText, from, softCurrency.GetValue(), countUpDuration);
        }

        private static void Pulse(ref Tween tween, Transform target, float scaleTo, float duration, float delay, int pulseCount)
        {
            if (target == null) return;
            tween?.Kill();
            tween = target.DOScale(scaleTo, duration)
                .SetEase(Ease.InOutSine)
                .SetDelay(delay)
                .SetLoops(pulseCount * 2, LoopType.Yoyo)
                .OnComplete(() => target.DOScale(1f, 0.1f).SetEase(Ease.OutBack));
        }

        private void PlayHitParticles()
        {
            if (poolManager == null || pulseRoot == null) return;

            GameObject fx = poolManager.Get(hitParticlePoolId);
            if (fx == null) return;

            RectTransform fxRect = fx.GetComponent<RectTransform>();
            if (fxRect != null)
            {
                fxRect.SetParent(pulseRoot, false);
                fxRect.anchoredPosition = Vector2.zero;
                fxRect.localScale = new Vector3(40, 40, 40);
            }

            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }

            poolManager.ReleaseAfterDelay(hitParticlePoolId, fx, 2f);
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Medium);
        }

        private void OnTapped()
        {
            if (openShopEvent == null) return;
            if (shopOpenedFlag != null && shopOpenedFlag.GetValue()) return;

            if (soundService != null) soundService.Play("Click");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Selection);
            openShopEvent.Invoke();
        }
    }
}
