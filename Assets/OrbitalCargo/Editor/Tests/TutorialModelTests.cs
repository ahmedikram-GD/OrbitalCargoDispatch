using NUnit.Framework;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Radio;

namespace OrbitalCargo.Tests
{
    public class TutorialModelTests
    {
        private TutorialModel _model;

        [SetUp]
        public void SetUp() => _model = new TutorialModel(2f, 90f);

        // ── Phases ─────────────────────────────────────────────────────────────

        [Test]
        public void Begin_TutorialShift_StartsOnMove()
        {
            _model.Begin(true);

            Assert.AreEqual(TutorialPhase.Move, _model.Phase);
        }

        [Test]
        public void Begin_LaterShift_IsOff()
        {
            _model.Begin(false);

            Assert.AreEqual(TutorialPhase.Off, _model.Phase);
        }

        [Test]
        public void ReportMoved_AccumulatesUntilDistance_ThenAdvancesToLook()
        {
            _model.Begin(true);

            Assert.IsFalse(_model.ReportMoved(1.2f));
            Assert.AreEqual(0.6f, _model.Progress, 1e-4f);
            Assert.IsTrue(_model.ReportMoved(1f));

            Assert.AreEqual(TutorialPhase.Look, _model.Phase);
            Assert.AreEqual(0f, _model.Progress, 1e-4f);
        }

        [Test]
        public void ReportLooked_WhileOnMove_IsIgnored()
        {
            _model.Begin(true);

            Assert.IsFalse(_model.ReportLooked(500f));
            Assert.AreEqual(TutorialPhase.Move, _model.Phase);
        }

        [Test]
        public void ReportLooked_AccumulatesUntilDegrees_ThenAdvancesToCargo()
        {
            _model.Begin(true);
            _model.ReportMoved(5f);

            Assert.IsFalse(_model.ReportLooked(60f));
            Assert.IsTrue(_model.ReportLooked(-40f)); // looking back the other way still counts

            Assert.AreEqual(TutorialPhase.Cargo, _model.Phase);
        }

        [Test]
        public void IsHoldingShift_OnlyDuringControlLessons()
        {
            _model.Begin(true);
            Assert.IsTrue(_model.IsHoldingShift);

            _model.ReportMoved(5f);
            Assert.IsTrue(_model.IsHoldingShift);

            _model.ReportLooked(200f);
            Assert.IsFalse(_model.IsHoldingShift);
        }

        [Test]
        public void End_TurnsOff()
        {
            _model.Begin(true);

            _model.End();

            Assert.AreEqual(TutorialPhase.Off, _model.Phase);
            Assert.IsFalse(_model.IsHoldingShift);
        }

        // ── Crate intros ───────────────────────────────────────────────────────

        [Test]
        public void TryClaimIntro_FirstTimeInCargo_ReturnsTrueOnce()
        {
            ReachCargo();

            Assert.IsTrue(_model.TryClaimIntro("cryo"));
            Assert.IsFalse(_model.TryClaimIntro("cryo"));
            Assert.IsTrue(_model.TryClaimIntro("junk"));
        }

        [Test]
        public void TryClaimIntro_WhenOff_ReturnsFalse()
        {
            _model.Begin(false);

            Assert.IsFalse(_model.TryClaimIntro("standard"));
        }

        [Test]
        public void TryClaimIntro_EmptyKey_ReturnsFalse()
        {
            ReachCargo();

            Assert.IsFalse(_model.TryClaimIntro(string.Empty));
            Assert.IsFalse(_model.TryClaimIntro(null));
        }

        [Test]
        public void Begin_Again_ForgetsIntroducedCrates()
        {
            ReachCargo();
            _model.TryClaimIntro("standard");

            ReachCargo();

            Assert.IsTrue(_model.TryClaimIntro("standard"));
        }

        // ── Lesson rules ───────────────────────────────────────────────────────

        [TestCase(CrateState.OnConveyor, LessonStage.Fetch)]
        [TestCase(CrateState.Pickable, LessonStage.Fetch)]
        [TestCase(CrateState.Carried, LessonStage.Deliver)]
        [TestCase(CrateState.Scanning, LessonStage.Wait)]
        [TestCase(CrateState.Thrown, LessonStage.Wait)]
        [TestCase(CrateState.Slotted, LessonStage.Done)]
        [TestCase(CrateState.Consumed, LessonStage.Done)]
        public void StageOf_MapsCrateState(CrateState state, LessonStage expected)
        {
            Assert.AreEqual(expected, TutorialRules.StageOf(state));
        }

        [Test]
        public void TargetOf_PlainCrate_IsItsBay()
        {
            Assert.AreEqual(LessonTarget.Bay, TutorialRules.TargetOf(BayId.B, false, false, false));
        }

        [Test]
        public void TargetOf_Junk_IsChute()
        {
            Assert.AreEqual(LessonTarget.Chute, TutorialRules.TargetOf(BayId.Chute, false, false, false));
        }

        [Test]
        public void TargetOf_SuspiciousUnscanned_IsScanner()
        {
            Assert.AreEqual(LessonTarget.Scanner, TutorialRules.TargetOf(BayId.A, true, false, true));
        }

        [Test]
        public void TargetOf_ScannedContraband_IsChute()
        {
            Assert.AreEqual(LessonTarget.Chute, TutorialRules.TargetOf(BayId.A, true, true, true));
        }

        [Test]
        public void TargetOf_ScannedClean_IsItsBay()
        {
            Assert.AreEqual(LessonTarget.Bay, TutorialRules.TargetOf(BayId.A, true, true, false));
        }

        private void ReachCargo()
        {
            _model.Begin(true);
            _model.ReportMoved(5f);
            _model.ReportLooked(200f);
        }
    }
}
