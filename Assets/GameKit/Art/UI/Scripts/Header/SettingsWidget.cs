using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using GameKit.Architecture.Events;
using GameKit.Haptics;
using THEBADDEST.SoundSystem;

namespace GameKit.UI.Header
{
    /// <summary>
    /// Self-contained settings button widget. Owns its own button and announces "open settings" via a
    /// <see cref="GameEvent"/> and/or a UnityEvent — it does not know or care what handles the request,
    /// keeping it fully decoupled from the app flow. Drop it on any screen that needs a settings entry.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsWidget : MonoBehaviour, IHeaderWidget
    {
        [Header("Open settings")]
        [SerializeField] private Button button;
        [Tooltip("Raised when settings should open (subscribe your flow/state to this).")]
        [SerializeField] private GameEvent openSettingsEvent;
        [Tooltip("Inspector hook for the same intent (optional).")]
        [SerializeField] private UnityEvent onOpen;

        [Header("Feedback (optional)")]
        [SerializeField] private SoundService soundService;
        [SerializeField] private HapticHandlerSO haptics;

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(Open);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Open);
        }

        /// <inheritdoc/>
        public void Refresh() { /* nothing dynamic to sync */ }

        /// <summary>Request that settings open. Safe to call from code or a button.</summary>
        public void Open()
        {
            if (soundService != null) soundService.Play("Click");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Selection);
            openSettingsEvent?.Invoke();
            onOpen?.Invoke();
        }
    }
}
