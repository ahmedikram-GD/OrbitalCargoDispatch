using GameKit.Architecture;
namespace GameKit.UI.Header
{
    /// <summary>
    /// A self-contained header element that owns a single concern (coins, lives, settings…).
    /// It binds to its own data sources and keeps itself in sync — no sibling widget and no
    /// central controller are required for it to function. Drop one on any screen in isolation.
    /// </summary>
    public interface IHeaderWidget
    {
        /// <summary>Force an immediate sync of the visuals to the current data.</summary>
        void Refresh();
    }

    /// <summary>
    /// A header widget that can additionally play a short "gain" emphasis animation on demand
    /// (e.g. a coin counter pulsing when a reward lands). Segregated from <see cref="IHeaderWidget"/>
    /// so purely-static widgets (like settings) don't have to implement animation.
    /// </summary>
    public interface IAnimatableHeaderWidget : IHeaderWidget
    {
        /// <summary>Play the gain pulse using the supplied tuning (use <c>default</c> for sensible defaults).</summary>
        void PlayGainAnimation(HeaderAnimParams animParams);
    }
}
