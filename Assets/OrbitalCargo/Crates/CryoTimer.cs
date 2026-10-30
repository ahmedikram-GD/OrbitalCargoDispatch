using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Cryo twist (GDD): a crate whose type has CryoSeconds &gt; 0 counts down from the moment it lands
    /// on the belt until it's slotted in a bay. Running out flags <see cref="Crate.CryoExpired"/> (it
    /// pays less at launch — soft failure) and raises e_CryoExpired. Logic in <see cref="CryoCountdown"/>.
    /// </summary>
    [RequireComponent(typeof(Crate))]
    [AddComponentMenu("OrbitalCargo/Crates/Cryo Timer")]
    public sealed class CryoTimer : MonoBehaviour
    {
        [SerializeField] private Crate crate;
        [SerializeField] private CryoRingView ring;
        [Tooltip("e_CryoExpired. Optional.")]
        [SerializeField] private GameEventWithCrate onCryoExpired;

        private readonly CryoCountdown _countdown = new CryoCountdown();

        public CryoCountdown Countdown => _countdown;
        private bool IsCryo => crate != null && crate.Type != null && crate.Type.CryoSeconds > 0f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (crate == null) crate = GetComponent<Crate>();
        }

        private void OnEnable()
        {
            if (crate != null) crate.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (crate != null) crate.StateChanged -= HandleStateChanged;
        }

        // Covers crates placed by hand in a scene, which never go through Configure.
        private void Start() => ResetForType();

        private void Update()
        {
            if (!IsCryo) return;

            if (_countdown.Tick(Time.deltaTime, IsCounting(crate.State)))
            {
                crate.CryoExpired = true;
                if (onCryoExpired != null) onCryoExpired.Raise(crate);
            }

            // Once delivered the ring has done its job; an expired ring stays as the "too late" badge.
            bool visible = crate.State != CrateState.Consumed && (crate.State != CrateState.Slotted || _countdown.Expired);
            if (ring == null) return;
            ring.SetVisible(visible);
            if (visible) ring.Show(_countdown.Normalized, _countdown.Expired);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleStateChanged(Crate changed, CrateState state)
        {
            if (state == CrateState.OnConveyor) ResetForType(); // fresh (or re-pooled) crate on the belt
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ResetForType()
        {
            _countdown.Reset(IsCryo ? crate.Type.CryoSeconds : 0f);
            if (ring != null) ring.SetVisible(IsCryo);
        }

        private static bool IsCounting(CrateState state) =>
            state == CrateState.OnConveyor || state == CrateState.Pickable || state == CrateState.Carried ||
            state == CrateState.Scanning || state == CrateState.Thrown;
    }
}
