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
    /// does — first crate placed, first launch (shift 1 only, they're tutorial) — plus shift start and
    /// end, all derived from existing events so gameplay code never knows Rook exists. It also owns the
    /// guide arrow: up over Bay A on the Intro beat, down once the first crate lands in a bay.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Radio/Rook Radio")]
    public sealed class RookRadio : MonoBehaviour
    {
        public const string IntroBeat = "Intro";
        public const string FirstPlaceBeat = "FirstPlace";
        public const string FirstLaunchBeat = "FirstLaunch";
        public const string ShiftStartBeat = "ShiftStart";
        public const string ShiftEndBeat = "ShiftEnd";

        [Tooltip("ShiftSession — tutorial beats only play in shift 1.")]
        [SerializeField] private ShiftSessionSO session;
        [Tooltip("Arrow over Bay A (scene). Optional.")]
        [SerializeField] private GuideArrow arrow;

        [Header("Events")]
        [Tooltip("e_RadioBeat (string) — listened to (Intro shows the arrow) and raised for derived beats.")]
        [SerializeField] private GameEventWithString onRadioBeat;
        [SerializeField] private GameEvent onShiftStarted;
        [SerializeField] private GameEvent onShiftEnded;
        [SerializeField] private GameEventWithCrate onCratePlaced;
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;

        private bool _placedThisShift;
        private bool _launchedThisShift;

        private bool IsTutorialShift => session == null || session.ShiftNumber <= 1;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (arrow != null) arrow.Hide();
        }

        private void OnEnable()
        {
            if (onRadioBeat != null) onRadioBeat.Subscribe(HandleRadioBeat);
            if (onShiftStarted != null) onShiftStarted.Subscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.Subscribe(HandleShiftEnded);
            if (onCratePlaced != null) onCratePlaced.Subscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
        }

        private void OnDisable()
        {
            if (onRadioBeat != null) onRadioBeat.UnSubscribe(HandleRadioBeat);
            if (onShiftStarted != null) onShiftStarted.UnSubscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.UnSubscribe(HandleShiftEnded);
            if (onCratePlaced != null) onCratePlaced.UnSubscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleRadioBeat(string beat)
        {
            if (beat == IntroBeat && arrow != null && !_placedThisShift) arrow.Show();
        }

        private void HandleShiftStarted()
        {
            _placedThisShift = false;
            _launchedThisShift = false;
            if (arrow != null) arrow.Hide();
            // Shift 1 opens with the schedule's Intro beat; later shifts get a short welcome back.
            if (!IsTutorialShift) Raise(ShiftStartBeat);
        }

        private void HandleShiftEnded()
        {
            if (arrow != null) arrow.Hide();
            Raise(ShiftEndBeat);
        }

        private void HandleCratePlaced(Crate crate)
        {
            if (_placedThisShift) return;
            _placedThisShift = true;
            if (arrow != null) arrow.Hide();
            if (IsTutorialShift) Raise(FirstPlaceBeat);
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
