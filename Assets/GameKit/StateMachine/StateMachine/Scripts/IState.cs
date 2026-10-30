using System.Collections;

namespace GameKit.StateMachine
{
    public interface IState
    {
        void TransitionTo(Transition transition, bool pauseCurrent = false);
        IEnumerator ClearPausedStates();
        IEnumerator ReloadCurrentState();
    }
}