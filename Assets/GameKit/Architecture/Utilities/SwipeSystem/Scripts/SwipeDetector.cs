using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameKit.Architecture.SwipeSystem
{
    public class SwipeDetector : MonoBehaviour
    {
        public event Action<SwipeData> OnSwipeDetected;

        [Tooltip("Minimum swipe distance (screen pixels) to consider a swipe")] [SerializeField]
        private float minSwipeDistance = 50f;

        [Tooltip("Maximum swipe duration (seconds) for Single mode")] [SerializeField]
        private float maxSwipeDuration = 0.5f;

        [Tooltip("Minimum time between swipes (seconds) in Continuous mode")] [SerializeField]
        private float minTimeBetweenSwipes = 0.1f;

        [Tooltip("Swipe detection mode: Single (one swipe per drag) or Continuous (multiple swipes per drag)")]
        [SerializeField]
        private SwipeMode swipeMode = SwipeMode.Continuous;

        private Vector2 startPos;
        private float startTime;
        private bool trackingSwipe = false;
        private bool swipeFiredThisDrag = false;
        private Vector2 currentPos;
        private SwipeDirection lastSwipeDirection;

        private InputAction touchPositionAction;
        private InputAction touchPressAction;

        private void Awake()
        {
            touchPositionAction =
                new InputAction(type: InputActionType.Value, binding: "<Touchscreen>/touch0/position");
            touchPressAction = new InputAction(type: InputActionType.Button, binding: "<Touchscreen>/touch0/press");

            touchPressAction.started += OnTouchStarted;
            touchPressAction.canceled += OnTouchCanceled;
            touchPositionAction.performed += OnTouchMoved;

            touchPositionAction.Enable();
            touchPressAction.Enable();

            // Prewarm to avoid garbage collection on first swipe
            Prewarm();
        }

        private void OnDestroy()
        {
            touchPressAction.started -= OnTouchStarted;
            touchPressAction.canceled -= OnTouchCanceled;
            touchPositionAction.performed -= OnTouchMoved;

            touchPressAction.Disable();
            touchPositionAction.Disable();
        }

        private void Prewarm()
        {
            // Simulate initial allocations without invoking the event
            SwipeData dummyData = new SwipeData(SwipeDirection.Up, 0f, 0f, swipeMode);
            // No event invocation here, just pre-allocating resources
        }

        private void OnTouchStarted(InputAction.CallbackContext context)
        {
            startPos = touchPositionAction.ReadValue<Vector2>();
            currentPos = startPos;
            startTime = Time.time;
            trackingSwipe = true;
            swipeFiredThisDrag = false;
        }

        private void OnTouchMoved(InputAction.CallbackContext context)
        {
            if (!trackingSwipe)
                return;

            currentPos = context.ReadValue<Vector2>();
            float duration = Time.time - startTime;
            Vector2 delta = currentPos - startPos;

            bool canFireSwipe = false;
            if (swipeMode == SwipeMode.Single)
            {
                // In Single mode, respect maxSwipeDuration and allow only one swipe per drag
                canFireSwipe = !swipeFiredThisDrag && duration <= maxSwipeDuration &&
                               delta.magnitude >= minSwipeDistance;
            }
            else
            {
                // In Continuous mode, ignore maxSwipeDuration, but enforce minTimeBetweenSwipes
                canFireSwipe = delta.magnitude >= minSwipeDistance &&
                               (!swipeFiredThisDrag || duration >= minTimeBetweenSwipes);
            }

            if (canFireSwipe)
            {
                SwipeDirection direction = CalculateSwipeDirection(delta);

                // In Continuous mode, allow new swipes if direction changes or enough distance is covered
                if (swipeMode == SwipeMode.Continuous && swipeFiredThisDrag &&
                    (direction != lastSwipeDirection || delta.magnitude >= minSwipeDistance * 1.5f))
                {
                    // Reset start position and time for continuous swipe
                    startPos = currentPos;
                    startTime = Time.time;
                    swipeFiredThisDrag = false;
                }

                if (!swipeFiredThisDrag)
                {
                    float intensity = delta.magnitude / Mathf.Max(duration, 0.01f); // Avoid division by zero
                    lastSwipeDirection = direction;
                    swipeFiredThisDrag = swipeMode != SwipeMode.Continuous; // Only block further swipes in Single mode

                    OnSwipeDetected?.Invoke(new SwipeData(direction, intensity, duration, swipeMode));

                    // In Continuous mode, reset for next swipe
                    if (swipeMode == SwipeMode.Continuous)
                    {
                        startPos = currentPos;
                        startTime = Time.time;
                        swipeFiredThisDrag = true;
                    }
                }
            }
        }

        private void OnTouchCanceled(InputAction.CallbackContext context)
        {
            trackingSwipe = false;
            swipeFiredThisDrag = false;
        }

        private SwipeDirection CalculateSwipeDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.y) >= Mathf.Abs(delta.x))
                return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
            else
                return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        }
    }
}