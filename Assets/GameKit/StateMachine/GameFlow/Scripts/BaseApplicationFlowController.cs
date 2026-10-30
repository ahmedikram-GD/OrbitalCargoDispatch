using System;
using System.Collections;
using UnityEngine;
using GameKit.StateMachine;
using GameKit.UI;
using GameKit.Architecture.Events;

namespace GameKit.StateMachine.Application
{
    public abstract class BaseApplicationFlowController<TTransition> : MonoBehaviour where TTransition : Transition
    {
        [SerializeField] protected FiniteStateMachine finiteStateMachine;

        [Header("Events")]
        [SerializeField] private GameEvent backBtnPressedEvent;

        // Tracks the active HandleTransition coroutine so concurrent GoTo calls
        // don't spawn multiple transitions simultaneously (which orphans HUD instances).
        private Coroutine _activeHandleTransitionCoroutine;

        protected virtual void OnEnable()
        {
            backBtnPressedEvent.Subscribe(OnBackButtonPressed);
            RegisterFlowEvents();
        }

        protected virtual void OnDisable()
        {
            backBtnPressedEvent.UnSubscribe(OnBackButtonPressed);
            UnregisterFlowEvents();
        }

        public virtual void BootFlow(TTransition bootTransition, UICloseReasons reason)
        {
            if (_activeHandleTransitionCoroutine != null)
                StopCoroutine(_activeHandleTransitionCoroutine);
            _activeHandleTransitionCoroutine = StartCoroutine(HandleTransition(bootTransition, GetPolicyForReason(reason), ShouldPauseCurrent(reason), reason));
        }

        protected virtual void GoTo(TTransition transition, UICloseReasons reason)
        {
            // Stop any in-flight HandleTransition before starting a new one.
            // Without this, rapid button presses run concurrent coroutines: both call
            // TransitionTo(), the second cancels the first mid-Enter, leaving orphaned HUD instances.
            print("Go to " + transition.ToState.GetType().Name + "");
            if (_activeHandleTransitionCoroutine != null)
                StopCoroutine(_activeHandleTransitionCoroutine);

            _activeHandleTransitionCoroutine = StartCoroutine(HandleTransition(
                transition,
                GetPolicyForReason(reason),
                ShouldPauseCurrent(reason),
                reason
            ));
        }

        protected virtual IEnumerator HandleTransition(TTransition transition, ClosePolicy defaultPolicy, bool pauseCurrent, UICloseReasons reason)
        {
            var policy = transition.closePolicy != ClosePolicy.Default ? transition.closePolicy : defaultPolicy;

            switch (policy)
            {
                case ClosePolicy.ClearAll:
                    yield return finiteStateMachine.ClearPausedStates();
                    break;
                case ClosePolicy.PopOne:
                    yield return finiteStateMachine.PopPausedState();
                    break;
                case ClosePolicy.PopUntil:
                    yield return finiteStateMachine.JumpTo((StateMachine.State)transition.ToState);
                    _activeHandleTransitionCoroutine = null;
                    yield break;
            }

            // Home means the player is leaving the level entirely, so the outgoing state (e.g. the
            // active GameState) tears down instantly (ExitImmediate) instead of awaiting its normal
            // Exit() — which on GameState awaits gameHudInstance.Hide()'s fade/slide animation, the
            // "delay" between pressing Back and actually landing on the home screen. Scoped to Home
            // specifically (not every ClearAll reason, e.g. Game) so a level-to-level transition keeps
            // its normal exit animation.
            bool exitCurrentImmediately = reason == UICloseReasons.Home;
            finiteStateMachine.TransitionTo(transition, pauseCurrent, exitCurrentImmediately);
            _activeHandleTransitionCoroutine = null;
        }

        protected virtual void OnBackButtonPressed()
        {
            StartCoroutine(finiteStateMachine.PopPausedState());
        }

        protected virtual ClosePolicy GetPolicyForReason(UICloseReasons reason)
        {
            return reason switch
            {
                UICloseReasons.Home => ClosePolicy.ClearAll,
                UICloseReasons.Game => ClosePolicy.ClearAll,
                UICloseReasons.ResumeGame => ClosePolicy.PopOne,
                UICloseReasons.Revive => ClosePolicy.PopOne,
                UICloseReasons.SkipLevel => ClosePolicy.PopOne,
                UICloseReasons.ResumeAny => ClosePolicy.PopUntil,
                _ => ClosePolicy.Default
            };
        }

        protected virtual bool ShouldPauseCurrent(UICloseReasons reason)
        {
            return reason is UICloseReasons.ShowFullScreenPlacement
                        or UICloseReasons.FullScreenPlacement
                        or UICloseReasons.DailyLogin;
        }

        // Override to register specific flow events (MainMenu, Game, etc.)
        protected abstract void RegisterFlowEvents();
        protected abstract void UnregisterFlowEvents();
    }
}
