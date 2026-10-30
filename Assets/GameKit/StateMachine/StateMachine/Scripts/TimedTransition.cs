using UnityEngine;
using System.Collections;

namespace GameKit.StateMachine
{
    [CreateAssetMenu(fileName = "TimedTransition", menuName = "ProjectCore/State Machine/Transitions/Timed")]
    public class TimedTransition : Transition
    {
        [SerializeField] private float delayInSeconds = 1f;

        public override IEnumerator Execute()
        {
            yield return new WaitForSeconds(delayInSeconds);
        }
    }
}
