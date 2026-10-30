using System;

namespace OrbitalCargo.Ads
{
    /// <summary>How a rewarded ad ended. Only <see cref="Completed"/> earns the reward.</summary>
    public enum RewardedAdResult
    {
        Completed,
        Skipped,
        Failed,
    }

    /// <summary>
    /// A rewarded-video provider. Gameplay talks to this, never to an ads SDK, so the fake used today
    /// (<see cref="FakeRewardedAdService"/>) and a real SDK later are swapped by changing one component.
    ///
    /// Contract: <c>onFinished</c> is invoked exactly once per <see cref="Show"/>. Callers check
    /// <see cref="IsReady"/> first and treat "not ready" as the ad being unavailable.
    /// </summary>
    public interface IRewardedAdService
    {
        /// <summary>True when an ad can be shown right now.</summary>
        bool IsReady { get; }

        /// <summary>Plays an ad for <paramref name="placement"/> (e.g. "unlock_prop") and reports how it ended.</summary>
        void Show(string placement, Action<RewardedAdResult> onFinished);
    }
}
