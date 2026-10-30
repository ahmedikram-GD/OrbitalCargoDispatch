using NUnit.Framework;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class RoomLayoutTests
    {
        private RoomLayoutSO _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = ScriptableObject.CreateInstance<RoomLayoutSO>();
            _layout.SlotColumns = 3;
            _layout.SlotRows = 2;
            _layout.SlotSpacing = 1.1f;
            _layout.PadHeight = 0.1f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_layout);

        [Test]
        public void GetSlotLocalPositions_TwoByThree_ReturnsSixSlots()
        {
            Assert.AreEqual(6, _layout.GetSlotLocalPositions().Length);
        }

        [Test]
        public void GetSlotLocalPositions_TwoByThree_AreCentredOnPad()
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in _layout.GetSlotLocalPositions()) sum += p;

            Assert.AreEqual(0f, sum.x, 1e-4f);
            Assert.AreEqual(0f, sum.z, 1e-4f);
        }

        [Test]
        public void GetSlotLocalPositions_FirstSlot_IsFrontLeftAtPadHeight()
        {
            Vector3 first = _layout.GetSlotLocalPositions()[0];

            Assert.AreEqual(-1.1f, first.x, 1e-4f);
            Assert.AreEqual(0.1f, first.y, 1e-4f);
            Assert.AreEqual(-0.55f, first.z, 1e-4f);
        }

        [Test]
        public void GetSlotLocalPositions_FourthSlot_StartsSecondRow()
        {
            Vector3 fourth = _layout.GetSlotLocalPositions()[3];

            Assert.AreEqual(-1.1f, fourth.x, 1e-4f);
            Assert.AreEqual(0.55f, fourth.z, 1e-4f);
        }
    }
}
