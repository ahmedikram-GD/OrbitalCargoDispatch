using System;

namespace GameKit.UI.Header
{
    /// <summary>
    /// Tuning for a header widget's short "gain"/emphasis animation (the scale pulse a coin or
    /// life counter plays when the player earns some). Passed by value so callers never share state.
    /// </summary>
    [Serializable]
    public struct HeaderAnimParams
    {
        /// <summary>How many in/out scale pulses to play.</summary>
        public int PulseCount;
        /// <summary>Peak scale multiplier of a pulse (1 = no scaling).</summary>
        public float ScaleTo;
        /// <summary>Duration of a single pulse half, in seconds.</summary>
        public float Duration;
        /// <summary>Delay before the pulse starts, in seconds.</summary>
        public float Delay;

        public HeaderAnimParams(int pulseCount, float scaleTo, float duration, float delay)
        {
            PulseCount = pulseCount;
            ScaleTo = scaleTo;
            Duration = duration;
            Delay = delay;
        }

        /// <summary>Sensible default pulse: 3 bounces, 1.2x, snappy.</summary>
        public static HeaderAnimParams Default => new HeaderAnimParams(3, 1.2f, 0.15f, 0f);

        /// <summary>True when the struct is its zero value (e.g. came from <c>default</c>).</summary>
        public bool IsUnset => PulseCount == 0 && ScaleTo == 0f && Duration == 0f && Delay == 0f;

        /// <summary>Returns this when it carries values, otherwise <see cref="Default"/>.</summary>
        public HeaderAnimParams OrDefault() => IsUnset ? Default : this;
    }
}
