using NUnit.Framework;
using OrbitalCargo.Shift;

namespace OrbitalCargo.Tests
{
    public class ShiftClockTests
    {
        [Test]
        public void Tick_AccumulatesElapsed()
        {
            var clock = new ShiftClock(300f);

            clock.Tick(1.5f);
            clock.Tick(2f);

            Assert.AreEqual(3.5f, clock.Elapsed, 1e-4f);
            Assert.AreEqual(296.5f, clock.Remaining, 1e-4f);
            Assert.IsFalse(clock.IsOver);
        }

        [Test]
        public void Tick_ReturnsTrueOnlyOnFrameItEnds()
        {
            var clock = new ShiftClock(1f);

            Assert.IsFalse(clock.Tick(0.6f));
            Assert.IsTrue(clock.Tick(0.6f));
            Assert.IsFalse(clock.Tick(0.6f));
            Assert.IsTrue(clock.IsOver);
        }

        [Test]
        public void Remaining_ClampsAtZero()
        {
            var clock = new ShiftClock(2f);

            clock.Tick(5f);

            Assert.AreEqual(0f, clock.Remaining, 1e-4f);
            Assert.AreEqual(2f, clock.Elapsed, 1e-4f);
        }

        [Test]
        public void Reset_RestartsWithNewLength()
        {
            var clock = new ShiftClock(1f);
            clock.Tick(2f);

            clock.Reset(60f);

            Assert.IsFalse(clock.IsOver);
            Assert.AreEqual(0f, clock.Elapsed, 1e-4f);
            Assert.AreEqual(60f, clock.Remaining, 1e-4f);
        }
    }
}
