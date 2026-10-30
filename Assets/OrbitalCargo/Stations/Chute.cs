using DG.Tweening;
using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using OrbitalCargo.Interaction;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// The round floor hatch. Swallows thrown crates that fall into its trigger, and offers "Drop" for
    /// whatever is in the player's hands. Junk pays its payout; a clean, non-junk crate is simply lost
    /// (GDD: "chute a clean crate → lose its value"). Slice 5 adds the scanned-contraband bonus.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Chute")]
    public sealed class Chute : MonoBehaviour, IInteractable
    {
        private const string CratePoolId = "Crate";

        [Tooltip("Aim point for assisted throws (Chute/TargetPoint).")]
        [SerializeField] private Transform targetPoint;
        [Tooltip("FP_InteractionConfig — prompt text.")]
        [SerializeField] private InteractionConfigSO config;
        [SerializeField] private CreditsService credits;
        [SerializeField] private PoolManagerSO cratePooler;
        [Tooltip("e_CrateChuted. Optional.")]
        [SerializeField] private GameEventWithCrate onCrateChuted;

        [Header("Swallow")]
        [Tooltip("Seconds a crate takes to sink out of sight.")]
        [Min(0.05f)] [SerializeField] private float swallowDuration = 0.35f;
        [Tooltip("How far (m) below the target point it sinks.")]
        [Min(0f)] [SerializeField] private float swallowDepth = 0.8f;

        public Vector3 TargetPoint => targetPoint != null ? targetPoint.position : transform.position;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        // The trigger lives on this object, so thrown crates (rigidbody) report here.
        private void OnTriggerEnter(Collider other)
        {
            var crate = other.GetComponentInParent<Crate>();
            if (crate != null && crate.State == CrateState.Thrown) Consume(crate);
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        public string Prompt(CrateCarrier carrier) =>
            carrier != null && carrier.HasCrate && config != null ? config.DropPrompt : string.Empty;

        public bool CanInteract(CrateCarrier carrier) => carrier != null && carrier.HasCrate;

        public void Interact(CrateCarrier carrier, RaycastHit hit)
        {
            if (CanInteract(carrier)) Consume(carrier.PopTop());
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Consume(Crate crate)
        {
            if (crate == null || crate.State == CrateState.Consumed) return;

            crate.Carryable.Freeze();
            crate.SetState(CrateState.Consumed);

            if (crate.Type != null && crate.Type.IsJunk && credits != null) credits.Add(crate.Type.Payout);
            if (onCrateChuted != null) onCrateChuted.Raise(crate);

            Transform t = crate.transform;
            DOTween.Kill(t);
            t.SetParent(null, true);
            DOTween.Sequence()
                .Join(t.DOMove(TargetPoint + Vector3.down * swallowDepth, swallowDuration).SetEase(Ease.InQuad))
                .Join(t.DOScale(0.1f, swallowDuration).SetEase(Ease.InQuad))
                .SetLink(crate.gameObject)
                .OnComplete(() => Release(crate));
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void Release(Crate crate)
        {
            if (crate == null) return;
            if (cratePooler != null) cratePooler.Release(CratePoolId, crate.gameObject);
            else Destroy(crate.gameObject);
        }
    }
}
