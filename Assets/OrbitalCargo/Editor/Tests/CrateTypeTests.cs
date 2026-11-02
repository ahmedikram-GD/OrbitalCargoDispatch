using NUnit.Framework;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class CrateTypeTests
    {
        private CrateTypeSO _type;

        [SetUp]
        public void SetUp() => _type = ScriptableObject.CreateInstance<CrateTypeSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_type);

        [Test]
        public void BelongsIn_OwnBay_ReturnsTrue()
        {
            _type.Destination = BayId.B;

            Assert.IsTrue(_type.BelongsIn(BayId.B));
        }

        [Test]
        public void BelongsIn_OtherBay_ReturnsFalse()
        {
            _type.Destination = BayId.B;

            Assert.IsFalse(_type.BelongsIn(BayId.A));
        }
    }
}
