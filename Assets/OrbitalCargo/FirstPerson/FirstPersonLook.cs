using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// <see cref="ILookController"/> that yaws the body (this transform) horizontally and pitches
    /// the camera pivot vertically, clamping pitch. Orientation only — it never touches locomotion.
    /// Tuning is data via <see cref="LookConfigSO"/>. Ported from Silly-Justice with the held-item
    /// look lock removed (crates don't lock the view).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/First Person/First Person Look")]
    public sealed class FirstPersonLook : MonoBehaviour, ILookController
    {
        [Tooltip("Look tuning asset (FP_LookConfig). Required.")]
        [SerializeField] private LookConfigSO config;

        [Tooltip("The camera pivot that gets pitched — the Main Camera child. Yaw is applied to this transform.")]
        [SerializeField] private Transform cameraPivot;

        private float _pitch;
        private float _pitchVelocity; // for optional smoothing
        private float _baseYaw;       // spawn facing — the centre of the yaw-limit window

        public float Pitch => _pitch;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _baseYaw = transform.eulerAngles.y;
            if (config == null)
                Debug.LogError($"[FirstPersonLook] '{name}' has no LookConfigSO assigned.", this);
            if (cameraPivot == null)
                Debug.LogError($"[FirstPersonLook] '{name}' has no camera pivot assigned.", this);
        }

        // ── ILookController ────────────────────────────────────────────────────

        public void Look(Vector2 lookDelta)
        {
            if (config == null) return;

            float yawDelta = lookDelta.x * config.Sensitivity;
            float pitchDelta = lookDelta.y * config.Sensitivity * (config.InvertY ? 1f : -1f);

            // Yaw the whole body so movement direction follows where we look. With the limit on, the
            // yaw is re-derived from the transform each frame (self-syncs even if another system
            // rotated the body) and clamped around the spawn facing.
            if (config.LimitYaw)
            {
                float current = Mathf.DeltaAngle(_baseYaw, transform.eulerAngles.y);
                float clamped = Mathf.Clamp(current + yawDelta, -config.YawRange, config.YawRange);
                transform.rotation = Quaternion.Euler(0f, _baseYaw + clamped, 0f);
            }
            else
            {
                transform.Rotate(Vector3.up, yawDelta, Space.World);
            }

            float targetPitch = Mathf.Clamp(_pitch + pitchDelta, config.MinPitch, config.MaxPitch);
            _pitch = config.Smoothing > 0f
                ? Mathf.SmoothDampAngle(_pitch, targetPitch, ref _pitchVelocity, 1f / config.Smoothing)
                : targetPitch;

            ApplyPitch();
        }

        // ── Public API (tutorial focus) ────────────────────────────────────────

        /// <summary>
        /// Ease the view toward <paramref name="worldPoint"/> (yaw the body, pitch the pivot) by the
        /// interpolation factor <paramref name="t"/> [0..1]. Drives orientation directly yet keeps
        /// <c>_pitch</c> in sync, so when the player takes control back there is no snap. Call once
        /// per frame with a small t for a smooth glide (used by Rook's "look at Bay A" beat).
        /// </summary>
        public void AimTowards(Vector3 worldPoint, float t)
        {
            Vector3 to = worldPoint - transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);

            if (flat.sqrMagnitude > 1e-4f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(flat, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetYaw, t);
            }

            // Pitch is measured from the camera (eye height), not the body's feet — the SJ original
            // used the body, so a target below eye level made the view look UP.
            Vector3 fromEye = cameraPivot != null ? worldPoint - cameraPivot.position : to;
            if (cameraPivot != null && fromEye.sqrMagnitude > 1e-4f)
            {
                float desiredPitch = -Mathf.Atan2(fromEye.y, new Vector2(fromEye.x, fromEye.z).magnitude) * Mathf.Rad2Deg;
                if (config != null) desiredPitch = Mathf.Clamp(desiredPitch, config.MinPitch, config.MaxPitch);
                _pitch = Mathf.LerpAngle(_pitch, desiredPitch, t);
                ApplyPitch();
            }
        }

        /// <summary>Directly set the vertical look pitch (positive = down), clamped and kept in sync.</summary>
        public void SetPitch(float pitch)
        {
            if (config != null) pitch = Mathf.Clamp(pitch, config.MinPitch, config.MaxPitch);
            _pitch = pitch;
            _pitchVelocity = 0f;
            ApplyPitch();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyPitch()
        {
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
