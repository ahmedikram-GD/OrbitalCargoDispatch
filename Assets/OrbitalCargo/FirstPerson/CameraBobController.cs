using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Adds subtle procedural camera motion on top of the pitched camera pivot: gentle breathing
    /// while idle and a minimal footstep bob while walking. It only writes a <b>local position
    /// offset</b> on its own transform, so it never fights <see cref="FirstPersonLook"/> (which owns
    /// rotation). Sits on the Main Camera; reads locomotion through <see cref="ICharacterMotor"/>
    /// resolved from a parent. <see cref="Kick"/> is the "felt" half of picking up / placing a crate.
    /// Ported from Silly-Justice.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [AddComponentMenu("OrbitalCargo/First Person/Camera Bob")]
    public sealed class CameraBobController : MonoBehaviour
    {
        [Tooltip("Camera bob tuning asset (FP_CameraBobConfig). Required.")]
        [SerializeField] private CameraBobConfigSO config;

        private ICharacterMotor _motor;
        private Vector3 _restLocalPosition;
        private float _phase;

        // One-off jolt state (see Kick). Lives here because this class is the single owner of the
        // camera's local position — a separate component writing the same field would be overwritten.
        private float _kickAmplitude, _kickDuration, _kickElapsed;

        // Smoothed, currently-applied targets (SmoothDamp state).
        private float _vertAmp, _vertAmpVel;
        private float _horizAmp, _horizAmpVel;
        private float _frequency, _frequencyVel;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _motor = GetComponentInParent<ICharacterMotor>();
            if (config == null)
                Debug.LogError($"[CameraBobController] '{name}' has no CameraBobConfigSO assigned.", this);
            if (_motor == null)
                Debug.LogWarning($"[CameraBobController] '{name}' found no ICharacterMotor in parents; " +
                                 "idle breathing will still play but locomotion bob will not.", this);

            _restLocalPosition = transform.localPosition;
        }

        // Every enable resumes FROM the rest pose: phase 0 and zero amplitude, eased up by the
        // SmoothDamp in LateUpdate, so re-enabling never pops the camera.
        private void OnEnable()
        {
            _phase = 0f;
            _vertAmp = _horizAmp = 0f;
            _vertAmpVel = _horizAmpVel = _frequencyVel = 0f;
            _frequency = config != null ? config.IdleFrequency : 0f;
        }

        // LateUpdate runs after FirstPersonController.Update (which applies look), so the bob layers
        // cleanly on top of the current pitch/yaw for this frame.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 bob = Vector3.zero;

            if (config != null && dt > 0f)
            {
                ResolveTargets(out float targetVert, out float targetHoriz, out float targetFreq);

                float smooth = config.TransitionSmoothTime;
                _vertAmp = Mathf.SmoothDamp(_vertAmp, targetVert, ref _vertAmpVel, smooth);
                _horizAmp = Mathf.SmoothDamp(_horizAmp, targetHoriz, ref _horizAmpVel, smooth);
                _frequency = Mathf.SmoothDamp(_frequency, targetFreq, ref _frequencyVel, smooth);

                // Advance a continuous phase so frequency changes never cause a discontinuity.
                _phase += _frequency * (2f * Mathf.PI) * dt;
                if (_phase > 2f * Mathf.PI) _phase -= 2f * Mathf.PI;

                // Classic head-bob: vertical at full rate, horizontal figure-8 at half rate.
                float vertical = Mathf.Sin(_phase) * _vertAmp;
                float horizontal = Mathf.Cos(_phase * 0.5f) * _horizAmp;
                bob = new Vector3(horizontal, vertical, 0f);
            }

            // Decaying jolt layered on top of the bob (amp x s^2), re-applied from rest every frame.
            Vector3 kick = Vector3.zero;
            if (_kickElapsed < _kickDuration)
            {
                _kickElapsed += Time.unscaledDeltaTime;
                float s = 1f - Mathf.Clamp01(_kickElapsed / _kickDuration);
                float amp = _kickAmplitude * s * s;
                kick = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * amp;
            }

            transform.localPosition = _restLocalPosition + bob + kick;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Punch a one-off decaying jolt on the camera. Amplitude is in world units (~0.02 reads as a
        /// bump, not a quake); the jolt eases out over <paramref name="duration"/> real seconds.
        /// Re-kicking restarts the decay rather than stacking.
        /// </summary>
        public void Kick(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            _kickAmplitude = amplitude;
            _kickDuration = duration;
            _kickElapsed = 0f;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ResolveTargets(out float vert, out float horiz, out float freq)
        {
            bool moving = false;
            float speed = 0f;

            if (_motor != null && _motor.IsGrounded)
            {
                Vector3 v = _motor.Velocity;
                v.y = 0f; // planar speed only — vertical motion should not drive footsteps
                speed = v.magnitude;
                moving = speed > config.MoveSpeedThreshold;
            }

            if (!moving)
            {
                vert = config.IdleVerticalAmplitude;
                horiz = config.IdleHorizontalAmplitude;
                freq = config.IdleFrequency;
                return;
            }

            float t = Mathf.InverseLerp(config.MoveSpeedThreshold, config.RunSpeedThreshold, speed);
            vert = Mathf.Lerp(config.WalkVerticalAmplitude, config.RunVerticalAmplitude, t);
            horiz = Mathf.Lerp(config.WalkHorizontalAmplitude, config.RunHorizontalAmplitude, t);
            freq = Mathf.Lerp(config.WalkFrequency, config.RunFrequency, t);
        }
    }
}
