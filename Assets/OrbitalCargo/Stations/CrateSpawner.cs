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
    /// e_RadioBeat (Rook's lines, Slice 7).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Crate Spawner")]
    public sealed class CrateSpawner : MonoBehaviour
    {
        private const string CratePoolId = "Crate";

        [SerializeField] private Conveyor conveyor;
        [SerializeField] private PoolManagerSO cratePooler;
        [Tooltip("ShiftConfig_Pitch — provides the schedule when starting on its own.")]
        [SerializeField] private ShiftConfigSO config;
        [Tooltip("Start the pitch schedule on scene start. Temporary: the Slice 6 ShiftController calls Begin instead.")]
        [SerializeField] private bool beginOnStart = true;

        [Header("Events")]
        [SerializeField] private GameEventWithCrate onCrateSpawned;
        [SerializeField] private GameEventWithString onRadioBeat;

        private readonly Queue<SpawnRequest> _pending = new Queue<SpawnRequest>();
        private readonly List<SpawnRequest> _due = new List<SpawnRequest>();
        private SpawnScheduler _scheduler;

        public float Elapsed { get; private set; }
        public bool IsRunning { get; private set; }
        public int PendingCount => _pending.Count;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (beginOnStart && config != null && config.Schedule != null) Begin(config.Schedule);
        }

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

            if (onCrateSpawned != null) onCrateSpawned.Raise(crate);
            if (!string.IsNullOrEmpty(request.RadioBeat) && onRadioBeat != null) onRadioBeat.Raise(request.RadioBeat);
            return true;
        }
    }
}
