using UnityEngine;
using Sirenix.OdinInspector;
using GameKit.Architecture;

namespace GameKit.Architecture.Events
{
    [CreateAssetMenu(fileName = "e_", menuName = "ProjectCore/Events/Game Event - Basic")]
    public class GameEvent : ScriptableObject
    {
        public delegate void EventHandler();
        private event EventHandler _handler;

        [GUIColor(1, 1, 0.5f)]
        [Button(ButtonSizes.Medium)]
        public void Invoke()
        {
            var handler = _handler;
            if (handler == null) return;

            // Invoke each subscriber INDEPENDENTLY. A raw multicast `_handler.Invoke()` stops at the first
            // subscriber that throws, silently dropping every later one — e.g. one LiveOp icon's CheckForVisibility
            // throwing on a home-landing would leave the remaining icons unrefreshed until the next landing.
            // Isolating each handler guarantees all subscribers run; a throwing one is logged, not swallowed silently.
            var list = handler.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try { ((EventHandler)list[i]).Invoke(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameEvent '{name}'] subscriber '{list[i].Method?.DeclaringType?.Name}.{list[i].Method?.Name}' threw: {e}");
                }
            }
        }

        public void Subscribe(EventHandler handler)
        {
            _handler += handler;
        }

        public void UnSubscribe(EventHandler handler)
        {
            _handler -= handler;
        }
        public bool HasListeners()
        {
            return _handler != null;
        }

        private void OnDisable()
        {
            _handler = null; // Clear all handlers when the ScriptableObject is disabled or destroyed to avoid memory leaks
        }
    }
}
