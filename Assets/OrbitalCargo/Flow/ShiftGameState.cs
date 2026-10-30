using System.Collections;
using GameKit.AppFlow;
using GameKit.Architecture.Events;
using GameKit.StateMachine;
using OrbitalCargo.Shift;
using OrbitalCargo.UI;
using UnityEngine;

namespace OrbitalCargo.Flow
{
    /// <summary>
    /// The running shift. Gameplay lives in the scene, so the kit <see cref="GameState"/> only spawns
    /// the pooled ShiftHud and raises e_ShiftGameEnter (which starts <c>ShiftController</c>). This
    /// subclass is the HUD's brain: it binds the pause button to e_PausePressed and sets the shift label.
    /// </summary>
    [CreateAssetMenu(fileName = "ShiftGameState", menuName = "OrbitalCargo/Flow/Shift Game State")]
    public class ShiftGameState : GameState
    {
        [Tooltip("e_PausePressed — GameFlow stacks the pause panel.")]
        [SerializeField] private GameEvent onPausePressed;
        [Tooltip("ShiftSession — shift number for the HUD label.")]
        [SerializeField] private ShiftSessionSO session;

        private HudView _hud;

        public override IEnumerator Enter(IState previous)
        {
            yield return base.Enter(previous);
            Bind(gameHudInstance as HudView);
        }

        public override IEnumerator Exit()
        {
            Unbind();
            yield return base.Exit();
        }

        public override IEnumerator ExitImmediate()
        {
            Unbind();
            yield return base.ExitImmediate();
        }

        // ── Binding ────────────────────────────────────────────────────────────

        private void Bind(HudView hud)
        {
            Unbind();
            _hud = hud;
            if (_hud == null)
            {
                Debug.LogError("[ShiftGameState] ShiftHud prefab has no HudView.");
                return;
            }
            _hud.SetShiftNumber(session != null ? session.ShiftNumber : 1);
            if (_hud.PauseButton != null) _hud.PauseButton.onClick.AddListener(OnPauseClicked);
        }

        private void Unbind()
        {
            if (_hud != null && _hud.PauseButton != null) _hud.PauseButton.onClick.RemoveListener(OnPauseClicked);
            _hud = null;
        }

        private void OnPauseClicked()
        {
            if (onPausePressed != null) onPausePressed.Invoke();
        }
    }
}
