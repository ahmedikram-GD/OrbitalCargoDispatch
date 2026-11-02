using GameKit.Architecture.Events;
using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Thin orchestrator that wires input → look + motor each frame. It owns no movement or look
    /// math itself; it depends only on the <see cref="IMoveInput"/>, <see cref="ILookController"/>
    /// and <see cref="ICharacterMotor"/> abstractions, resolved from sibling components in
    /// <see cref="Awake"/>. Pausing is driven by the kit's <c>e_GamePaused</c> channel (bool) so
    /// the pause menu never needs a reference to the player. Ported from Silly-Justice.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [AddComponentMenu("OrbitalCargo/First Person/First Person Controller")]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Cursor")]
        [Tooltip("Lock and hide the hardware cursor while playing. Off for this game: the HUD is tapped/clicked.")]
        [SerializeField] private bool lockCursor = false;

        [Header("Events")]
        [Tooltip("e_GamePaused (GameEventWithBool). Raised true by the pause state and false on resume; " +
                 "while paused no input is read at all (mouse look isn't time-scaled). Optional.")]
        [SerializeField] private GameEventWithBool onGamePaused;

        private IMoveInput _input;
        private ILookController _look;
        private ICharacterMotor _motor;
        private int _pauseCount;

        /// <summary>True while at least one pause request is outstanding.</summary>
        public bool IsPaused => _pauseCount > 0;

        /// <summary>
        /// While true, look input is ignored (movement still works) — the tutorial's camera nudge owns
        /// the view for a moment and the player's thumb would otherwise fight it.
        /// </summary>
        public bool IsLookSuppressed { get; set; }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _input = GetComponent<IMoveInput>();
            _look = GetComponent<ILookController>();
            _motor = GetComponent<ICharacterMotor>();

            if (_input == null) Debug.LogError($"[FirstPersonController] '{name}': no IMoveInput component found.", this);
            if (_look == null) Debug.LogError($"[FirstPersonController] '{name}': no ILookController component found.", this);
            if (_motor == null) Debug.LogError($"[FirstPersonController] '{name}': no ICharacterMotor component found.", this);
        }

        private void OnEnable()
        {
            if (lockCursor) SetCursorLocked(true);
            if (onGamePaused != null) onGamePaused.Subscribe(HandleGamePaused);
        }

        private void OnDisable()
        {
            if (lockCursor) SetCursorLocked(false);
            if (onGamePaused != null) onGamePaused.UnSubscribe(HandleGamePaused);
            _pauseCount = 0;   // the matching resume can't reach us while disabled — don't wake up stuck
        }

        private void Update()
        {
            if (_input == null || _pauseCount > 0) return;

            // Look runs in Update for the smoothest aim (frame-rate look, not physics step).
            if (!IsLookSuppressed) _look?.Look(_input.Look);

            if (_input.JumpPressed) _motor?.Jump();
            _motor?.Move(_input.Move, _input.SprintHeld, _input.CrouchHeld);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleGamePaused(bool paused) => _pauseCount = Mathf.Max(0, _pauseCount + (paused ? 1 : -1));

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
