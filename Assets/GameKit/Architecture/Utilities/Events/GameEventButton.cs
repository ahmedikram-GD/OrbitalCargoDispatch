using UnityEngine;
using UnityEngine.UI;

namespace GameKit.Architecture.Events
{
    /// <summary>
    /// Raises a <see cref="GameEvent"/> ScriptableObject when its UI Button is clicked.
    /// Attach to a Button GameObject and assign the event — no manual onClick wiring
    /// required (it self-binds in Awake). <see cref="Raise"/> is also public so it can
    /// be hooked to any UnityEvent.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GameEventButton : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;

        private void Awake()
        {
            var button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(Raise);
        }

        public void Raise()
        {
            gameEvent?.Invoke();
        }
    }
}
