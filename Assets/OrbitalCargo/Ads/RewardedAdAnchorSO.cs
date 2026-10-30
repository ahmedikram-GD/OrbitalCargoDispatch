using System;
using UnityEngine;

namespace OrbitalCargo.Ads
{
    /// <summary>
    /// Runtime handle to the scene's <see cref="IRewardedAdService"/> for code that lives in assets
    /// (the summary FSM state is a ScriptableObject and can't hold a scene reference). The scene's
    /// <see cref="RewardedAdRegistrar"/> fills it while enabled; readers treat null as "no ad available".
    /// </summary>
    [CreateAssetMenu(fileName = "RewardedAdAnchor", menuName = "OrbitalCargo/Ads/Rewarded Ad Anchor")]
    public class RewardedAdAnchorSO : ScriptableObject
    {
        [NonSerialized] private IRewardedAdService _service;

        public IRewardedAdService Service => _service;
        public bool IsReady => _service != null && _service.IsReady;

        public void Register(IRewardedAdService service) => _service = service;

        public void Unregister(IRewardedAdService service)
        {
            if (_service == service) _service = null;
        }
    }
}
