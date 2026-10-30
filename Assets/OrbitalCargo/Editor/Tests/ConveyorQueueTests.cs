using System.Linq;
using NUnit.Framework;
using OrbitalCargo.Stations;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class ConveyorQueueTests
    {
        private static ConveyorQueue Straight(float length = 10f, float spacing = 1f) =>
            new ConveyorQueue(new[] { Vector3.zero, new Vector3(length, 0f, 0f) }, spacing);

        [Test]
        public void Length_IsSumOfLegs()
        {
            var q = new ConveyorQueue(new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f) }, 1f);

            Assert.AreEqual(7f, q.Length, 1e-4f);
        }

        [Test]
        public void Advance_MovesItemBySpeedTimesDt()
        {
            var q = Straight();
            q.Enqueue(1);

            q.Advance(0.5f, 2f);

            Assert.AreEqual(1f, q.DistanceOf(1), 1e-4f);
        }

        [Test]
        public void Advance_ClampsAtEnd()
        {
            var q = Straight(5f);
            q.Enqueue(1);

            q.Advance(10f, 2f);

            Assert.AreEqual(5f, q.DistanceOf(1), 1e-4f);
            Assert.IsTrue(q.IsAtEnd(1));
        }

        [Test]
        public void Advance_WhenEndBlocked_StopsWithSpacing()
        {
            var q = Straight(5f, 1f);
            q.Enqueue(1);
            q.Advance(1f, 2f);   // first item at 2, entry free again
            q.Enqueue(2);

            q.Advance(10f, 2f);  // both run to the end

            Assert.AreEqual(5f, q.DistanceOf(1), 1e-4f);
            Assert.AreEqual(4f, q.DistanceOf(2), 1e-4f);
        }

        [Test]
        public void CanEnqueue_FalseWhileEntryOccupied()
        {
            var q = Straight(10f, 1f);
            q.Enqueue(1);

            Assert.IsFalse(q.CanEnqueue);
            Assert.IsFalse(q.Enqueue(2));

            q.Advance(0.6f, 2f); // item 1 moved 1.2 m > spacing

            Assert.IsTrue(q.CanEnqueue);
        }

        [Test]
        public void Enqueue_DuplicateId_ReturnsFalse()
        {
            var q = Straight();
            q.Enqueue(7);
            q.Advance(5f, 2f);

            Assert.IsFalse(q.Enqueue(7));
            Assert.AreEqual(1, q.Count);
        }

        [Test]
        public void Remove_FrontItem_LetsItemsBehindAdvance()
        {
            var q = Straight(5f, 1f);
            q.Enqueue(1);
            q.Advance(1f, 2f);
            q.Enqueue(2);
            q.Advance(10f, 2f); // 1 at 5, 2 queued at 4

            Assert.IsTrue(q.Remove(1));
            q.Advance(10f, 2f);

            Assert.AreEqual(5f, q.DistanceOf(2), 1e-4f);
            Assert.IsFalse(q.Contains(1));
        }

        [Test]
        public void Items_AreFrontFirst()
        {
            var q = Straight(10f, 1f);
            q.Enqueue(1);
            q.Advance(1f, 2f);
            q.Enqueue(2);

            CollectionAssert.AreEqual(new[] { 1, 2 }, q.Items.ToArray());
        }

        [Test]
        public void PositionOf_FollowsCornerWaypoints()
        {
            // L-shaped path: 4 m along +X, then 3 m along +Z.
            var q = new ConveyorQueue(new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f) }, 1f);
            q.Enqueue(1);

            q.Advance(5f, 1f); // 5 m: 1 m into the second leg

            Vector3 p = q.PositionOf(1);
            Assert.AreEqual(4f, p.x, 1e-4f);
            Assert.AreEqual(1f, p.z, 1e-4f);
            Assert.AreEqual(Vector3.forward, q.ForwardOf(1));
        }
    }
}
