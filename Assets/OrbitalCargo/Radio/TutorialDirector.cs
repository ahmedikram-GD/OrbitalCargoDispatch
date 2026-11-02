using System;
using System.Collections.Generic;
using GameKit.Architecture.Events;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.Interaction;
using OrbitalCargo.Shift;
using OrbitalCargo.Stations;
using OrbitalCargo.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalCargo.Radio
{
    /// <summary>
    /// Runs the first-shift onboarding, one thing at a time. First it holds the shift and teaches the
    /// controls (move card + hand on the stick until the player has walked a few metres, then look card
    /// + hand sweep until they've turned around). Then, for the first crate of every kind (and the first
    /// suspicious one), it gives one guided delivery: camera nudge to the crate, arrow over it, objective
    /// card in the crate's colour, hand on the button when aiming at it; once it's in the hands the arrow
    /// and camera move to where it belongs (bay, chute or scanner). The first full bay gets a "hit LAUNCH"
    /// lesson. Later shifts skip all of it — only the always-on wrong-bay warnings remain (Bay, juice).
    /// <para>Sequencing lives in the pure <see cref="TutorialModel"/>/<see cref="TutorialRules"/>; this
    /// adapts them to the scene. Lessons queue, so crates that arrive together are taught in turn.</para>
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Radio/Tutorial Director")]
    public sealed class TutorialDirector : MonoBehaviour
    {
        private const string LogPrefix = "[TutorialDirector]";

        [Title("Data")]
        [Required, Tooltip("TutorialScript_Pitch — texts, thresholds, camera nudge, arrow heights.")]
        [SerializeField] private TutorialScriptSO script;
        [Required, Tooltip("ShiftSession — the tutorial only runs in shift 1.")]
        [SerializeField] private ShiftSessionSO session;

        [Title("Scene (set by SceneSetup)")]
        [Required, SerializeField] private ShiftController shift;
        [Required, SerializeField] private FirstPersonController player;
        [Required, SerializeField] private FirstPersonLook look;
        [Required, SerializeField] private CrateInteractor interactor;
        [Required, Tooltip("The player's hands — a lesson follows whichever crate of its kind gets picked up.")]
        [SerializeField] private CrateCarrier carrier;
        [Required, SerializeField] private TutorialOverlayView overlay;
        [Required, SerializeField] private GuideArrow arrow;
        [SerializeField] private Bay[] bays;
        [SerializeField] private LaunchButton[] launchButtons;
        [SerializeField] private Chute chute;
        [SerializeField] private ScannerBooth scanner;

        [Title("Listens")]
        [SerializeField] private GameEvent onShiftStarted;
        [SerializeField] private GameEvent onShiftEnded;
        [Tooltip("e_CrateSpawned — the first crate of each kind gets its lesson.")]
        [SerializeField] private GameEventWithCrate onCrateSpawned;

        private sealed class Lesson
        {
            public Crate Crate;
            public CrateLesson Data;
            public LaunchButton Launch;
            public bool IsFinished;
        }

        private readonly Queue<Lesson> _queue = new Queue<Lesson>();
        private TutorialModel _model;
        private Lesson _current;
        private LessonStage _shownStage;
        private LessonTarget _shownTarget;
        private bool _shownScanned;
        private bool _launchTaught;
        private float _cardClearLeft;
        private Action _afterCardClear;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _lastPitch;
        private float _aimLeft;
        private Transform _aimTarget;
        private Vector3 _aimPoint;

        private static bool IsTouch => Touchscreen.current != null || Application.isMobilePlatform;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (script == null) Debug.LogError($"{LogPrefix} No TutorialScriptSO — run Tools/OrbitalCargo/Setup Game Scene.", this);
            _model = new TutorialModel(script != null ? script.MoveDistance : 2f, script != null ? script.LookDegrees : 90f);
            if (arrow != null) arrow.Hide();
        }

        private void OnEnable()
        {
            if (onShiftStarted != null) onShiftStarted.Subscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.Subscribe(HandleShiftEnded);
            if (onCrateSpawned != null) onCrateSpawned.Subscribe(HandleCrateSpawned);
            if (bays != null) foreach (var bay in bays) if (bay != null) bay.Changed += HandleBayChanged;
        }

        private void OnDisable()
        {
            if (onShiftStarted != null) onShiftStarted.UnSubscribe(HandleShiftStarted);
            if (onShiftEnded != null) onShiftEnded.UnSubscribe(HandleShiftEnded);
            if (onCrateSpawned != null) onCrateSpawned.UnSubscribe(HandleCrateSpawned);
            if (bays != null) foreach (var bay in bays) if (bay != null) bay.Changed -= HandleBayChanged;
            StopAll();
        }

        private void Update()
        {
            if (_model == null || script == null) return;
            switch (_model.Phase)
            {
                case TutorialPhase.Move:
                    if (TickCardClear()) return;
                    if (_model.ReportMoved(TakeWalked())) FinishControlCard(StartLookLesson);
                    else if (overlay != null) overlay.SetCardProgress(_model.Progress);
                    break;
                case TutorialPhase.Look:
                    if (TickCardClear()) return;
                    if (_model.ReportLooked(TakeTurned())) FinishControlCard(EndControlLessons);
                    else if (overlay != null) overlay.SetCardProgress(_model.Progress);
                    break;
                case TutorialPhase.Cargo:
                    TickCardClear();
                    TickLesson();
                    break;
            }
        }

        // The nudge runs after the controller has applied (suppressed) input this frame.
        private void LateUpdate()
        {
            if (_aimLeft <= 0f) return;
            _aimLeft -= Time.deltaTime;
            if (_aimTarget != null) _aimPoint = _aimTarget.position;
            if (look != null) look.AimTowards(_aimPoint, 1f - Mathf.Exp(-script.AimSharpness * Time.deltaTime));
            if (_aimLeft <= 0f && player != null) player.IsLookSuppressed = false;
        }

        // ── Shift ──────────────────────────────────────────────────────────────

        private void HandleShiftStarted()
        {
            StopAll();
            _launchTaught = false;
            _model.Begin(session == null || session.ShiftNumber <= 1);
            if (_model.Phase != TutorialPhase.Move) return;

            if (shift != null) shift.SetHeld(true);
            ResetTracking();
            ShowControlCard(script.MoveCard, TutorialGesture.Joystick);
        }

        private void HandleShiftEnded()
        {
            _model.End();
            StopAll();
        }

        // ── Control lessons ────────────────────────────────────────────────────

        private void StartLookLesson()
        {
            ResetTracking();
            ShowControlCard(script.LookCard, TutorialGesture.LookDrag);
        }

        private void EndControlLessons()
        {
            if (overlay != null) overlay.HideAll();
            if (shift != null) shift.SetHeld(false);
        }

        private void ShowControlCard(TutorialCard data, TutorialGesture gesture)
        {
            if (overlay == null || data == null) return;
            string body = !IsTouch && !string.IsNullOrEmpty(data.KeyboardBody) ? data.KeyboardBody : data.Body;
            overlay.ShowCard(data.Title, body);
            overlay.SetGesture(gesture);
        }

        // Fill the bar, linger a beat, then run what comes next — so the player sees they got it.
        private void FinishControlCard(Action next)
        {
            if (overlay != null)
            {
                overlay.SetCardProgress(1f);
                overlay.SetGesture(TutorialGesture.None);
            }
            _afterCardClear = next;
            _cardClearLeft = Mathf.Max(0.01f, script.CardClearDelay);
        }

        private bool TickCardClear()
        {
            if (_cardClearLeft <= 0f) return false;
            _cardClearLeft -= Time.deltaTime;
            if (_cardClearLeft > 0f) return true;
            var next = _afterCardClear;
            _afterCardClear = null;
            next?.Invoke();
            return true;
        }

        private void ResetTracking()
        {
            if (player == null) return;
            _lastPosition = player.transform.position;
            _lastYaw = player.transform.eulerAngles.y;
            _lastPitch = look != null ? look.Pitch : 0f;
        }

        private float TakeWalked()
        {
            if (player == null) return 0f;
            Vector3 p = player.transform.position;
            Vector3 d = p - _lastPosition;
            _lastPosition = p;
            return new Vector2(d.x, d.z).magnitude;
        }

        private float TakeTurned()
        {
            if (player == null) return 0f;
            float yaw = player.transform.eulerAngles.y;
            float pitch = look != null ? look.Pitch : 0f;
            float turned = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) + Mathf.Abs(pitch - _lastPitch);
            _lastYaw = yaw;
            _lastPitch = pitch;
            return turned;
        }

        // ── Crate lessons ──────────────────────────────────────────────────────

        private void HandleCrateSpawned(Crate crate)
        {
            if (crate == null || crate.Type == null || script == null) return;
            string key = KeyOf(crate);
            if (!script.TryGetLesson(key, out var data) || !_model.TryClaimIntro(key)) return;
            _queue.Enqueue(new Lesson { Crate = crate, Data = data });
        }

        private void HandleBayChanged(Bay bay)
        {
            if (_launchTaught || _model.Phase != TutorialPhase.Cargo || bay == null || bay.Model == null || !bay.Model.IsFull) return;
            var button = LaunchButtonFor(bay);
            if (button == null) return;
            _launchTaught = true;
            _queue.Enqueue(new Lesson { Launch = button, Data = script.LaunchLesson });
        }

        private void TickLesson()
        {
            if (_current == null && !TryStartNext()) return;

            if (_current.Launch != null)
            {
                TickLaunchLesson();
                return;
            }

            var crate = _current.Crate;
            if (_current.IsFinished || crate == null || !crate.gameObject.activeInHierarchy)
            {
                FinishLesson();
                return;
            }

            LessonStage stage = TutorialRules.StageOf(crate.State);
            if (stage == LessonStage.Fetch && TryAdoptCarriedTwin())
            {
                crate = _current.Crate;
                stage = TutorialRules.StageOf(crate.State);
            }
            if (stage == LessonStage.Done)
            {
                FinishLesson();
                return;
            }

            LessonTarget target = TutorialRules.TargetOf(crate.Type.Destination, crate.IsSuspicious, crate.IsScanned, crate.IsBad);
            if (stage != _shownStage || target != _shownTarget || crate.IsScanned != _shownScanned)
            {
                bool startedDelivering = stage == LessonStage.Deliver && _shownStage != LessonStage.Deliver;
                _shownStage = stage;
                _shownTarget = target;
                _shownScanned = crate.IsScanned;
                ShowObjective(crate, stage);
                if (startedDelivering) BeginAim(null, TargetPoint(crate, target, false));
            }

            switch (stage)
            {
                case LessonStage.Fetch:
                    if (arrow != null) arrow.ShowAt(crate.transform.position + Vector3.up * script.ArrowAboveCrate);
                    SetGesture(interactor != null && interactor.FocusedCrate == crate ? TutorialGesture.Tap : TutorialGesture.None);
                    break;
                case LessonStage.Deliver:
                    if (arrow != null) arrow.ShowAt(TargetPoint(crate, target, true));
                    if (IsAimingAt(crate, target)) SetGesture(TutorialGesture.Tap);
                    else SetGesture(_current.Data.ShowThrowGesture && target == LessonTarget.Chute ? TutorialGesture.SwipeUp : TutorialGesture.None);
                    break;
                default:
                    if (arrow != null) arrow.Hide();
                    SetGesture(TutorialGesture.None);
                    break;
            }
        }

        private void TickLaunchLesson()
        {
            var launcher = _current.Launch.Launcher;
            if (launcher == null || !launcher.CanLaunch)
            {
                FinishLesson();
                return;
            }
            if (arrow != null) arrow.ShowAt(_current.Launch.transform.position + Vector3.up * script.ArrowAboveLaunchButton);
            bool aiming = interactor != null && ReferenceEquals(interactor.FocusedTarget, _current.Launch);
            SetGesture(aiming ? TutorialGesture.Tap : TutorialGesture.None);
        }

        private bool TryStartNext()
        {
            while (_queue.Count > 0)
            {
                var lesson = _queue.Dequeue();
                if (lesson.Launch != null)
                {
                    if (lesson.Launch.Launcher == null || !lesson.Launch.Launcher.CanLaunch) continue;
                    _current = lesson;
                    if (overlay != null) overlay.ShowObjective(lesson.Data.Title, lesson.Data.DeliverText, script.LaunchColor);
                    BeginAim(lesson.Launch.transform, lesson.Launch.transform.position);
                    return true;
                }

                var crate = lesson.Crate;
                if (crate == null || !crate.gameObject.activeInHierarchy) continue;
                LessonStage stage = TutorialRules.StageOf(crate.State);
                if (stage == LessonStage.Done) continue;

                _current = lesson;
                crate.StateChanged += HandleLessonCrateStateChanged;
                _shownStage = stage;
                _shownTarget = TutorialRules.TargetOf(crate.Type.Destination, crate.IsSuspicious, crate.IsScanned, crate.IsBad);
                _shownScanned = crate.IsScanned;
                ShowObjective(crate, stage);
                if (stage == LessonStage.Deliver) BeginAim(null, TargetPoint(crate, _shownTarget, false));
                else BeginAim(crate.transform, crate.transform.position);
                return true;
            }
            return false;
        }

        // The player grabbed another crate of the same kind than the one being pointed at: teach with
        // that one instead of sending them back to the belt.
        private bool TryAdoptCarriedTwin()
        {
            var carried = carrier != null ? carrier.Top : null;
            if (carried == null || carried == _current.Crate || KeyOf(carried) != _current.Data.Key) return false;
            _current.Crate.StateChanged -= HandleLessonCrateStateChanged;
            _current.Crate = carried;
            carried.StateChanged += HandleLessonCrateStateChanged;
            return true;
        }

        private static string KeyOf(Crate crate) =>
            crate.IsSuspicious ? TutorialScriptSO.SuspiciousKey : crate.Type != null ? crate.Type.Id : null;

        // A pooled crate can be launched and respawned between frames; the event catches the end for sure.
        private void HandleLessonCrateStateChanged(Crate crate, CrateState state)
        {
            if (_current != null && _current.Crate == crate && TutorialRules.StageOf(state) == LessonStage.Done)
                _current.IsFinished = true;
        }

        private void FinishLesson()
        {
            if (_current != null && _current.Crate != null) _current.Crate.StateChanged -= HandleLessonCrateStateChanged;
            _current = null;
            if (arrow != null) arrow.Hide();
            if (overlay != null)
            {
                overlay.HideObjective();
                overlay.SetGesture(TutorialGesture.None);
            }
        }

        private void ShowObjective(Crate crate, LessonStage stage)
        {
            if (overlay == null) return;
            var data = _current.Data;
            string body;
            if (crate.IsSuspicious && crate.IsScanned) body = crate.IsBad ? data.BadText : data.CleanText;
            else body = stage == LessonStage.Deliver ? data.DeliverText : data.FetchText;
            overlay.ShowObjective(data.Title, body, SwatchOf(crate));
        }

        private static Color SwatchOf(Crate crate)
        {
            var material = crate.Type != null ? crate.Type.CasingMaterial : null;
            return material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        }

        // ── Targets ────────────────────────────────────────────────────────────

        private Vector3 TargetPoint(Crate crate, LessonTarget target, bool forArrow)
        {
            switch (target)
            {
                case LessonTarget.Chute:
                    if (chute == null) break;
                    return chute.TargetPoint + Vector3.up * (forArrow ? script.ArrowAboveChute : 0f);
                case LessonTarget.Scanner:
                    if (scanner == null) break;
                    return scanner.transform.position + Vector3.up * (forArrow ? script.ArrowAboveScanner : 1f);
                default:
                    var bay = BayFor(crate.Type.Destination);
                    if (bay == null) break;
                    // Nudge aims above the pad so the pad and the arrow over the ship both stay in view.
                    return bay.transform.position + Vector3.up * (forArrow ? script.ArrowAboveBay : 1.5f);
            }
            return crate.transform.position;
        }

        private bool IsAimingAt(Crate crate, LessonTarget target)
        {
            if (interactor == null || interactor.FocusedTarget == null) return false;
            object focused = interactor.FocusedTarget;
            switch (target)
            {
                case LessonTarget.Chute: return chute != null && ReferenceEquals(focused, chute);
                case LessonTarget.Scanner: return scanner != null && ReferenceEquals(focused, scanner);
                default:
                    var bay = BayFor(crate.Type.Destination);
                    return bay != null && ReferenceEquals(focused, bay);
            }
        }

        private Bay BayFor(BayId id)
        {
            if (bays == null) return null;
            foreach (var bay in bays) if (bay != null && bay.Id == id) return bay;
            return null;
        }

        private LaunchButton LaunchButtonFor(Bay bay)
        {
            if (launchButtons == null) return null;
            foreach (var button in launchButtons)
                if (button != null && button.Launcher != null && button.Launcher.Bay == bay) return button;
            return null;
        }

        // ── Camera nudge / helpers ─────────────────────────────────────────────

        private void BeginAim(Transform follow, Vector3 point)
        {
            if (script == null || script.AimSeconds <= 0f || look == null) return;
            _aimTarget = follow;
            _aimPoint = point;
            _aimLeft = script.AimSeconds;
            if (player != null) player.IsLookSuppressed = true;
        }

        private void SetGesture(TutorialGesture gesture)
        {
            if (overlay != null) overlay.SetGesture(gesture);
        }

        private void StopAll()
        {
            FinishLesson();
            _queue.Clear();
            _cardClearLeft = 0f;
            _afterCardClear = null;
            _aimLeft = 0f;
            _aimTarget = null;
            if (player != null) player.IsLookSuppressed = false;
            if (shift != null) shift.SetHeld(false);
            if (overlay != null) overlay.HideAll();
        }
    }
}
