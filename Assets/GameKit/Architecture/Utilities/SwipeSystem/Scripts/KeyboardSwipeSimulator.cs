using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GameKit.Architecture.SwipeSystem;

namespace GameKit.Architecture
{

public class KeyboardSwipeSimulator : MonoBehaviour
{
    [SerializeField] private SwipeDetector swipeDetector;
    public event Action<SwipeData> OnSwipeDetected;

    [SerializeField] private float simulatedIntensity = 500f;
    [SerializeField] private float simulatedDuration = 0.1f;

    private InputAction upAction;
    private InputAction downAction;
    private InputAction leftAction;
    private InputAction rightAction;

    private void OnEnable()
    {
        upAction = new InputAction("Up", binding: "<Keyboard>/w");
        downAction = new InputAction("Down", binding: "<Keyboard>/s");
        leftAction = new InputAction("Left", binding: "<Keyboard>/a");
        rightAction = new InputAction("Right", binding: "<Keyboard>/d");

        upAction.started += ctx => TriggerSwipe(SwipeDirection.Up);
        downAction.started += ctx => TriggerSwipe(SwipeDirection.Down);
        leftAction.started += ctx => TriggerSwipe(SwipeDirection.Left);
        rightAction.started += ctx => TriggerSwipe(SwipeDirection.Right);

        upAction.Enable();
        downAction.Enable();
        leftAction.Enable();
        rightAction.Enable();
    }

    private void OnDisable()
    {
        upAction.started -= ctx => TriggerSwipe(SwipeDirection.Up);
        downAction.started -= ctx => TriggerSwipe(SwipeDirection.Down);
        leftAction.started -= ctx => TriggerSwipe(SwipeDirection.Left);
        rightAction.started -= ctx => TriggerSwipe(SwipeDirection.Right);

        upAction.Disable();
        downAction.Disable();
        leftAction.Disable();
        rightAction.Disable();
    }

    private void TriggerSwipe(SwipeDirection direction)
    {
        SwipeData simulatedSwipe = new SwipeData(
            direction,
            simulatedIntensity,
            simulatedDuration,
            SwipeMode.Single
        );
        //// print("Swipe Direction  : "+simulatedSwipe.Direction);
        OnSwipeDetected?.Invoke(simulatedSwipe);
    }
    
}
}