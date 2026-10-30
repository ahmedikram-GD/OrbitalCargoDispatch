using UnityEngine;

namespace OrbitalCargo.Ads
{
    /// <summary>
    /// Publishes the scene's ad service into <see cref="RewardedAdAnchorSO"/> while enabled. Kept
    /// separate from <see cref="FakeRewardedAdService"/> so the SJ port stays verbatim and a real SDK
    /// component can be dropped in beside this one later.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Ads/Rewarded Ad Registrar")]
    public sealed class RewardedAdRegistrar : MonoBehaviour
    {
        [Tooltip("Object holding the IRewardedAdService (FakeRewardedAdService). Empty = search this object and its children.")]
        [SerializeField] private GameObject adServiceSource;
        [Tooltip("RewardedAdAnchor asset the summary state reads.")]
        [SerializeField] private RewardedAdAnchorSO anchor;

        private IRewardedAdService _service;

        private void OnEnable()
        {
            _service = RewardedAdResolver.Resolve(adServiceSource, this);
            if (anchor != null && _service != null) anchor.Register(_service);
        }

        private void OnDisable()
        {
            if (anchor != null) anchor.Unregister(_service);
            _service = null;
        }
    }
}
