using System;
using System.Linq;
using NUnit.Framework;
using OrbitalCargo.Stations;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class BayModelTests
    {
        // A 2×3 pad like the real bays: front row z = -0.55, back row z = +0.55, x = -1.1 / 0 / +1.1.
        private static readonly Vector3[] Slots =
        {
            new Vector3(-1.1f, 0f, -0.55f), new Vector3(0f, 0f, -0.55f), new Vector3(1.1f, 0f, -0.55f),
            new Vector3(-1.1f, 0f,  0.55f), new Vector3(0f, 0f,  0.55f), new Vector3(1.1f, 0f,  0.55f),
        };

        [Test]
        public void NewBay_IsEmpty_NotFull()
        {
            var bay = new BayModel(6);

            Assert.IsTrue(bay.IsEmpty);
            Assert.IsFalse(bay.IsFull);
            Assert.AreEqual(0, bay.Count);
        }

        [Test]
        public void Occupy_SixSlots_IsFull()
        {
            var bay = new BayModel(6);
            for (int i = 0; i < 6; i++) bay.Occupy(i, 100 + i);

            Assert.IsTrue(bay.IsFull);
            Assert.AreEqual(6, bay.Count);
        }

        [Test]
        public void TryGetNearestFreeSlot_ReturnsClosestSlot()
        {
            var bay = new BayModel(6);

            Assert.IsTrue(bay.TryGetNearestFreeSlot(new Vector3(1.0f, 0f, 0.5f), Slots, out int slot));
            Assert.AreEqual(5, slot);
        }

        [Test]
        public void TryGetNearestFreeSlot_SkipsOccupied_ReturnsNextClosest()
        {
            var bay = new BayModel(6);
            bay.Occupy(5, 42);

            // Right-hand column, nearer the back row: slot 5 is closest but taken, slot 2 is next.
            Assert.IsTrue(bay.TryGetNearestFreeSlot(new Vector3(1.1f, 0f, 0.2f), Slots, out int slot));
            Assert.AreEqual(2, slot);
        }

        [Test]
        public void TryGetNearestFreeSlot_WhenFull_ReturnsFalse()
        {
            var bay = new BayModel(6);
            for (int i = 0; i < 6; i++) bay.Occupy(i, i);

            Assert.IsFalse(bay.TryGetNearestFreeSlot(Vector3.zero, Slots, out _));
        }

        [Test]
        public void Occupy_SameSlotTwice_Throws()
        {
            var bay = new BayModel(6);
            bay.Occupy(0, 1);

            Assert.Throws<InvalidOperationException>(() => bay.Occupy(0, 2));
        }

        [Test]
        public void TryRelease_OccupiedSlot_ReturnsCrateAndFreesSlot()
        {
            var bay = new BayModel(6);
            bay.Occupy(3, 77);

            Assert.IsTrue(bay.TryRelease(3, out int crateId));
            Assert.AreEqual(77, crateId);
            Assert.IsTrue(bay.IsEmpty);
        }

        [Test]
        public void OccupiedCrateIds_ListsEveryOccupant()
        {
            var bay = new BayModel(6);
            bay.Occupy(1, 10);
            bay.Occupy(4, 40);

            CollectionAssert.AreEquivalent(new[] { 10, 40 }, bay.OccupiedCrateIds.ToArray());
        }

        [Test]
        public void Clear_ResetsCount()
        {
            var bay = new BayModel(6);
            bay.Occupy(0, 1);
            bay.Occupy(1, 2);

            bay.Clear();

            Assert.IsTrue(bay.IsEmpty);
            Assert.IsTrue(bay.TryGetNearestFreeSlot(Slots[0], Slots, out int slot));
            Assert.AreEqual(0, slot);
        }
    }
}
