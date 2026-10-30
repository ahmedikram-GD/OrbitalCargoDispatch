using DG.Tweening;
using UnityEngine;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// Rocks an object on the spot and puts it back exactly where it was — no Rigidbody, so twenty
    /// knocks look like the first instead of walking it off its mark. Ported from Silly Justice
    /// <c>PropShake</c>; unlike the original it reads the rest pose at the moment of the shake, because
    /// crates are pooled and slotted all over the room (the original cached it once in Awake). Sound is
    /// left to <see cref="JuicePresenter"/>.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Juice/Prop Shake")]
    public sealed class PropShake : MonoBehaviour
    {
        [Header("Rock")]
        [Tooltip("How far it tips, in degrees, at the strongest point. Small reads as a solid thump.")]
        [Min(0f)] [SerializeField] private float tipAngle = 7f;
        [Tooltip("Seconds the whole rock takes, settle included.")]
        [Min(0.05f)] [SerializeField] private float duration = 0.35f;
        [Tooltip("How many times it rocks back and forth before settling.")]
        [Min(1)] [SerializeField] private int vibrato = 7;
        [Tooltip("Squash on landing (fraction of size, applied as a scale punch). 0 = rock only.")]
        [Range(0f, 0.5f)] [SerializeField] private float squash = 0.12f;

        private Vector3 _restPos;
        private Quaternion _restRot;
        private Vector3 _restScale;
        private bool _shaking;

        /// <summary>Rock away from <paramref name="direction"/> (the way the hit travelled; zero = forward).</summary>
        public void Shake(Vector3 direction)
        {
            // A second knock mid-rock lands the first one, so the rest pose is never read off a tilt.
            Settle();
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
            _restScale = transform.localScale;
            _shaking = true;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
            flat.Normalize();

            // Tip around the axis across the hit, so it rocks away from it rather than twisting.
            Vector3 axis = Vector3.Cross(Vector3.up, flat);
            transform.DOPunchRotation(axis * tipAngle, duration, vibrato, 0.8f)
                .SetEase(Ease.OutQuad).SetLink(gameObject).OnComplete(HandleDone);
            if (squash > 0f)
                transform.DOPunchScale(new Vector3(squash, -squash, squash) * _restScale.y, duration, vibrato, 0.6f)
                    .SetLink(gameObject);
        }

        private void OnDisable() => Settle();

        private void HandleDone() => _shaking = false;

        private void Settle()
        {
            if (!_shaking) return;
            _shaking = false;
            transform.DOKill();
            transform.localPosition = _restPos;
            transform.localRotation = _restRot;
            transform.localScale = _restScale;
        }
    }
}
