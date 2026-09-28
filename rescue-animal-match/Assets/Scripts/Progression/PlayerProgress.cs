using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Serializable snapshot of a player's long-term progression.
    /// Persisted to PlayerPrefs (JSON) and mirrored to Firestore users/{uid}/progress.
    /// </summary>
    [Serializable]
    public class PlayerProgress
    {
        public int highestLevelUnlocked = 1;
        public int totalStars = 0;
        public List<int> completedLevels = new List<int>();
        public Dictionary<string, int> starsByLevel = new Dictionary<string, int>();
        public List<string> rescuedAnimals = new List<string>();

        public bool IsCompleted(int levelNumber) => completedLevels.Contains(levelNumber);

        public int StarsFor(int levelNumber)
            => starsByLevel.TryGetValue("L" + levelNumber, out var s) ? s : 0;

        public void RecordCompletion(int levelNumber, int stars, IEnumerable<string> animals)
        {
            if (!IsCompleted(levelNumber))
            {
                totalStars += stars;
                completedLevels.Add(levelNumber);
            }
            else if (stars > StarsFor(levelNumber))
            {
                // Replay: only the star delta counts toward the total.
                totalStars += stars - StarsFor(levelNumber);
            }

            starsByLevel["L" + levelNumber] = Math.Max(stars, StarsFor(levelNumber));

            if (animals != null)
                foreach (var a in animals)
                    if (!string.IsNullOrEmpty(a) && !rescuedAnimals.Contains(a))
                        rescuedAnimals.Add(a);

            if (levelNumber + 1 > highestLevelUnlocked)
                highestLevelUnlocked = levelNumber + 1;
        }
    }
}
