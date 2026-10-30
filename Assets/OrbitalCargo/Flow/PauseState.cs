using System.Collections;
using GameKit.Architecture.Events;
using GameKit.StateMachine;
using GameKit.UI;
using OrbitalCargo.UI;
using UnityEngine;

namespace OrbitalCargo.Flow
{
    /// <summary>
    /// Pause panel over the running shift. Freezes the world (<c>Time.timeScale = 0</c>: shift clock,
    /// belt, cryo timers and scaled tweens) and raises e_GamePaused so the player and the touch
    /// controls stand down. Resume raises e_ResumePressed; GameFlow pops this state.
    /// </summary>
    [CreateAssetMenu(fileName = "PauseState", menuName = "OrbitalCargo/Flow/Pause State")]
    public class PauseState : UIViewState
    {
        [SerializeField] private GameEvent onResumePressed;
        [Tooltip("e_GamePaused (bool) — FirstPersonController and the Controls canvas listen.")]
        [SerializeField] private GameEventWithBool onGamePaused;

        private PauseView _view;
        private bool _frozen;

        public override IEnumerator Enter(IState previous)
        {
            Freeze(true);
            yield return base.Enter(previous);
        }

        public override IEnumerator Exit()
        {
            yield return base.Exit();
            Freeze(false);
        }

        public override IEnumerator ExitImmediate()
        {
            yield return base.ExitImmediate();
            Freeze(false);
        }

        protected override void OnBindView(UIBase view)
        {
            _view = view as PauseView;
            if (_view != null && _view.ResumeButton != null) _view.ResumeButton.onClick.AddListener(OnResumeClicked);
        }

        protected override void OnUnbindView(UIBase view)
        {
            if (_view != null && _view.ResumeButton != null) _view.ResumeButton.onClick.RemoveListener(OnResumeClicked);
            _view = null;
        }

        private void OnResumeClicked()
        {
            if (onResumePressed != null) onResumePressed.Invoke();
        }

        // Unfreeze only once per freeze so Enter/Exit stay paired for FirstPersonController's pause
        // counter. Enter always applies: the flag lives on an asset and can be stale from a play session
        // that was stopped while paused.
        private void Freeze(bool frozen)
        {
            if (!frozen && !_frozen) return;
            _frozen = frozen;
            Time.timeScale = frozen ? 0f : 1f;
            if (onGamePaused != null) onGamePaused.Raise(frozen);
        }
    }
}
