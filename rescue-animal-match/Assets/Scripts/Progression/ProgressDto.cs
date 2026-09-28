using System;
using System.Collections.Generic;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// JsonUtility-friendly DTO (JsonUtility cannot serialize Dictionary&lt;,&gt;).
    /// Maps to/from <see cref="PlayerProgress"/>.
    /// </summary>
    [Serializable]
    public class ProgressDto
    {
        [Serializable] public class StarEntry { public string level; public int stars; }

        public int highestLevelUnlocked;
        public int totalStars;
        public List<int> completedLevels = new List<int>();
        public List<StarEntry> stars = new List<StarEntry>();
        public List<string> rescuedAnimals = new List<string>();

        public static ProgressDto FromDomain(PlayerProgress p)
        {
            var dto = new ProgressDto
            {
                highestLevelUnlocked = p.highestLevelUnlocked,
                totalStars = p.totalStars,
                completedLevels = new List<int>(p.completedLevels),
                rescuedAnimals = new List<string>(p.rescuedAnimals)
            };
            foreach (var kv in p.starsByLevel)
                dto.stars.Add(new StarEntry { level = kv.Key, stars = kv.Value });
            return dto;
        }

        public PlayerProgress ToDomain()
        {
            var p = new PlayerProgress
            {
                highestLevelUnlocked = highestLevelUnlocked,
                totalStars = totalStars,
                completedLevels = new List<int>(completedLevels ?? new List<int>()),
                rescuedAnimals = new List<string>(rescuedAnimals ?? new List<string>())
            };
            foreach (var e in stars ?? new List<StarEntry>())
                if (e != null && !string.IsNullOrEmpty(e.level))
                    p.starsByLevel[e.level] = e.stars;
            return p;
        }
    }
}
