using UnityEngine;
using System.Collections;

namespace GameKit.StateMachine
{
    public class State : ScriptableObject
    {
        protected IState Listener;
        public bool PausePreviousState = false;
        public bool ShowBanner = false;

        // Enter method which accepts an IState listener, which is nothing but FSM refernce, but in a controlled manner
        // Which means you can only access IState Transition method not all FSM prperties!  
        // We pass the FiniteStateMachine instance (which implements IState) to allow the state to call TransitionTo
        public virtual IEnumerator Enter(IState listener)
        {
            Listener = listener;
            yield return null;
        }
        public virtual IEnumerator Pause()
        {
            yield return null;
        }
        public virtual IEnumerator Resume()
        {
            yield return null;
        }
        public virtual IEnumerator Exit()
        {
            yield return null;
        }

        /// <summary>
        /// Instant teardown for BULK clears (ClosePolicy.ClearAll / multi-pop), where the state is
        /// occluded and never seen leaving. It must close in one go with NO exit animation and no await,
        /// so clearing N states costs ~0 time instead of summing N exit animations. Only the single
        /// visible top state and the incoming destination should animate. Defaults to the normal Exit();
        /// UI states override this to hide instantly.
        /// </summary>
        public virtual IEnumerator ExitImmediate() => Exit();
    }
}
