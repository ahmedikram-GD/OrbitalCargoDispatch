using UnityEngine;

namespace GameKit.StateMachine.Application
{
    public static class StateRootManager
    {
        public static Transform States { get; private set; }
        public static Transform UIStates { get; private set; }

        public static void Initialize(Transform state , Transform uiStates)
        {
            UIStates = uiStates;
            States = state;
            IsInitialized = true;
        }

        public static bool IsInitialized { get; private set; }

    }
}

