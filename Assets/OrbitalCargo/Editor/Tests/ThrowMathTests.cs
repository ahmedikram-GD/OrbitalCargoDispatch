using NUnit.Framework;
using OrbitalCargo.Interaction;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class ThrowMathTests
    {
        private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

        // p(t) = p0 + v·t + ½·g·t²
        private static Vector3 PositionAt(Vector3 p0, Vector3 v, float t) => p0 + v * t + 0.5f * t * t * Gravity;

        [Test]
        public void VelocityToHit_LandsOnTargetAtFlightTime()
        {
            var start = new Vector3(-2f, 1.2f, 0.5f);
            var target = new Vector3(1.4f, 0.2f, -4.3f);

            Vector3 v = VelocityFor(start, target, 0.8f);

            Vector3 p = PositionAt(start, v, 0.8f);
            Assert.AreEqual(target.x, p.x, 1e-3f);
            Assert.AreEqual(target.y, p.y, 1e-3f);
            Assert.AreEqual(target.z, p.z, 1e-3f);
        }

        [Test]
        public void VelocityToHit_TargetBelow_StillArcsUpward()
        {
            // A short toss down into the hatch should still leave the hand rising (feels like a throw, not a drop).
            Vector3 v = VelocityFor(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 0.2f, 3f), 0.8f);

            Assert.Greater(v.y, 0f);
        }

        private static Vector3 VelocityFor(Vector3 start, Vector3 target, float t) =>
            ThrowMath.VelocityToHit(start, target, t, Gravity);
    }
}
