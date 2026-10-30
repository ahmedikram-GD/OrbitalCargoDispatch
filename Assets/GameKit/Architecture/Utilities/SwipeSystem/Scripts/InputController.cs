using System;
using GameKit.Architecture.SwipeSystem;
using UnityEngine;

namespace GameKit.Architecture
{

public class InputController : MonoBehaviour
{
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private KeyboardSwipeSimulator keyboardSwipeSimulator;

    public static Action<SwipeData> SwipeAction;

    private void OnEnable()
    {
        swipeDetector.OnSwipeDetected += CallSwipe;
        keyboardSwipeSimulator.OnSwipeDetected += CallSwipe;
    }

    private void OnDisable()
    {
        swipeDetector.OnSwipeDetected -= CallSwipe;
        keyboardSwipeSimulator.OnSwipeDetected -= CallSwipe;
    }

    private void CallSwipe(SwipeData swipeData)
    {
        SwipeAction?.Invoke(swipeData);
    }
}
}