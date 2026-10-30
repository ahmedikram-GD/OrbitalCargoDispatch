using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalCargo.Config;
using OrbitalCargo.Stations;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class SpawnSchedulerTests
    {
        private CrateTypeSO _standard;
        private CrateTypeSO _cryo;

        [SetUp]
        public void SetUp()
        {
            _standard = ScriptableObject.CreateInstance<CrateTypeSO>();
            _cryo = ScriptableObject.CreateInstance<CrateTypeSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_standard);
            Object.DestroyImmediate(_cryo);
        }

        private SpawnEntry Entry(float time, CrateTypeSO type, int count = 1, float interval = 2f, string beat = null) =>
            new SpawnEntry { Time = time, Type = type, Count = count, Interval = interval, RadioBeat = beat };

        [Test]
        public void Advance_ReturnsEntriesUpToElapsed_Once()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(5f, _standard), Entry(20f, _cryo) });
            var due = new List<SpawnRequest>();

            s.Advance(6f, due);
            Assert.AreEqual(1, due.Count);
            Assert.AreSame(_standard, due[0].Type);

            due.Clear();
            s.Advance(10f, due);
            Assert.AreEqual(0, due.Count, "already-released spawns must not repeat");
        }

        [Test]
        public void Advance_ExpandsCountWithInterval()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(10f, _standard, count: 3, interval: 2f) });
            var due = new List<SpawnRequest>();

            s.Advance(13f, due);

            CollectionAssert.AreEqual(new[] { 10f, 12f }, due.Select(d => d.Time).ToArray());
            due.Clear();
            s.Advance(14f, due);
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(14f, due[0].Time, 1e-4f);
        }

        [Test]
        public void Advance_SameTimestamp_ReturnsBoth()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(5f, _standard), Entry(5f, _cryo) });
            var due = new List<SpawnRequest>();

            s.Advance(5f, due);

            Assert.AreEqual(2, due.Count);
        }

        [Test]
        public void Advance_UnsortedEntries_ReleasedInTimeOrder()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(30f, _cryo), Entry(5f, _standard) });
            var due = new List<SpawnRequest>();

            s.Advance(60f, due);

            Assert.AreSame(_standard, due[0].Type);
            Assert.AreSame(_cryo, due[1].Type);
        }

        [Test]
        public void RadioBeat_OnlyOnFirstCrateOfEntry()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(0f, _cryo, count: 3, beat: "CryoIntro") });
            var due = new List<SpawnRequest>();

            s.Advance(100f, due);

            Assert.AreEqual("CryoIntro", due[0].RadioBeat);
            Assert.IsTrue(string.IsNullOrEmpty(due[1].RadioBeat));
            Assert.IsTrue(string.IsNullOrEmpty(due[2].RadioBeat));
        }

        [Test]
        public void IsExhausted_AfterLastSpawn()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(1f, _standard, count: 2, interval: 1f) });
            var due = new List<SpawnRequest>();

            Assert.AreEqual(2, s.Total);
            s.Advance(1.5f, due);
            Assert.IsFalse(s.IsExhausted);
            s.Advance(2f, due);
            Assert.IsTrue(s.IsExhausted);
        }

        [Test]
        public void NullTypeEntries_AreSkipped()
        {
            var s = new SpawnScheduler(new List<SpawnEntry> { Entry(1f, null), Entry(2f, _standard) });
            var due = new List<SpawnRequest>();

            s.Advance(10f, due);

            Assert.AreEqual(1, due.Count);
        }
    }
}
