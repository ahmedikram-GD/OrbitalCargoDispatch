using System.Collections.Generic;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using OrbitalCargo.Interaction;
using OrbitalCargo.Stations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OrbitalCargo.Shift
{
    /// <summary>
    /// Runs one shift: starts when the FSM enters <c>ShiftGameState</c> (e_ShiftGameEnter), ticks a
    /// <see cref="ShiftClock"/> on scaled time (so pause freezes it), tallies <see cref="ShiftStats"/> into
    /// the shared <see cref="ShiftSessionSO"/> from economy events, and at time-up stops the belt and
    /// sweeps every crate still in play back to the pool — carried, on the belt, in bays, in the
    /// scanner or lying on the floor — with no payout and no fine, then raises e_ShiftEnded for the
    /// flow. Shuttles already in the air finish their launch (they were paid on lift-off).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Shift/Shift Controller")]
    public sealed class ShiftController : MonoBehaviour
    {
        private const string CratePoolId = "Crate";
        private const string LogPrefix = "[ShiftController]";

        [Title("Data")]
        [Required, SerializeField] private ShiftConfigSO config;
        [Required, Tooltip("ShiftSession — shift number and the stats the summary reads.")]
        [SerializeField] private ShiftSessionSO session;
        [Required, Tooltip("UpgradeService — carry capacity applied at shift start.")]
        [SerializeField] private UpgradeService upgrades;
        [Required, SerializeField] private PoolManagerSO cratePooler;

        [Title("Scene")]
        [Required, SerializeField] private CrateSpawner spawner;
        [Required, SerializeField] private Conveyor conveyor;
        [Required, SerializeField] private CrateCarrier carrier;
        [SerializeField] private ScannerBooth scanner;
        [SerializeField] private Bay[] bays;

        [Title("Listens")]
        [Tooltip("e_ShiftGameEnter — raised by ShiftGameState when a shift (re)starts.")]
        [SerializeField] private GameEvent onShiftGameEnter;
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;
        [SerializeField] private GameEventWithHiddenItem onItemCollected;
        [Tooltip("e_CreditsEarned — every positive credit change.")]
        [SerializeField] private GameEventWithInt onCreditsEarned;

        [Title("Raises")]
        [SerializeField] private GameEvent onShiftStarted;
        [Tooltip("e_ShiftTick (int) — whole seconds remaining, once per second, for the HUD timer.")]
        [SerializeField] private GameEventWithInt onShiftTick;
        [SerializeField] private GameEvent onShiftEnded;

        private readonly ShiftClock _clock = new ShiftClock(0f);
        private readonly List<Crate> _sweep = new List<Crate>();
        private int _lastTickSecond = -1;

        public bool IsRunning { get; private set; }
        public float Remaining => _clock.Remaining;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (onShiftGameEnter != null) onShiftGameEnter.Subscribe(BeginShift);
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
            if (onItemCollected != null) onItemCollected.Subscribe(HandleItemCollected);
            if (onCreditsEarned != null) onCreditsEarned.Subscribe(HandleCreditsEarned);
        }

        private void OnDisable()
        {
            if (onShiftGameEnter != null) onShiftGameEnter.UnSubscribe(BeginShift);
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
            if (onItemCollected != null) onItemCollected.UnSubscribe(HandleItemCollected);
            if (onCreditsEarned != null) onCreditsEarned.UnSubscribe(HandleCreditsEarned);
        }

        private void Update()
        {
            if (!IsRunning) return;

            bool ended = _clock.Tick(Time.deltaTime);
            RaiseTickIfNewSecond();
            if (ended) EndShift();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void BeginShift()
        {
            if (config == null || session == null)
            {
                Debug.LogError($"{LogPrefix} Missing config or session — run Tools/OrbitalCargo/Setup Game Scene.", this);
                return;
            }

            session.BeginShift();
            _clock.Reset(config.ShiftLengthSeconds);
            _lastTickSecond = -1;
            IsRunning = true;

            if (carrier != null && upgrades != null) carrier.SetCapacity(upgrades.CarryCapacity);
            if (spawner != null) spawner.Begin(session.ShiftNumber <= 1 ? config.Schedule : config.ScheduleShift2);

            RaiseTickIfNewSecond();
            if (onShiftStarted != null) onShiftStarted.Invoke();
        }

        [Button, DisableInEditorMode]
        public void EndShift()
        {
            if (!IsRunning) return;
            IsRunning = false;

            if (spawner != null) spawner.Stop();
            SweepCrates();
            if (onShiftEnded != null) onShiftEnded.Invoke();
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleShuttleLaunched(LaunchResult result)
        {
            if (!IsRunning) return;
            session.Stats.Launches++;
            session.Stats.CratesShipped += result.CrateCount;
            session.Stats.Fines += result.FineApplied;
        }

        private void HandleItemCollected(HiddenItemSO item)
        {
            if (IsRunning) session.Stats.ItemsFound++;
        }

        private void HandleCreditsEarned(int amount)
        {
            if (IsRunning) session.Stats.CreditsEarned += amount;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void RaiseTickIfNewSecond()
        {
            int second = Mathf.CeilToInt(_clock.Remaining);
            if (second == _lastTickSecond) return;
            _lastTickSecond = second;
            if (onShiftTick != null) onShiftTick.Raise(second);
        }

        // Every holder lets go first (so none keeps a reference to a pooled crate), then everything the
        // spawner still knows about that is in play — floor strays, mid-throw junk — goes too.
        private void SweepCrates()
        {
            _sweep.Clear();

            if (carrier != null)
                while (carrier.HasCrate) _sweep.Add(carrier.PopTop());

            if (scanner != null)
            {
                var occupant = scanner.Evict();
                if (occupant != null) _sweep.Add(occupant);
            }

            if (conveyor != null) _sweep.AddRange(conveyor.TakeAll());

            if (bays != null)
                foreach (var bay in bays)
                    if (bay != null) _sweep.AddRange(bay.TakeAll());

            if (spawner != null) spawner.CollectLive(_sweep);

            foreach (var crate in _sweep) Discard(crate);
            _sweep.Clear();
        }

        private void Discard(Crate crate)
        {
            if (crate == null || crate.State == CrateState.Consumed) return;
            crate.SetState(CrateState.Consumed);
            crate.transform.SetParent(null, true);
            if (cratePooler != null) cratePooler.Release(CratePoolId, crate.gameObject);
            else Destroy(crate.gameObject);
        }
    }
}
