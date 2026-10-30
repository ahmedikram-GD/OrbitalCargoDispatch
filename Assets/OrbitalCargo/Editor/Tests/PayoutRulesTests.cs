using System.Collections.Generic;
using NUnit.Framework;
using OrbitalCargo.Config;
using OrbitalCargo.Economy;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class PayoutRulesTests
    {
        private ShiftConfigSO _cfg;

        [SetUp]
        public void SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<ShiftConfigSO>();
            _cfg.ExpiredCryoMultiplier = 0.4f;
            _cfg.WrongBayMultiplier = 0.5f;
            _cfg.UnscannedBadFine = 30;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cfg);

        private static CrateSnapshot Crate(int payout, BayId dest, bool expired = false, bool bad = false, bool scanned = false) =>
            new CrateSnapshot { BasePayout = payout, Destination = dest, CryoExpired = expired, IsBad = bad, IsScanned = scanned };

        [Test]
        public void CratePayout_Normal_ReturnsBase()
        {
            Assert.AreEqual(10, PayoutRules.CratePayout(10, false, false, _cfg));
        }

        [Test]
        public void CratePayout_CryoExpired_AppliesMultiplier()
        {
            Assert.AreEqual(6, PayoutRules.CratePayout(15, true, false, _cfg));
        }

        [Test]
        public void CratePayout_WrongBay_AppliesMultiplier()
        {
            Assert.AreEqual(5, PayoutRules.CratePayout(10, false, true, _cfg));
        }

        [Test]
        public void CratePayout_ExpiredAndWrongBay_StacksMultipliers()
        {
            Assert.AreEqual(3, PayoutRules.CratePayout(15, true, true, _cfg));
        }

        [Test]
        public void Launch_SixStandardInBayA_SumsPayout()
        {
            var crates = new List<CrateSnapshot>();
            for (int i = 0; i < 6; i++) crates.Add(Crate(10, BayId.A));

            LaunchResult r = PayoutRules.Launch(crates, BayId.A, _cfg);

            Assert.AreEqual(60, r.Credits);
            Assert.AreEqual(6, r.CrateCount);
            Assert.AreEqual(0, r.FineApplied);
            Assert.AreEqual(BayId.A, r.Bay);
        }

        [Test]
        public void Launch_CountsWrongBayCrates()
        {
            var crates = new List<CrateSnapshot> { Crate(10, BayId.A), Crate(15, BayId.B), Crate(20, BayId.C) };

            LaunchResult r = PayoutRules.Launch(crates, BayId.A, _cfg);

            Assert.AreEqual(2, r.WrongBayCount);
            Assert.AreEqual(10 + 8 + 10, r.Credits); // 15*0.5 = 7.5 rounds to 8
        }

        [Test]
        public void Launch_UnscannedBad_AppliesFineOnce()
        {
            var crates = new List<CrateSnapshot> { Crate(10, BayId.A, bad: true), Crate(10, BayId.A, bad: true), Crate(10, BayId.A) };

            LaunchResult r = PayoutRules.Launch(crates, BayId.A, _cfg);

            Assert.AreEqual(30, r.FineApplied);
            Assert.AreEqual(2, r.BadShipped);
        }

        [Test]
        public void Launch_ScannedClean_NoFine()
        {
            var crates = new List<CrateSnapshot> { Crate(10, BayId.A, bad: false, scanned: true), Crate(10, BayId.A) };

            LaunchResult r = PayoutRules.Launch(crates, BayId.A, _cfg);

            Assert.AreEqual(0, r.FineApplied);
            Assert.AreEqual(0, r.BadShipped);
            Assert.AreEqual(20, r.Credits);
        }

        [Test]
        public void Launch_Empty_PaysNothing()
        {
            LaunchResult r = PayoutRules.Launch(new List<CrateSnapshot>(), BayId.B, _cfg);

            Assert.AreEqual(0, r.Credits);
            Assert.AreEqual(0, r.CrateCount);
        }
    }
}
