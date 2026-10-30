using OrbitalCargo.Crates;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.Stations;
using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Junk twist (GDD): a quick upward swipe on the look area while holding junk (or Space in the
    /// Editor) throws it. When the chute is close enough and roughly ahead, the throw is an exact
    /// ballistic arc into it ("assisted"); otherwise it's a plain throw along the view and the crate
    /// becomes pickable where it lands. Swipes while holding anything else, and ordinary look-drags,
    /// do nothing.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Interaction/Junk Thrower")]
    public sealed class JunkThrower : MonoBehaviour
    {
        [SerializeField] private CrateCarrier carrier;
        [Tooltip("FP_InteractionConfig — throw assist tuning.")]
        [SerializeField] private InteractionConfigSO config;
        [Tooltip("The Main Camera (aim direction).")]
        [SerializeField] private Transform aim;

        [Header("Scene wiring (set by SceneSetup)")]
        [SerializeField] private TouchLookArea lookArea;
        [SerializeField] private Chute chute;

        [Header("Events")]
        [Tooltip("e_CrateThrown. Optional.")]
        [SerializeField] private GameEventWithCrate onCrateThrown;

        private IInteractInput _input;
        private InteractionInputProvider _inputProvider;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _input = GetComponent<IInteractInput>();
            _inputProvider = GetComponent<InteractionInputProvider>();
            if (carrier == null) carrier = GetComponent<CrateCarrier>();
        }

        private void OnEnable()
        {
            if (lookArea != null) lookArea.Swiped += HandleSwiped;
        }

        private void OnDisable()
        {
            if (lookArea != null) lookArea.Swiped -= HandleSwiped;
        }

        private void Update()
        {
            if (_input != null && _input.ConsumeThrow()) TryThrow();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Throw the carried junk crate. False (nothing happens) unless the top crate is junk.</summary>
        public bool TryThrow()
        {
            if (carrier == null || !carrier.TopIsJunk || config == null || aim == null) return false;

            Crate crate = carrier.PopTop();
            crate.SetState(CrateState.Thrown);
            Vector3 velocity = ComputeVelocity(crate.transform.position);
            crate.Carryable.Launch(velocity, () => HandleLanded(crate));
            if (onCrateThrown != null) onCrateThrown.Raise(crate);
            return true;
        }

        /// <summary>Launch velocity from <paramref name="start"/>: an exact arc into the chute when assisted, else along the view.</summary>
        public Vector3 ComputeVelocity(Vector3 start)
        {
            if (chute != null)
            {
                Vector3 to = chute.TargetPoint - start;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 lookFlat = new Vector3(aim.forward.x, 0f, aim.forward.z);
                bool inRange = flat.magnitude <= config.ThrowAssistRange;
                bool ahead = lookFlat.sqrMagnitude < 1e-4f || Vector3.Angle(lookFlat, flat) <= config.ThrowAssistAngle;
                if (inRange && ahead)
                {
                    float t = Mathf.Clamp(flat.magnitude / config.ThrowHorizontalSpeed, config.ThrowFlightTimeMin, config.ThrowFlightTimeMax);
                    return ThrowMath.VelocityToHit(start, chute.TargetPoint, t, Physics.gravity);
                }
            }
            return (aim.forward + Vector3.up * config.FreeThrowUpBias).normalized * config.FreeThrowSpeed;
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        // Only an upward flick that is mostly vertical, and only while junk is in hand.
        private void HandleSwiped(Vector2 delta)
        {
            if (carrier == null || !carrier.TopIsJunk) return;
            if (delta.y <= 0f || delta.y < Mathf.Abs(delta.x)) return;
            if (_inputProvider != null) _inputProvider.RequestThrow();
        }

        private static void HandleLanded(Crate crate)
        {
            if (crate != null && crate.State == CrateState.Thrown) crate.SetState(CrateState.Pickable);
        }
    }
}
