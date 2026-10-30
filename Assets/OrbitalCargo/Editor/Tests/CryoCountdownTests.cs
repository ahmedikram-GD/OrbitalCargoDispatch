using NUnit.Framework;
using OrbitalCargo.Crates;

namespace OrbitalCargo.Tests
{
    public class CryoCountdownTests
    {
        [Test]
        public void Reset_StartsFull()
        {
            var c = new CryoCountdown();
            c.Reset(45f);

            Assert.AreEqual(45f, c.Remaining, 1e-4f);
            Assert.AreEqual(1f, c.Normalized, 1e-4f);
            Assert.IsFalse(c.Expired);
        }

        [Test]
        public void Tick_Running_CountsDown()
        {
            var c = new CryoCountdown();
            c.Reset(10f);

            c.Tick(2.5f, true);

            Assert.AreEqual(7.5f, c.Remaining, 1e-4f);
            Assert.AreEqual(0.75f, c.Normalized, 1e-4f);
        }

        [Test]
        public void Tick_NotRunning_Freezes()
        {
            var c = new CryoCountdown();
            c.Reset(10f);

            c.Tick(5f, false);

            Assert.AreEqual(10f, c.Remaining, 1e-4f);
        }

        [Test]
        public void Tick_ReturnsTrueOnlyOnTheExpiringTick()
        {
            var c = new CryoCountdown();
            c.Reset(1f);

            Assert.IsFalse(c.Tick(0.6f, true));
            Assert.IsTrue(c.Tick(0.6f, true));
            Assert.IsFalse(c.Tick(0.6f, true));
            Assert.IsTrue(c.Expired);
            Assert.AreEqual(0f, c.Remaining, 1e-4f);
        }
    }
}
