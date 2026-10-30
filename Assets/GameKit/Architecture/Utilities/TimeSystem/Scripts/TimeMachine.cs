using System;
using UnityEngine;
using GameKit.Architecture.Events;
using System.Collections;

namespace GameKit.Architecture.TheTimeMachine
{
    [CreateAssetMenu(fileName = "TimeMachine", menuName = "ProjectCore/TimeMachine")]
    public class TimeMachine : ScriptableObject
    {
        bool timeMachineStarted = false;
        [SerializeField] private GameEvent _tick;
        private readonly WaitForSeconds _waitForOneSecond = new WaitForSeconds(1);
        public IEnumerator Tick()
        {
            if(timeMachineStarted)
                yield break;
            timeMachineStarted = true;
            while (true)
            {   
                _tick.Invoke();
                yield return _waitForOneSecond;
            }
        }

        private void OnEnable()
        {
            timeMachineStarted = false;
        }
    }
}
