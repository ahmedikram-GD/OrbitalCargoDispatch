namespace GameKit.Architecture.SwipeSystem
{
    public interface ISwipeDetector
    {
        event System.Action<SwipeData> OnSwipeDetected;
        void Enable();
        void Disable();
    }
}
