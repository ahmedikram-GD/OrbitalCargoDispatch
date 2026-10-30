using System;

namespace GameKit.UI.Header
{
    /// <summary>
    /// Decoupled, allocation-free signal hub for header gain-animations.
    /// <para>
    /// Gameplay / reward code raises "coins gained" or "lives gained" here; whichever coin or life
    /// widget happens to be on screen reacts by playing its own pulse. Neither side references the
    /// other — callers don't need a header reference, and widgets don't need a central manager.
    /// </para>
    /// Widgets subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>. If no widget is
    /// present the signal is simply a no-op.
    /// </summary>
    public static class HeaderSignals
    {
        /// <summary>Raised when the player gains coins and the coin widget(s) should pulse.</summary>
        public static event Action<HeaderAnimParams> CoinsGained;

        /// <summary>Raised when the player gains lives and the life widget(s) should pulse.</summary>
        public static event Action<HeaderAnimParams> LivesGained;

        public static void RaiseCoinsGained() => CoinsGained?.Invoke(HeaderAnimParams.Default);
        public static void RaiseCoinsGained(HeaderAnimParams animParams) => CoinsGained?.Invoke(animParams.OrDefault());

        public static void RaiseLivesGained() => LivesGained?.Invoke(HeaderAnimParams.Default);
        public static void RaiseLivesGained(HeaderAnimParams animParams) => LivesGained?.Invoke(animParams.OrDefault());
    }
}
