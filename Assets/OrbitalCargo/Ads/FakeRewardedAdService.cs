using System;
using System.Collections;
using UnityEngine;

namespace OrbitalCargo.Ads
{
    /// <summary>
    /// Stand-in rewarded-ad provider until a real ads SDK is integrated. "Plays" an ad by waiting a few
    /// real-time seconds (optionally showing an overlay so it's obvious in a build), then reports the
    /// result picked in the Inspector — so the success AND failure paths can both be tested.
    ///
    /// Swap for a real SDK by adding a component that implements <see cref="IRewardedAdService"/> and
    /// pointing the callers' Ad Service Source at it.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Ads/Fake Rewarded Ad Service")]
    public class FakeRewardedAdService : MonoBehaviour, IRewardedAdService
    {
        [Tooltip("Real-time seconds the fake ad 'plays' for. Unaffected by slow-mo / timeScale.")]
        [SerializeField, Min(0f)] private float _fakeDuration = 1.5f;

        [Tooltip("What every fake ad reports. Set to Skipped or Failed to test those paths.")]
        [SerializeField] private RewardedAdResult _simulatedResult = RewardedAdResult.Completed;

        [Tooltip("Untick to simulate no ad being available (callers then show 'Ad not available').")]
        [SerializeField] private bool _isReady = true;

        [Tooltip("Optional full-screen 'FAKE AD' object shown while the ad plays.")]
        [SerializeField] private GameObject _overlay;

        private Action<RewardedAdResult> _pending;
        private Coroutine _playing;

        public bool IsReady => _isReady && enabled && gameObject.activeInHierarchy;

        private void Awake()
        {
            if (_overlay != null) _overlay.SetActive(false);
        }

        public void Show(string placement, Action<RewardedAdResult> onFinished)
        {
            if (_pending != null || !IsReady)
            {
                Debug.Log($"[FakeAd] '{placement}' refused ({(_pending != null ? "an ad is already playing" : "not ready")}).", this);
                onFinished?.Invoke(RewardedAdResult.Failed);
                return;
            }

            Debug.Log($"[FakeAd] Showing '{placement}' for {_fakeDuration:0.##}s → {_simulatedResult}.", this);
            _pending = onFinished;
            if (_overlay != null) _overlay.SetActive(true);
            _playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            if (_fakeDuration > 0f) yield return new WaitForSecondsRealtime(_fakeDuration);
            _playing = null;
            Finish(_simulatedResult);
        }

        private void OnDisable()
        {
            if (_playing != null) { StopCoroutine(_playing); _playing = null; }
            // Never leave a caller waiting forever (its buttons would stay disabled).
            if (_pending != null) Finish(RewardedAdResult.Failed);
        }

        private void Finish(RewardedAdResult result)
        {
            if (_overlay != null) _overlay.SetActive(false);
            var callback = _pending;
            _pending = null;
            callback?.Invoke(result);
        }
    }
}
