using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// <see cref="ICharacterMotor"/> implemented on top of Unity's <see cref="CharacterController"/>.
    /// Turns a planar move request + jump into body motion, with acceleration, gravity, coyote-time
    /// jumping and crouch. All tuning comes from <see cref="MovementConfigSO"/>; the only runtime
    /// knob is <see cref="SpeedMultiplier"/>, which the crate carrier lowers while a volatile crate
    /// is held. Ported from Silly-Justice.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("OrbitalCargo/First Person/Character Motor")]
    public sealed class CharacterMotor : MonoBehaviour, ICharacterMotor
    {
        [Tooltip("Movement tuning asset (FP_MovementConfig). Required.")]
        [SerializeField] private MovementConfigSO config;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _timeSinceGrounded;
        private bool _jumpQueued;

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;

        public float SpeedMultiplier { get; set; } = 1f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (config == null)
            {
                Debug.LogError($"[CharacterMotor] '{name}' has no MovementConfigSO assigned.", this);
                return;
            }

            // Convention: transform origin sits at the feet, so center = height/2.
            _controller.height = config.StandHeight;
            _controller.center = new Vector3(_controller.center.x, config.StandHeight * 0.5f, _controller.center.z);
        }

        // ── ICharacterMotor ────────────────────────────────────────────────────

        public void Jump()
        {
            // Latched; consumed in the next Move() step so ordering with input is deterministic.
            _jumpQueued = true;
        }

        public void Move(Vector2 moveInput, bool sprint, bool crouch)
        {
            if (config == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateStance(crouch, dt);

            bool grounded = _controller.isGrounded;
            if (grounded) _timeSinceGrounded = 0f;
            else _timeSinceGrounded += dt;

            // --- Horizontal ---
            float targetSpeed = (crouch ? config.CrouchSpeed
                              : sprint ? config.SprintSpeed
                              : config.WalkSpeed) * Mathf.Max(0f, SpeedMultiplier);

            // Move input is body-local; convert to world using the body's yaw.
            Vector3 wishDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            Vector3 targetVelocity = wishDir * targetSpeed;
            float accel = grounded ? config.GroundAcceleration : config.AirAcceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, accel * Mathf.Max(targetSpeed, 0.01f) * dt);

            // --- Vertical ---
            bool canCoyoteJump = _timeSinceGrounded <= config.CoyoteTime;
            if (_jumpQueued && canCoyoteJump)
            {
                _verticalVelocity = Mathf.Sqrt(2f * config.Gravity * config.JumpHeight);
                _timeSinceGrounded = config.CoyoteTime + 1f; // consume coyote window
            }
            else if (grounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -config.GroundedStickForce; // keep glued to ground
            }
            else
            {
                _verticalVelocity -= config.Gravity * dt;
            }
            _jumpQueued = false;

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * dt);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void UpdateStance(bool crouch, float dt)
        {
            float target = crouch ? config.CrouchHeight : config.StandHeight;
            if (Mathf.Approximately(_controller.height, target)) return;

            float next = Mathf.MoveTowards(_controller.height, target, config.StanceTransitionSpeed * dt);

            // Keep the capsule's feet planted while its height changes.
            float delta = next - _controller.height;
            _controller.height = next;
            _controller.center = new Vector3(_controller.center.x, next * 0.5f, _controller.center.z);
            if (delta > 0f && _controller.isGrounded)
                _controller.Move(Vector3.up * delta * 0.5f);
        }
    }
}
