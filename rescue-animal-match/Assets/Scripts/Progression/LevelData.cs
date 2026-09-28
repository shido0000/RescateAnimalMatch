using System;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Difficulty bucket used by the generator and the world map.
    /// </summary>
    public enum LevelDifficulty
    {
        Easy = 0,     // levels 1-10 (tutorial)
        Medium = 1,   // levels 11-20
        Hard = 2      // levels 21-30
    }

    /// <summary>
    /// ScriptableObject describing one match-3 level.
    /// 30 assets live in Resources/Levels/Level_XX.asset (see tools/generate_levels.py).
    /// Loaded via <see cref="LevelDatabase"/> (Resources.LoadAll) so it works in EditMode tests.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Rescate Animal Match/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelName = "Primer Rescate";
        [SerializeField] private LevelDifficulty difficulty = LevelDifficulty.Easy;

        [Header("Rules")]
        [SerializeField] private int movesLimit = 20;
        [SerializeField] private LevelObjective[] objectives = Array.Empty<LevelObjective>();

        [Header("Scoring")]
        [SerializeField] private int starThreshold1 = 1000;
        [SerializeField] private int starThreshold2 = 2000;
        [SerializeField] private int starThreshold3 = 3000;

        [Header("Rewards")]
        [SerializeField] private string[] animalRewards = Array.Empty<string>();
        [SerializeField] private int huellasReward = 50;

        public int LevelNumber => levelNumber;
        public string LevelName => levelName;
        public LevelDifficulty Difficulty => difficulty;
        public int MovesLimit => movesLimit;
        public LevelObjective[] Objectives => objectives ?? Array.Empty<LevelObjective>();
        public int StarThreshold1 => starThreshold1;
        public int StarThreshold2 => starThreshold2;
        public int StarThreshold3 => starThreshold3;
        public string[] AnimalRewards => animalRewards ?? Array.Empty<string>();
        public int HuellasReward => huellasReward;

        /// <summary>Star thresholds ordered ascending; used by StarsForScore.</summary>
        public int[] StarThresholds => new[] { starThreshold1, starThreshold2, starThreshold3 };

        /// <summary>1..3 stars for a final score, 0 if below threshold 1.</summary>
        public int StarsForScore(int score)
        {
            if (score >= starThreshold3) return 3;
            if (score >= starThreshold2) return 2;
            if (score >= starThreshold1) return 1;
            return 0;
        }

        /// <summary>Structural validation used by asset-loading tests and CI.</summary>
        public bool IsValid()
        {
            if (levelNumber < 1) return false;
            if (string.IsNullOrEmpty(levelName)) return false;
            if (movesLimit <= 0) return false;
            if (objectives == null || objectives.Length == 0) return false;
            foreach (var o in objectives)
                if (!o.IsValid) return false;
            if (starThreshold1 <= 0) return false;
            if (starThreshold2 < starThreshold1) return false;
            if (starThreshold3 < starThreshold2) return false;
            return true;
        }
    }
}
