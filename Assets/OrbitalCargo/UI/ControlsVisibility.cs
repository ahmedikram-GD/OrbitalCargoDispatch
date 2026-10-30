using GameKit.Architecture.Events;
using UnityEngine;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// Hides the touch controls (joystick, look area, context button) while anything pauses the player
    /// — the pause panel and the shift summary — so they neither show through nor eat taps meant for the
    /// panel. Counts nested pauses the same way <c>FirstPersonController</c> does.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Controls Visibility")]
    public sealed class ControlsVisibility : MonoBehaviour
    {
        [Tooltip("CanvasGroup on the Controls canvas root.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("e_GamePaused (bool).")]
        [SerializeField] private GameEventWithBool onGamePaused;

        private int _pauseCount;

        private void OnEnable()
        {
            if (onGamePaused != null) onGamePaused.Subscribe(HandleGamePaused);
        }

        private void OnDisable()
        {
            if (onGamePaused != null) onGamePaused.UnSubscribe(HandleGamePaused);
        }

        private void HandleGamePaused(bool paused)
        {
            _pauseCount = Mathf.Max(0, _pauseCount + (paused ? 1 : -1));
            if (group == null) return;
            bool visible = _pauseCount == 0;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }
    }
}
