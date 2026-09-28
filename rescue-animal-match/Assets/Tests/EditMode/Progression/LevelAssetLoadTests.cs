using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Progression;

namespace RescueAnimalMatch.Tests.EditMode.Progression
{
    /// <summary>
    /// Loads every generated asset in Resources/Levels and asserts the structural
    /// contract required by the spec: movesLimit > 0, objectives non-empty, valid
    /// star thresholds, unique level numbers covering 1..30.
    /// </summary>
    public class LevelAssetLoadTests
    {
        [Test]
        public void AllLevelAssets_LoadFromResources_WithValidData()
        {
            var assets = Resources.LoadAll<LevelData>("Levels");
            Assert.GreaterOrEqual(assets.Length, 30,
                "Expected at least 30 level assets in Resources/Levels");

            var seen = new HashSet<int>();
            foreach (var lvl in assets)
            {
                Assert.NotNull(lvl, "Null asset entry");
                Assert.Greater(lvl.MovesLimit, 0, $"Level {lvl.LevelNumber}: movesLimit must be > 0");
                Assert.Greater(lvl.Objectives.Length, 0, $"Level {lvl.LevelNumber}: needs >=1 objective");
                foreach (var o in lvl.Objectives)
                    Assert.Greater(o.TargetAmount, 0, $"Level {lvl.LevelNumber}: objective target must be > 0");
                Assert.IsTrue(lvl.IsValid(), $"Level {lvl.LevelNumber} failed IsValid()");
                Assert.IsTrue(seen.Add(lvl.LevelNumber), $"Duplicate level number {lvl.LevelNumber}");
            }

            for (int n = 1; n <= 30; n++)
                Assert.IsTrue(seen.Contains(n), $"Missing level {n}");
        }

        [Test]
        public void DifficultyCurve_MatchesSpec()
        {
            // L1-10 easy: exactly 1 objective; L11-20 medium: 2; L21-30 hard: >=2.
            for (int n = 1; n <= 30; n++)
            {
                if (!LevelDatabase.TryGet(n, out var lvl)) continue; // CI without imported assets
                int objs = lvl.Objectives.Length;
                if (n <= 10) Assert.AreEqual(1, objs, $"L{n} easy should have 1 objective");
                else if (n <= 20) Assert.AreEqual(2, objs, $"L{n} medium should have 2 objectives");
                else Assert.GreaterOrEqual(objs, 2, $"L{n} hard should have >=2 objectives");
            }
        }

        [Test]
        public void StarThresholds_AreMonotonicAcrossAllLevels()
        {
            foreach (var lvl in LevelDatabase.All.Values)
            {
                Assert.LessOrEqual(lvl.StarThreshold1, lvl.StarThreshold2, $"L{lvl.LevelNumber} t1<=t2");
                Assert.LessOrEqual(lvl.StarThreshold2, lvl.StarThreshold3, $"L{lvl.LevelNumber} t2<=t3");
            }
        }
    }
}
