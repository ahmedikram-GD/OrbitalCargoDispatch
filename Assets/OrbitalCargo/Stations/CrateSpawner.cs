using System.Collections.Generic;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Feeds the belt from a <see cref="SpawnScheduleSO"/>: releases due spawns from a
    /// <see cref="SpawnScheduler"/> and puts them on the <see cref="Conveyor"/> from the crate pool
    /// whenever the airlock is clear — spawns that come due while the belt is backed up wait their
    /// turn instead of overlapping. Raises e_CrateSpawned and, for the first crate of a beat entry,
    /// e_RadioBeat (Rook's lines, Slice 7). <c>ShiftController</c> drives Begin/Stop and, at shift end,
    /// asks for every crate still in play (<see cref="CollectLive"/>) so strays on the floor go too.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Crate Spawner")]
    public sealed class CrateSpawner : MonoBehaviour
    {
        private const string CratePoolId = "Crate";

        [SerializeField] private Conveyor conveyor;
        [SerializeField] private PoolManagerSO cratePooler;
        [Header("Events")]
        [SerializeField] private GameEventWithCrate onCrateSpawned;
        [SerializeField] private GameEventWithString onRadioBeat;

        private readonly Queue<SpawnRequest> _pending = new Queue<SpawnRequest>();
        private readonly List<SpawnRequest> _due = new List<SpawnRequest>();
        private readonly HashSet<Crate> _spawned = new HashSet<Crate>();
        private SpawnScheduler _scheduler;

        public float Elapsed { get; private set; }
        public bool IsRunning { get; private set; }
        public int PendingCount => _pending.Count;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (IsRunning && _scheduler != null)
            {
                Elapsed += Time.deltaTime;
                _scheduler.Advance(Elapsed, _due);
                foreach (var request in _due) _pending.Enqueue(request);
                _due.Clear();
            }

            while (_pending.Count > 0 && conveyor != null && conveyor.CanAccept)
                if (!Spawn(_pending.Peek())) break; // try again next frame
                else _pending.Dequeue();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Begin(SpawnScheduleSO schedule)
        {
            _scheduler = new SpawnScheduler(schedule != null ? schedule.Entries : null);
            _pending.Clear();
            Elapsed = 0f;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
            _pending.Clear();
        }

        /// <summary>Queue one extra crate for the belt (debug keys, tutorials).</summary>
        public void Enqueue(SpawnRequest request) => _pending.Enqueue(request);

        /// <summary>
        /// Adds every crate this spawner put out that is still in play (active, not consumed) to
        /// <paramref name="into"/>, then forgets them all — pooled crates get re-tracked when respawned.
        /// </summary>
        public void CollectLive(List<Crate> into)
        {
            foreach (var crate in _spawned)
                if (crate != null && crate.gameObject.activeInHierarchy && crate.State != CrateState.Consumed && !into.Contains(crate))
                    into.Add(crate);
            _spawned.Clear();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Put one crate on the belt. False (nothing spawned) if the pool or belt refused it.</summary>
        private bool Spawn(SpawnRequest request)
        {
            GameObject go = cratePooler != null ? cratePooler.Get(CratePoolId) : null;
            if (go == null)
            {
                Debug.LogError("[CrateSpawner] Crate pool returned nothing — is GameBootstrap initialising CratePooler?", this);
                return false;
            }

            var crate = go.GetComponent<Crate>();
            crate.Configure(request.Type, request.Suspicious, request.Hidden);
            if (!conveyor.Place(crate))
            {
                cratePooler.Release(CratePoolId, go);
                return false;
            }

            _spawned.Add(crate);
            if (onCrateSpawned != null) onCrateSpawned.Raise(crate);
            if (!string.IsNullOrEmpty(request.RadioBeat) && onRadioBeat != null) onRadioBeat.Raise(request.RadioBeat);
            return true;
        }
    }
}
