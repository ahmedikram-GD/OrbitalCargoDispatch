using GameKit.Architecture.Events;
using GameKit.Architecture.Variables;
using NUnit.Framework;
using OrbitalCargo.Economy;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class CreditsServiceTests
    {
        private const string TestKey = "OrbitalCargo_Tests_Credits";

        private DBInt _credits;
        private GameEvent _changed;
        private CreditsService _service;
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(TestKey);

            _credits = ScriptableObject.CreateInstance<DBInt>();
            var creditsSo = new SerializedObject(_credits);
            creditsSo.FindProperty("key").stringValue = TestKey;
            creditsSo.ApplyModifiedPropertiesWithoutUndo();
            _credits.SetValue(100);

            _changed = ScriptableObject.CreateInstance<GameEvent>();
            _changed.Subscribe(CountChanged);
            _changedCount = 0;

            _service = ScriptableObject.CreateInstance<CreditsService>();
            var so = new SerializedObject(_service);
            so.FindProperty("credits").objectReferenceValue = _credits;
            so.FindProperty("balanceChanged").objectReferenceValue = _changed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            _changed.UnSubscribe(CountChanged);
            Object.DestroyImmediate(_service);
            Object.DestroyImmediate(_changed);
            Object.DestroyImmediate(_credits);
            PlayerPrefs.DeleteKey(TestKey);
        }

        private void CountChanged() => _changedCount++;

        [Test]
        public void Add_IncreasesBalance_AndRaisesChanged()
        {
            _service.Add(60);

            Assert.AreEqual(160, _service.Balance);
            Assert.AreEqual(1, _changedCount);
        }

        [Test]
        public void Add_Deferred_IncreasesBalance_WithoutRaisingChanged()
        {
            _service.Add(60, deferDisplay: true);

            Assert.AreEqual(160, _service.Balance);
            Assert.AreEqual(0, _changedCount);
        }

        [Test]
        public void TrySpend_Insufficient_ReturnsFalse_AndKeepsBalance()
        {
            Assert.IsFalse(_service.TrySpend(150));
            Assert.AreEqual(100, _service.Balance);
            Assert.AreEqual(0, _changedCount);
        }

        [Test]
        public void TrySpend_Sufficient_Deducts_AndRaisesChanged()
        {
            Assert.IsTrue(_service.TrySpend(40));
            Assert.AreEqual(60, _service.Balance);
            Assert.AreEqual(1, _changedCount);
        }

        [Test]
        public void Add_Negative_IsAFine_AndNeverGoesBelowZero()
        {
            _service.Add(-130);

            Assert.AreEqual(0, _service.Balance);
        }
    }
}
