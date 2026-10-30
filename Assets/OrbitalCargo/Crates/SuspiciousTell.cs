using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Discovery hook, step 1 — "Spot it" (GDD): a suspicious crate shakes, and also sparks or drips
    /// green goo. Normal crates never do. The tell stays on (even in a bay, as a last warning before
    /// launch) until the crate has been scanned. It shakes the crate's body children, never its root,
    /// because the conveyor and the hands own the root transform.
    /// </summary>
    [RequireComponent(typeof(Crate))]
    [AddComponentMenu("OrbitalCargo/Crates/Suspicious Tell")]
    public sealed class SuspiciousTell : MonoBehaviour
    {
        [SerializeField] private Crate crate;
        [Tooltip("Body parts that jitter (Casing, Stripe).")]
        [SerializeField] private Transform[] shakeParts;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem drip;

        [Header("Shake")]
        [Tooltip("Jitter amplitude in metres.")]
        [Min(0f)] [SerializeField] private float shakeAmplitude = 0.025f;
        [Tooltip("Jitter speed.")]
        [Min(0f)] [SerializeField] private float shakeFrequency = 28f;
        [Tooltip("The shake comes in bursts: seconds per on/off cycle.")]
        [Min(0.1f)] [SerializeField] private float burstPeriod = 1.2f;

        private Vector3[] _rest;
        private bool _active;
        private float _seed;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (crate == null) crate = GetComponent<Crate>();
            _rest = new Vector3[shakeParts != null ? shakeParts.Length : 0];
            for (int i = 0; i < _rest.Length; i++) _rest[i] = shakeParts[i].localPosition;
        }

        private void OnEnable()
        {
            if (crate != null) crate.StateChanged += HandleStateChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (crate != null) crate.StateChanged -= HandleStateChanged;
            SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_active) return;
            bool burstOn = Mathf.Repeat(Time.time + _seed, burstPeriod) < burstPeriod * 0.55f;
            float amp = burstOn ? shakeAmplitude : 0f;
            float t = Time.time * shakeFrequency;
            for (int i = 0; i < _rest.Length; i++)
            {
                var offset = new Vector3(Mathf.PerlinNoise(t, _seed) - 0.5f, 0f, Mathf.PerlinNoise(_seed, t) - 0.5f) * (2f * amp);
                shakeParts[i].localPosition = _rest[i] + offset;
            }
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleStateChanged(Crate changed, CrateState state)
        {
            // A fresh crate on the belt picks a new tell; otherwise just re-evaluate (scanned → off).
            if (state == CrateState.OnConveyor) _seed = Random.Range(0f, 100f);
            Refresh();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void Refresh()
        {
            bool on = crate != null && crate.IsSuspicious && !crate.IsScanned && crate.State != CrateState.Consumed;
            SetActive(on);
        }

        private void SetActive(bool on)
        {
            if (_active == on && !on) return;
            _active = on;

            if (!on)
                for (int i = 0; i < _rest.Length; i++) if (shakeParts[i] != null) shakeParts[i].localPosition = _rest[i];

            // Shake always; plus one of sparks / goo, picked from the seed so each crate stays consistent.
            bool useSparks = Mathf.Repeat(_seed, 2f) < 1f;
            Toggle(sparks, on && useSparks);
            Toggle(drip, on && !useSparks);
        }

        private static void Toggle(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            if (on && !ps.isPlaying) ps.Play(true);
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
