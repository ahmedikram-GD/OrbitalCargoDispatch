using GameKit.Architecture.Events;
using GameKit.StateMachine;
using GameKit.StateMachine.Application;
using GameKit.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalCargo.Flow
{
    /// <summary>
    /// The only thing that drives the FSM. Maps flow events to transitions: pause stacks the pause
    /// panel over the running shift (FullScreenPlacement: the shift is paused, not exited), resume
    /// pops it, shift end stacks the summary the same way, and "next shift" clears the stack and enters
    /// a fresh shift (Game: ClearAll). Back (Escape / Android back) toggles pause during a shift and is
    /// ignored on the summary, so it can never resume a shift that has already ended.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Flow/Game Flow")]
    public sealed class GameFlow : BaseApplicationFlowController<Transition>
    {
        [Header("Transitions")]
        [SerializeField] private Transition toPause;
        [SerializeField] private Transition toSummary;
        [SerializeField] private Transition toShift;

        [Header("Flow events")]
        [SerializeField] private GameEvent onPausePressed;
        [SerializeField] private GameEvent onResumePressed;
        [SerializeField] private GameEvent onShiftEnded;
        [SerializeField] private GameEvent onNextShiftPressed;
        [Tooltip("e_BackBtnPressed — the same asset as the base class's back event; Escape raises it.")]
        [SerializeField] private GameEvent onBackPressed;

        private enum Screen { None, Shift, Pause, Summary }
        private Screen _screen = Screen.None;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Update()
        {
            // Escape is the Android back button under the Input System, and a handy pause key in the Editor.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && onBackPressed != null)
                onBackPressed.Invoke();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>The FSM booted straight into the shift (its boot state), so flow starts there.</summary>
        public void MarkShiftRunning() => _screen = Screen.Shift;

        // ── BaseApplicationFlowController ──────────────────────────────────────

        protected override void RegisterFlowEvents()
        {
            if (onPausePressed != null) onPausePressed.Subscribe(HandlePausePressed);
            if (onResumePressed != null) onResumePressed.Subscribe(HandleResumePressed);
            if (onShiftEnded != null) onShiftEnded.Subscribe(HandleShiftEnded);
            if (onNextShiftPressed != null) onNextShiftPressed.Subscribe(HandleNextShiftPressed);
        }

        protected override void UnregisterFlowEvents()
        {
            if (onPausePressed != null) onPausePressed.UnSubscribe(HandlePausePressed);
            if (onResumePressed != null) onResumePressed.UnSubscribe(HandleResumePressed);
            if (onShiftEnded != null) onShiftEnded.UnSubscribe(HandleShiftEnded);
            if (onNextShiftPressed != null) onNextShiftPressed.UnSubscribe(HandleNextShiftPressed);
        }

        protected override void OnBackButtonPressed()
        {
            if (_screen == Screen.Shift) HandlePausePressed();
            else if (_screen == Screen.Pause) HandleResumePressed();
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandlePausePressed()
        {
            if (_screen != Screen.Shift) return;
            _screen = Screen.Pause;
            GoTo(toPause, UICloseReasons.FullScreenPlacement);
        }

        private void HandleResumePressed()
        {
            if (_screen != Screen.Pause) return;
            _screen = Screen.Shift;
            StartCoroutine(finiteStateMachine.PopPausedState());
        }

        private void HandleShiftEnded()
        {
            _screen = Screen.Summary;
            GoTo(toSummary, UICloseReasons.FullScreenPlacement);
        }

        private void HandleNextShiftPressed()
        {
            if (_screen != Screen.Summary) return;
            _screen = Screen.Shift;
            GoTo(toShift, UICloseReasons.Game);
        }
    }
}
