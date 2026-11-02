using GameKit.Architecture.Events;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using OrbitalCargo.Shift;
using UnityEngine;

namespace OrbitalCargo.Radio
{
    /// <summary>
    /// Rook's beat director. The spawn schedule already raises the timeline beats (Intro, CryoIntro,
    /// Suspicious, Junk, Volatile) on e_RadioBeat; this adds the beats that depend on what the player
    /// does — first crate placed, first launch (shift 1 only, they're tutorial), the first wrong-bay
    /// crate of every shift — plus shift start and end, all derived from existing events so gameplay
    /// code never knows Rook exists. The guide arrow and on-screen lessons belong to
    /// <see cref="TutorialDirector"/>.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Radio/Rook Radio")]
    public sealed class RookRadio : MonoBehaviour
    {
        public const string IntroBeat = "Intro";
        public const string FirstPlaceBeat = "FirstPlace";
        public const string FirstLaunchBeat = "FirstLaunch";
        public const string WrongBayBeat = "WrongBay";
        public const string ShiftStartBeat = "ShiftStart";
        public const string ShiftEndBeat = "ShiftEnd";

        [Tooltip("ShiftSession — tutorial beats only play in shift 1.")]
        [SerializeField] private ShiftSessionSO session;

        [Header("Events")]
        [Tooltip("e_RadioBeat (string) — raised for derived beats.")]
        [SerializeField] private GameEventWithString onRadioBeat;
        [SerializeField] private GameEvent onShiftStarted;
        [SerializeField] private GameEvent onShiftEnded;
        [SerializeField] private GameEventWithCrate onCratePlaced;
        [Tooltip("e_WrongBay — Rook calls out the first wrong-bay crate each shift.")]
        [SerializeField] private GameEventWithCrate onWrongBay;
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;

        private bool _placedThisShift;
        private bool _launchedThisShift;
        private bool _wrongBayThisShift;

        private bool IsTutorialShift => session == null || session.ShiftNumber <= 1;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (onShiftStarted != null) onShiftStarted.Subscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.Subscribe(HandleShiftEnded);
            if (onCratePlaced != null) onCratePlaced.Subscribe(HandleCratePlaced);
            if (onWrongBay != null) onWrongBay.Subscribe(HandleWrongBay);
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
        }

        private void OnDisable()
        {
            if (onShiftStarted != null) onShiftStarted.UnSubscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.UnSubscribe(HandleShiftEnded);
            if (onCratePlaced != null) onCratePlaced.UnSubscribe(HandleCratePlaced);
            if (onWrongBay != null) onWrongBay.UnSubscribe(HandleWrongBay);
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleShiftStarted()
        {
            _placedThisShift = false;
            _launchedThisShift = false;
            _wrongBayThisShift = false;
            // Shift 1 opens with the schedule's Intro beat; later shifts get a short welcome back.
            if (!IsTutorialShift) Raise(ShiftStartBeat);
        }

        private void HandleShiftEnded() => Raise(ShiftEndBeat);

        private void HandleCratePlaced(Crate crate)
        {
            if (_placedThisShift) return;
            _placedThisShift = true;
            if (IsTutorialShift) Raise(FirstPlaceBeat);
        }

        private void HandleWrongBay(Crate crate)
        {
            if (_wrongBayThisShift) return;
            _wrongBayThisShift = true;
            Raise(WrongBayBeat);
        }

        private void HandleShuttleLaunched(LaunchResult result)
        {
            if (_launchedThisShift) return;
            _launchedThisShift = true;
            if (IsTutorialShift) Raise(FirstLaunchBeat);
        }

        private void Raise(string beat)
        {
            if (onRadioBeat != null) onRadioBeat.Raise(beat);
        }
    }
}
