using System;
using System.Collections.Generic;
using OrbitalCargo.Crates;
using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// The interact brain, adapted from Silly-Justice's <c>HandInteractor</c>. Every frame it casts the
    /// centre-screen ray, decides what the context button would do (pick up a crate, or whatever the
    /// aimed-at <see cref="IInteractable"/> offers), outlines a pickable crate, drives the station's
    /// preview (the bay ghost) and publishes the prompt for the button. An interact request (E or the
    /// button) performs it. No hands rig, so pick-up is immediate instead of waiting for an animation
    /// event; SFX moved to the juice presenter via <c>e_CratePicked</c>.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Interaction/Crate Interactor")]
    public sealed class CrateInteractor : MonoBehaviour
    {
        [Tooltip("FP_InteractionConfig. Required.")]
        [SerializeField] private InteractionConfigSO config;
        [Tooltip("Ray origin & aim — the Main Camera. Falls back to Camera.main.")]
        [SerializeField] private Transform aim;
        [Tooltip("The player's CrateCarrier (same object).")]
        [SerializeField] private CrateCarrier carrier;

        [Header("Events")]
        [Tooltip("e_CratePicked. Raised after a crate is taken into the hands. Optional.")]
        [SerializeField] private GameEventWithCrate onCratePicked;

        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private IInteractInput _input;
        private Crate _focusCrate;
        private IInteractable _focusTarget;
        private IInteractPreview _focusPreview;
        private IHighlightable _focusHighlight;
        private RaycastHit _focusHit;

        /// <summary>Current button label ("" = hidden).</summary>
        public string Prompt { get; private set; } = string.Empty;

        /// <summary>Whether the current prompt can be acted on.</summary>
        public bool PromptEnabled { get; private set; }

        /// <summary>Raised when the prompt text or its enabled state changes.</summary>
        public event Action<string, bool> PromptChanged;

        public Crate FocusedCrate => _focusCrate;
        public IInteractable FocusedTarget => _focusTarget;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _input = GetComponent<IInteractInput>();
            if (carrier == null) carrier = GetComponent<CrateCarrier>();
            if (aim == null && Camera.main != null) aim = Camera.main.transform;

            if (_input == null) Debug.LogError("[CrateInteractor] No IInteractInput component found.", this);
            if (config == null) Debug.LogError("[CrateInteractor] No InteractionConfigSO assigned.", this);
            if (aim == null) Debug.LogError("[CrateInteractor] No aim transform (and no Camera.main).", this);
        }

        private void OnDisable() => ClearFocus();

        private void Update()
        {
            UpdateFocus();
            if (_input != null && _input.ConsumeInteract()) Interact();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Perform whatever the prompt currently offers.</summary>
        public void Interact()
        {
            if (carrier == null) return;

            if (_focusCrate != null)
            {
                var crate = _focusCrate;
                if (carrier.TryPickUp(crate))
                {
                    ClearFocus();
                    if (onCratePicked != null) onCratePicked.Raise(crate);
                }
                return;
            }

            if (_focusTarget != null && _focusTarget.CanInteract(carrier))
                _focusTarget.Interact(carrier, _focusHit);
        }

        // ── Focus ──────────────────────────────────────────────────────────────

        private void UpdateFocus()
        {
            if (config == null || aim == null || carrier == null)
            {
                ClearFocus();
                return;
            }

            if (!TryGetFirstHit(out RaycastHit hit))
            {
                ClearFocus();
                return;
            }

            var crate = hit.collider.GetComponentInParent<Crate>();
            if (crate != null && crate.IsPickable)
            {
                SetFocus(crate, null, hit);
                bool canTake = !carrier.IsFull;
                SetPrompt(canTake ? config.PickUpPrompt : config.HandsFullPrompt, canTake);
                return;
            }

            // A slotted crate is a child of its bay, so this also resolves the bay behind it.
            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target != null)
            {
                SetFocus(null, target, hit);
                SetPrompt(target.Prompt(carrier), target.CanInteract(carrier));
                _focusPreview?.ShowPreview(carrier, hit);
                return;
            }

            ClearFocus();
        }

        /// <summary>Nearest hit within reach. The first thing hit decides — walls block, nothing is seen through.</summary>
        private bool TryGetFirstHit(out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(aim.position, aim.forward, _hits, config.PickRange,
                                                config.PickMask, QueryTriggerInteraction.Ignore);
            if (count == 0) return false;

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                nearest = _hits[i];
            }
            return true;
        }

        private void SetFocus(Crate crate, IInteractable target, RaycastHit hit)
        {
            _focusHit = hit;

            if (!ReferenceEquals(target, _focusTarget))
            {
                _focusPreview?.HidePreview();
                _focusTarget = target;
                _focusPreview = target as IInteractPreview;
            }

            _focusCrate = crate;
            var highlight = crate != null ? crate.GetComponent<IHighlightable>() : null;
            if (ReferenceEquals(highlight, _focusHighlight)) return;
            _focusHighlight?.SetHighlighted(false);
            _focusHighlight = highlight;
            _focusHighlight?.SetHighlighted(true);
        }

        private void ClearFocus()
        {
            SetFocus(null, null, default);
            SetPrompt(string.Empty, false);
        }

        private void SetPrompt(string text, bool enabled)
        {
            text ??= string.Empty;
            if (text == Prompt && enabled == PromptEnabled) return;
            Prompt = text;
            PromptEnabled = enabled;
            PromptChanged?.Invoke(text, enabled);
        }
    }
}
