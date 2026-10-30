using System.Collections.Generic;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Kinematic belt: owns a <see cref="ConveyorQueue"/> along the room's conveyor waypoints and writes
    /// each riding crate's position from it every frame — no physics. A crate leaves the belt the
    /// moment its state stops being OnConveyor (picked up), so nothing else needs a reference to it.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Conveyor")]
    public sealed class Conveyor : MonoBehaviour
    {
        [Tooltip("Belt path, airlock first (Conveyor/Waypoint_i, built by RoomBuilder).")]
        [SerializeField] private Transform[] waypoints;
        [Tooltip("ShiftConfig_Pitch — belt speed and spacing.")]
        [SerializeField] private ShiftConfigSO config;

        private readonly Dictionary<int, Crate> _crates = new Dictionary<int, Crate>();
        private ConveyorQueue _queue;

        public bool CanAccept => _queue != null && _queue.CanEnqueue;
        public int Count => _queue != null ? _queue.Count : 0;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            var points = new List<Vector3>(waypoints != null ? waypoints.Length : 0);
            if (waypoints != null)
                foreach (var w in waypoints) if (w != null) points.Add(w.position);
            if (points.Count < 2 || config == null)
                Debug.LogError("[Conveyor] Needs at least two waypoints and a ShiftConfigSO.", this);
            _queue = new ConveyorQueue(points, config != null ? config.ConveyorSpacing : 1f);
        }

        private void Update()
        {
            if (_queue == null || config == null || _queue.Count == 0) return;
            _queue.Advance(Time.deltaTime, config.ConveyorSpeed);
            foreach (int id in _queue.Items)
                if (_crates.TryGetValue(id, out var crate) && crate != null) Apply(crate);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Put <paramref name="crate"/> on the belt at the airlock. False if the entry is blocked.</summary>
        public bool Place(Crate crate)
        {
            if (crate == null || _queue == null || !_queue.Enqueue(crate.Id)) return false;
            _crates[crate.Id] = crate;
            crate.StateChanged += HandleCrateStateChanged;
            crate.Carryable.PlaceImmediate(null);
            Apply(crate);
            return true;
        }

        public void Remove(Crate crate)
        {
            if (crate == null) return;
            crate.StateChanged -= HandleCrateStateChanged;
            _crates.Remove(crate.Id);
            _queue?.Remove(crate.Id);
        }

        /// <summary>Take every crate off the belt (shift end). The caller decides where they go.</summary>
        public List<Crate> TakeAll()
        {
            var all = new List<Crate>(_crates.Values);
            foreach (var crate in all) Remove(crate);
            return all;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void HandleCrateStateChanged(Crate crate, CrateState state)
        {
            if (state != CrateState.OnConveyor) Remove(crate);
        }

        private void Apply(Crate crate)
        {
            Vector3 forward = _queue.ForwardOf(crate.Id);
            crate.transform.SetPositionAndRotation(_queue.PositionOf(crate.Id), Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
