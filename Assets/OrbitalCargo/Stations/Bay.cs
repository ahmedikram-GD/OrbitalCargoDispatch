using System;
using System.Collections.Generic;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Interaction;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// A shuttle bay: six slots on a colour-coded pad. Adapts the pure <see cref="BayModel"/> to the
    /// scene — aiming at the pad (or a crate already on it) with a crate in hand offers "Place",
    /// previews the nearest free slot with the ghost, and snaps the crate there. Any crate may go in
    /// any bay; a wrong bay just pays less at launch (soft failure, GDD) — but it is never silent: the
    /// ghost and context button turn red while aiming, and e_WrongBay fires on placement so the juice
    /// presenter and Rook can call it out.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Bay")]
    public sealed class Bay : MonoBehaviour, IInteractable, IInteractPreview, IInteractWarning
    {
        [SerializeField] private BayId id;
        [Tooltip("Slots in index order (built by StationWiring from RoomLayoutSO).")]
        [SerializeField] private Slot[] slots;
        [SerializeField] private PlacementGhost ghost;
        [Tooltip("FP_InteractionConfig — snap timing and prompt text.")]
        [SerializeField] private InteractionConfigSO config;

        [Header("Events")]
        [Tooltip("e_CratePlaced. Raised after a crate is put on a slot. Optional.")]
        [SerializeField] private GameEventWithCrate onCratePlaced;
        [Tooltip("e_WrongBay. Raised after a crate is put in a bay that isn't its destination. Optional.")]
        [SerializeField] private GameEventWithCrate onWrongBay;

        private BayModel _model;
        private Vector3[] _slotPositions;

        public BayId Id => id;
        public BayModel Model => _model;
        public IReadOnlyList<Slot> Slots => slots;

        /// <summary>Set by the shuttle launcher while its shuttle is away: nothing can be placed.</summary>
        public bool IsLocked { get; set; }

        /// <summary>Raised when a crate is placed or the bay is emptied.</summary>
        public event Action<Bay> Changed;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogError($"[Bay] '{name}' has no slots — run Tools/OrbitalCargo/Setup Game Scene.", this);
                slots = Array.Empty<Slot>();
                _model = new BayModel(1);
                _slotPositions = Array.Empty<Vector3>();
                return;
            }

            _model = new BayModel(slots.Length);
            _slotPositions = new Vector3[slots.Length];
            for (int i = 0; i < slots.Length; i++) _slotPositions[i] = slots[i].Anchor.position;
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        public string Prompt(CrateCarrier carrier)
        {
            if (carrier == null || !carrier.HasCrate || config == null || IsLocked) return string.Empty;
            if (_model.IsFull) return config.BayFullPrompt;
            return IsWrongFor(carrier.Top) ? config.WrongBayPrompt : config.PlacePrompt;
        }

        public bool CanInteract(CrateCarrier carrier) => carrier != null && carrier.HasCrate && !_model.IsFull && !IsLocked;

        public void Interact(CrateCarrier carrier, RaycastHit hit)
        {
            if (!CanInteract(carrier) || !_model.TryGetNearestFreeSlot(hit.point, _slotPositions, out int slot)) return;
            PlaceInSlot(carrier.PopTop(), slot);
        }

        // ── IInteractPreview ───────────────────────────────────────────────────

        public void ShowPreview(CrateCarrier carrier, RaycastHit hit)
        {
            if (ghost == null) return;
            if (CanInteract(carrier) && _model.TryGetNearestFreeSlot(hit.point, _slotPositions, out int slot))
                ghost.Show(slots[slot].Anchor, IsWrongFor(carrier.Top));
            else
                ghost.Hide();
        }

        public void HidePreview()
        {
            if (ghost != null) ghost.Hide();
        }

        // ── IInteractWarning ───────────────────────────────────────────────────

        public bool IsWarning(CrateCarrier carrier) => CanInteract(carrier) && IsWrongFor(carrier.Top);

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Place <paramref name="crate"/> in the free slot nearest <paramref name="nearPoint"/>. False if full.</summary>
        public bool TryPlace(Crate crate, Vector3 nearPoint)
        {
            if (crate == null || !_model.TryGetNearestFreeSlot(nearPoint, _slotPositions, out int slot)) return false;
            PlaceInSlot(crate, slot);
            return true;
        }

        /// <summary>Empty the bay: returns every slotted crate (still parented to its slot) and clears the model.</summary>
        public List<Crate> TakeAll()
        {
            var crates = new List<Crate>(slots.Length);
            foreach (var slot in slots)
            {
                if (slot.Occupant == null) continue;
                crates.Add(slot.Occupant);
                slot.Occupant = null;
            }
            _model.Clear();
            Changed?.Invoke(this);
            return crates;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void PlaceInSlot(Crate crate, int slot)
        {
            if (crate == null) return;

            _model.Occupy(slot, crate.Id);
            slots[slot].Occupant = crate;
            crate.SetState(CrateState.Slotted);
            crate.Carryable.SnapTo(slots[slot].Anchor, config.SnapDuration, config.FlyArcHeight);

            Changed?.Invoke(this);
            if (onCratePlaced != null) onCratePlaced.Raise(crate);
            if (IsWrongFor(crate) && onWrongBay != null) onWrongBay.Raise(crate);
        }

        private bool IsWrongFor(Crate crate) => crate != null && crate.Type != null && !crate.Type.BelongsIn(id);
    }
}
