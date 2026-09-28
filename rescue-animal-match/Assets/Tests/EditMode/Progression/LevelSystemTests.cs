using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Progression;

namespace RescueAnimalMatch.Tests.EditMode.Progression
{
    /// <summary>
    /// EditMode tests for the level system: LevelData validation, PlayerProgress
    /// bookkeeping, ProgressionMap unlock rule and the ProgressDto round-trip.
    /// </summary>
    public class LevelSystemTests
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private static void Set(LevelData lvl, string field, object value)
            => typeof(LevelData).GetField(field, Priv).SetValue(lvl, value);

        private static LevelData MakeLevel(int num, int moves, int stars1,
            params (ObjectiveType t, int amount)[] objs)
        {
            var lvl = ScriptableObject.CreateInstance<LevelData>();
            Set(lvl, "levelNumber", num);
            Set(lvl, "levelName", "Test " + num);
            Set(lvl, "movesLimit", moves);
            Set(lvl, "starThreshold1", stars1);
            Set(lvl, "starThreshold2", (int)(stars1 * 1.6f));
            Set(lvl, "starThreshold3", (int)(stars1 * 2.4f));
            Set(lvl, "huellasReward", 50);

            var list = new List<LevelObjective>();
            foreach (var o in objs) list.Add(new LevelObjective(o.t, o.amount));
            Set(lvl, "objectives", list.ToArray());
            return lvl;
        }

        [Test]
        public void LevelData_ValidWhenAllFieldsPositive()
        {
            var lvl = MakeLevel(1, 20, 1000, (ObjectiveType.RescueAnimals, 10));
            Assert.IsTrue(lvl.IsValid());
            Assert.AreEqual(20, lvl.MovesLimit);
            Assert.AreEqual(1, lvl.Objectives.Length);
        }

        [Test]
        public void LevelData_InvalidWhenMovesZeroOrObjectivesEmpty()
        {
            var noMoves = MakeLevel(2, 0, 1000, (ObjectiveType.FeedAnimals, 5));
            Assert.IsFalse(noMoves.IsValid(), "movesLimit must be > 0");

            var noObjs = MakeLevel(3, 10, 1000);
            Assert.IsFalse(noObjs.IsValid(), "objectives must be non-empty");
        }

        [Test]
        public void LevelData_StarsForScore_UsesThreeThresholds()
        {
            var lvl = MakeLevel(4, 20, 1000, (ObjectiveType.ClearDebris, 5));
            Assert.AreEqual(0, lvl.StarsForScore(999));
            Assert.AreEqual(1, lvl.StarsForScore(1000));
            Assert.AreEqual(2, lvl.StarsForScore((int)(1000 * 1.6f)));
            Assert.AreEqual(3, lvl.StarsForScore((int)(1000 * 2.4f)));
            Assert.AreEqual(3, lvl.StarsForScore(999999));
        }

        [Test]
        public void PlayerProgress_RecordCompletion_UnlocksNextAndCountsStars()
        {
            var p = new PlayerProgress();
            p.RecordCompletion(1, 3, new[] { "perro_max" });
            Assert.AreEqual(2, p.highestLevelUnlocked);
            Assert.AreEqual(3, p.totalStars);
            Assert.IsTrue(p.IsCompleted(1));
            Assert.AreEqual(3, p.StarsFor(1));
            CollectionAssert.Contains(p.rescuedAnimals, "perro_max");
        }

        [Test]
        public void PlayerProgress_ReplayOnlyAddsStarDelta()
        {
            var p = new PlayerProgress();
            p.RecordCompletion(1, 1, null);
            p.RecordCompletion(1, 3, null); // improve 1 -> 3
            Assert.AreEqual(3, p.totalStars, "delta 2 added once, not double-counted");
            Assert.AreEqual(3, p.StarsFor(1));
            Assert.AreEqual(1, p.completedLevels.Count);
        }

        [Test]
        public void ProgressionMap_LevelNUnlockedOnlyIfNMinusOneHasStar()
        {
            var p = new PlayerProgress();
            var states = ProgressionMap.ComputeStates(p, 5);
            Assert.AreEqual(NodeState.Unlocked, states[1], "level 1 always unlocked");
            Assert.AreEqual(NodeState.Locked, states[2]);

            p.RecordCompletion(1, 1, null);
            states = ProgressionMap.ComputeStates(p, 5);
            Assert.AreEqual(NodeState.Completed, states[1]);
            Assert.AreEqual(NodeState.Unlocked, states[2], "2 unlocked after 1 star");
            Assert.AreEqual(NodeState.Locked, states[3]);
        }

        [Test]
        public void ProgressDto_RoundTripsThroughJsonUtility()
        {
            var p = new PlayerProgress();
            p.RecordCompletion(1, 2, new[] { "gata_luna", "loro_pepe" });
            p.RecordCompletion(2, 3, new[] { "conejo_nube" });

            string json = JsonUtility.ToJson(ProgressDto.FromDomain(p));
            var back = JsonUtility.FromJson<ProgressDto>(json).ToDomain();

            Assert.AreEqual(p.highestLevelUnlocked, back.highestLevelUnlocked);
            Assert.AreEqual(p.totalStars, back.totalStars);
            Assert.AreEqual(p.StarsFor(1), back.StarsFor(1));
            Assert.AreEqual(p.StarsFor(2), back.StarsFor(2));
            CollectionAssert.AreEquivalent(p.rescuedAnimals, back.rescuedAnimals);
            CollectionAssert.AreEquivalent(p.completedLevels, back.completedLevels);
        }
    }
}
