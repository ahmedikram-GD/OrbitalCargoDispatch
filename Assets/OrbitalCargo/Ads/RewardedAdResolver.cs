using UnityEngine;

namespace OrbitalCargo.Ads
{
    /// <summary>
    /// Finds the <see cref="IRewardedAdService"/> a component should use. Unity can't serialize an
    /// interface field, so components hold a plain GameObject reference and resolve the interface on it
    /// (or, failing that, on themselves and their children).
    /// </summary>
    public static class RewardedAdResolver
    {
        public static IRewardedAdService Resolve(GameObject source, Component owner)
        {
            IRewardedAdService service = null;
            if (source != null) service = source.GetComponentInChildren<IRewardedAdService>(true);
            if (service == null && owner != null) service = owner.GetComponentInChildren<IRewardedAdService>(true);

            if (service == null && owner != null)
                Debug.LogError($"{owner.name}: no IRewardedAdService found — assign the object holding the ad " +
                               "service (e.g. FakeRewardedAdService). Ad buttons will report 'not available'.", owner);
            return service;
        }
    }
}
