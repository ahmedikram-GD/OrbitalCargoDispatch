using GameKit.Architecture.Variables;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using THEBADDEST.Coroutines;
using UnityEngine;

namespace GameKit.StateMachine
{
    // ScriptableObject-based FSM controller. Manages entering, exiting, pausing, and resuming states.
    [CreateAssetMenu(fileName = "FiniteStateMachine", menuName = "ProjectCore/State Machine/Basic FSM")]
    public class FiniteStateMachine : SerializedScriptableObject, IState
    {
        // State that the FSM boots into when first initialized //
        [SerializeField] private State BootState;

        // Currently active state (top of the stack)
        [SerializeField] private State CurrentState;

        // Stack of paused states (e.g., for pop-up overlays, full-screen interruptions)
        [SerializeField] private Stack<State> PausedStates = new();

        // Lookup used to quickly check if a state is paused (for safety)
        private readonly HashSet<State> _pausedStateLookup = new();

        // Tracks sort order for UI layering of state views
        [SerializeField] private Int currentStateSortingOrder;
        public int CurrentStateSortingOrder
        {
            get => currentStateSortingOrder.GetValue();
        }

        // The order the bottom-most (unpaused) state's view renders at. Every state stacked on top
        // of it adds one SortingStep. 10 matches the Game HUD's documented root canvas order.
        [SerializeField] private int baseSortingOrder = 10;
        private const int SortingStep = 10;

        /// <summary>
        /// Recomputes the sorting cursor as a pure function of how deep the paused stack is.
        ///
        /// <para>
        /// This replaces the blind Increment(10)/Decrement(10) the push and pop sites used to do.
        /// Those leaked: both <see cref="TransitionTo"/> and the flow controller's GoTo stop the
        /// in-flight transition coroutine when a second press arrives, so a cancellation landing
        /// between the arithmetic and the matching Push/Pop stranded the offset permanently. The
        /// Int is a ScriptableObject that is not reset on play, so in the Editor every stranded
        /// step was written back into the asset and the next session started higher still — the
        /// cursor had reached 110 against an authored base of 10. That is what put the Game HUD
        /// (which reads this same variable) above panels opened over it, and what made every
        /// hand-picked absolute sorting order in the project a moving target.
        /// </para>
        ///
        /// <para>
        /// Derived rather than accumulated, it cannot drift: a cancelled coroutine leaves the stack
        /// and the cursor agreeing, and the next push or pop re-derives it from scratch.
        /// </para>
        /// </summary>
        private void SyncSortingOrder()
        {
            if (currentStateSortingOrder == null) return;

            currentStateSortingOrder.SetValue(baseSortingOrder + SortingStep * PausedStates.Count);
        }

        // Handle for an active coroutine so we can cancel overlapping transitions
        private Coroutine _transitionCoroutine;

        // Events for tracking state lifecycle externally (used by observers like UI managers)
        public event System.Action<State> OnStateEntered;
        public event System.Action<State> OnStateExited;
        public event System.Action<State> OnStatePaused;
        public event System.Action<State> OnStateResumed;

        // Called to boot the FSM and enter the initial state
        public IEnumerator Init()
        {
            if (BootState == null)
            {
                Debug.LogWarning("Please assign a boot state in FiniteStateMachine to start the game!");
                yield break;
            }

            CurrentState = BootState;

            // Cleared BEFORE the boot state enters, not after: its view reads the sorting cursor in
            // Show(), so a stack (and cursor) left over from a previous run would have decided the
            // very first screen's layer.
            PausedStates.Clear();
            _pausedStateLookup.Clear();
            SyncSortingOrder();

            yield return CurrentState.Enter(this);
            OnStateEntered?.Invoke(CurrentState);
        }

        // Begin a transition to a new state (optionally pause current). Matches IState.TransitionTo's
        // exact signature — states that only hold an IState reference (see State.Enter) call through
        // here, so this overload can't gain a new parameter without breaking that interface match.
        public void TransitionTo(Transition transition, bool pauseCurrent = false)
            => TransitionTo(transition, pauseCurrent, exitCurrentImmediately: false);

        // exitCurrentImmediately: skips the outgoing state's animated Exit() in favour of
        // ExitImmediate() — used for reasons like Home, where the destination must not be held
        // back by an outgoing hide/fade animation (see BaseApplicationFlowController.HandleTransition).
        public void TransitionTo(Transition transition, bool pauseCurrent, bool exitCurrentImmediately)
        {
            if (transition == null || transition.ToState == null)
            {
                Debug.LogWarning("Invalid transition or target state.");
                return;
            }

            // Cancel existing transition if active
            if (_transitionCoroutine != null)
                CoroutineHandler.StopStaticCoroutine(_transitionCoroutine);

            // Start new transition coroutine
            _transitionCoroutine = CoroutineHandler.StartStaticCoroutine(DoTransition(transition, pauseCurrent, exitCurrentImmediately));
        }

        // Core transition coroutine
        private IEnumerator DoTransition(Transition transition, bool pauseCurrent, bool exitCurrentImmediately = false)
        {
            var nextState = transition.ToState;

            // If transitioning to the same state, skip
            if (CurrentState == nextState)
            {
                Debug.Log($"Already in state {nextState.name}, skipping redundant transition.");
                yield break;
            }

            // Prevent transitioning to a paused state; must resume manually
            if (_pausedStateLookup.Contains(nextState))
            {
                Debug.LogWarning($"State '{nextState.name}' is paused. Use ResumePausedState or JumpTo instead.");
                yield break;
            }

            // Handle pause or full exit based on intent
            if (pauseCurrent || nextState.PausePreviousState)
            {
                yield return PauseCurrentState();
            }
            else
            {
                yield return ExitCurrentState(exitCurrentImmediately);
            }

            yield return transition.Execute(); // Optional logic in the transition itself
            yield return nextState.Enter(this); // Enter the new state

            CurrentState = nextState;
            OnStateEntered?.Invoke(CurrentState);
        }

        // Reload the current state (exit → enter again)
        public IEnumerator ReloadCurrentState()
        {
            Debug.Log($"Reloading state: {CurrentState.name}");

            OnStateExited?.Invoke(CurrentState);
            yield return CurrentState.Exit();

            OnStateEntered?.Invoke(CurrentState);
            yield return CurrentState.Enter(this);
        }

        // Clears the entire paused state stack (used for ClosePolicy.ClearAll).
        // Paused states are occluded behind the current state, so they're torn down INSTANTLY
        // (ExitImmediate — no exit animation, no await). This closes any number of stacked states
        // in one go instead of paying each state's exit animation sequentially.
        public IEnumerator ClearPausedStates()
        {
            while (PausedStates.Count > 0)
            {
                var paused = PausedStates.Pop();
                _pausedStateLookup.Remove(paused);
                SyncSortingOrder();
                yield return paused.ExitImmediate();
                OnStateExited?.Invoke(paused);
            }
        }

        // Pops only the most recent paused state and resumes it (ClosePolicy.PopOne)
        public IEnumerator PopPausedState()
        {
            if (PausedStates.Count == 0)
            {
                Debug.LogWarning("No paused states to pop.");
                yield break;
            }

            var topState = PausedStates.Peek();

            yield return ExitCurrentState();      // Exit current
            yield return ResumePausedState(topState); // Resume previous

            CurrentState = topState;
        }

        // Jumps to a specific state in the paused stack (ClosePolicy.PopUntil)
        public IEnumerator JumpTo(State target)
        {
            if (_pausedStateLookup.Contains(target))
            {
                // Pop states until target is on top
                while (PausedStates.Peek() != target)
                {
                    var popped = PausedStates.Pop();
                    _pausedStateLookup.Remove(popped);
                    SyncSortingOrder();
                    yield return popped.ExitImmediate(); // occluded states torn down instantly, in one go
                    OnStateExited?.Invoke(popped);
                }

                // Replace current and resume
                yield return ExitCurrentState();
                yield return ResumePausedState(target);
                CurrentState = target;
            }
            else
            {
                // If target wasn't paused, clear everything and start fresh
                yield return ExitCurrentState();
                yield return ClearPausedStates();

                CurrentState = target;
                yield return CurrentState.Enter(this);
                OnStateEntered?.Invoke(CurrentState);
            }
        }

        // Pauses the current state and pushes it to the paused stack
        private IEnumerator PauseCurrentState()
        {
            // REFUSES the push rather than just complaining about it, and does so in players too.
            //
            // This fires when a previous transition pushed CurrentState and then died before
            // reaching `CurrentState = nextState` — an exception anywhere inside nextState.Enter
            // does it, and a UIViewState whose prefab carries a view of the wrong type is the
            // everyday way to get one (OnBindView casts). The state is then both CurrentState and
            // paused, and the next transition arrives here.
            //
            // It used to log and push anyway, editor-only. That put the same state on the stack
            // twice while _pausedStateLookup (a set) held one entry, so the first pop resumed it
            // and lost track of the duplicate — the stack never drained and the panel could not be
            // closed. In a player build the log was compiled out, so it corrupted silently.
            //
            // Returning here is correct, not a papering-over: the state IS already paused, which is
            // exactly what the caller wanted, so DoTransition should carry on and enter the next
            // state. The warning still names the real bug, which is upstream.
            if (PausedStates.Contains(CurrentState))
            {
                Debug.LogWarning($"[FSM] '{CurrentState.name}' is already paused — skipping the duplicate push. " +
                                 "A previous transition almost certainly threw before it could complete; " +
                                 "look for an exception raised from that state's Enter().");
                yield break;
            }

            // Push and sync FIRST, then pause. The stack and the cursor move together in one
            // uninterruptible step, so a transition cancelled during Pause() cannot leave them
            // disagreeing. Nothing inside Pause() reads either.
            PausedStates.Push(CurrentState);
            _pausedStateLookup.Add(CurrentState);
            SyncSortingOrder();

            yield return CurrentState.Pause();
            OnStatePaused?.Invoke(CurrentState);
        }

        // Fully exits the current state. immediate = true skips the animated Exit() (e.g. GameState
        // waiting on gameHudInstance.Hide()) in favour of ExitImmediate() — no yields, no animation.
        private IEnumerator ExitCurrentState(bool immediate = false)
        {
            if (CurrentState != null)
            {
                yield return immediate ? CurrentState.ExitImmediate() : CurrentState.Exit();
                OnStateExited?.Invoke(CurrentState);
            }
        }

        // Resumes a paused state and pops it from the paused stack
        private IEnumerator ResumePausedState(State target)
        {
            if (PausedStates.Count == 0)
            {
                Debug.LogWarning($"[FSM] Cannot resume '{target.name}' — paused stack is empty.");
                yield break;
            }
            if (PausedStates.Peek() != target)
            {
                Debug.LogWarning($"Trying to resume non-top state '{target.name}'. This is not allowed.");
                yield break;
            }

            // Pop and sync BEFORE Resume(), mirroring PauseCurrentState: the resuming view may
            // re-read the cursor on the way back up (GameHud.Show does), so it has to already be
            // back down to this state's own level.
            PausedStates.Pop();
            _pausedStateLookup.Remove(target);
            SyncSortingOrder();

            yield return target.Resume();
            OnStateResumed?.Invoke(target);
        }
    }
}
