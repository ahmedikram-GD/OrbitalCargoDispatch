using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Volatile twist (GDD): an amber beacon strobes on the crate wherever it is, and while carried the
    /// crate wobbles in the hands (amplitude from its CrateTypeSO). The walk slowdown itself is applied
    /// by <c>CrateCarrier</c>, which owns the player's speed. Only active for types with a slowdown or wobble.
    /// </summary>
    [RequireComponent(typeof(Crate))]
    [AddComponentMenu("OrbitalCargo/Crates/Volatile Effect")]
    public sealed class VolatileEffect : MonoBehaviour
    {
        [SerializeField] private Crate crate;
        [Tooltip("Small emissive beacon on the lid (Beacon child).")]
        [SerializeField] private Renderer beacon;
        [Tooltip("Beacon flashes per second.")]
        [Min(0.1f)] [SerializeField] private float blinkHz = 3f;
        [Tooltip("Wobble cycles per second while carried.")]
        [Min(0.1f)] [SerializeField] private float wobbleHz = 2.3f;
        [Tooltip("Roll/pitch wobble in degrees at full amplitude.")]
        [Min(0f)] [SerializeField] private float wobbleDegrees = 6f;

        private Vector3 _restLocalPosition;
        private bool _hasRest;

        private bool IsVolatile => crate != null && crate.Type != null &&
                                   (crate.Type.WobbleAmplitude > 0f || crate.Type.VolatileSlowMultiplier < 1f);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (crate == null) crate = GetComponent<Crate>();
        }

        private void Update()
        {
            bool on = IsVolatile;
            if (beacon != null) beacon.enabled = on && Mathf.Repeat(Time.time * blinkHz, 1f) < 0.5f;
        }

        private void LateUpdate()
        {
            bool wobbling = IsVolatile && crate.State == CrateState.Carried && !crate.Carryable.IsMoving;
            if (!wobbling)
            {
                _hasRest = false;
                return;
            }

            if (!_hasRest)
            {
                _restLocalPosition = transform.localPosition;
                _hasRest = true;
            }

            float t = Time.time * wobbleHz * Mathf.PI * 2f;
            float amp = crate.Type.WobbleAmplitude;
            transform.localPosition = _restLocalPosition + new Vector3(Mathf.Sin(t) * amp, Mathf.Abs(Mathf.Sin(t * 2f)) * amp * 0.5f, 0f);
            float scale = amp > 0f ? wobbleDegrees : 0f;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * scale * 0.5f, 0f, Mathf.Sin(t) * scale);
        }
    }
}
