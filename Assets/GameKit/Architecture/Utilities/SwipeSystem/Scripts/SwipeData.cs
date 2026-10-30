namespace GameKit.Architecture.SwipeSystem
{
    public enum SwipeMode
    {
        Single,
        Continuous
    }
    public struct SwipeData
    {
        public SwipeDirection Direction;
        public float Intensity; // e.g. swipe distance / duration or velocity
        public float Duration;    // time in seconds the swipe lasted

        public SwipeData(SwipeDirection dir, float intensity, float duration, SwipeMode mode)
        {
            Direction = dir;
            Intensity = intensity;
            Duration = duration;
        }
    }
}
