using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Radio
{
    /// <summary>Where the first-shift onboarding is. Off on later shifts.</summary>
    public enum TutorialPhase
    {
        Off,    // not a tutorial shift, or the shift is over
        Move,   // "use left thumb to move" — shift held
        Look,   // "drag to look around" — shift held
        Cargo,  // belt running; each new crate kind gets one guided delivery
    }

    /// <summary>
    /// Pure state of Rook's onboarding: the two gated control lessons (move a few metres, look a few
    /// degrees — the shift waits for both) and which crate kinds have already had their one-time intro.
    /// <c>TutorialDirector</c> feeds it distances, angles and spawned crates and turns its answers into
    /// arrows, cards and camera nudges, so the sequencing is testable without a scene.
    /// </summary>
    public sealed class TutorialModel
    {
        private readonly float _moveDistance;
        private readonly float _lookDegrees;
        private readonly HashSet<string> _introduced = new HashSet<string>();
        private float _accumulated;

        public TutorialModel(float moveDistance, float lookDegrees)
        {
            _moveDistance = Mathf.Max(0.01f, moveDistance);
            _lookDegrees = Mathf.Max(0.01f, lookDegrees);
        }

        public TutorialPhase Phase { get; private set; } = TutorialPhase.Off;

        /// <summary>0..1 through the current control lesson (0 outside them).</summary>
        public float Progress { get; private set; }

        /// <summary>True while the shift clock and spawner should wait for the player.</summary>
        public bool IsHoldingShift => Phase == TutorialPhase.Move || Phase == TutorialPhase.Look;

        // ── Public API ─────────────────────────────────────────────────────────

        public void Begin(bool tutorialShift)
        {
            _introduced.Clear();
            SetPhase(tutorialShift ? TutorialPhase.Move : TutorialPhase.Off);
        }

        public void End() => SetPhase(TutorialPhase.Off);

        /// <summary>Add walked distance. True on the call that finishes the move lesson.</summary>
        public bool ReportMoved(float metres) => Accumulate(TutorialPhase.Move, metres, _moveDistance, TutorialPhase.Look);

        /// <summary>Add turned angle (any direction). True on the call that finishes the look lesson.</summary>
        public bool ReportLooked(float degrees) => Accumulate(TutorialPhase.Look, degrees, _lookDegrees, TutorialPhase.Cargo);

        /// <summary>True the first time <paramref name="key"/> (a crate kind) is seen during the cargo phase.</summary>
        public bool TryClaimIntro(string key)
        {
            if (Phase != TutorialPhase.Cargo || string.IsNullOrEmpty(key)) return false;
            return _introduced.Add(key);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool Accumulate(TutorialPhase lesson, float amount, float goal, TutorialPhase next)
        {
            if (Phase != lesson) return false;
            _accumulated += Mathf.Abs(amount);
            Progress = Mathf.Clamp01(_accumulated / goal);
            if (_accumulated < goal) return false;
            SetPhase(next);
            return true;
        }

        private void SetPhase(TutorialPhase phase)
        {
            Phase = phase;
            _accumulated = 0f;
            Progress = 0f;
        }
    }
}
