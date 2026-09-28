using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Loads and indexes every LevelData asset in Resources/Levels/.
    /// Uses Resources.LoadAll so it works in both EditMode and PlayMode.
    /// </summary>
    public static class LevelDatabase
    {
        public const string LevelsResourcePath = "Levels";

        private static Dictionary<int, LevelData> _cache;

        /// <summary>All valid level assets keyed by levelNumber.</summary>
        public static IReadOnlyDictionary<int, LevelData> All
        {
            get
            {
                if (_cache == null) Load();
                return _cache;
            }
        }

        /// <summary>Force a reload (used by tests after touching the database).</summary>
        public static void Load()
        {
            _cache = new Dictionary<int, LevelData>();
            var assets = Resources.LoadAll<LevelData>(LevelsResourcePath);
            foreach (var lvl in assets)
            {
                if (lvl == null) continue;
                if (!lvl.IsValid())
                {
                    Debug.LogError($"[LevelDatabase] Invalid level asset: {lvl.name} (#{lvl.LevelNumber})");
                    continue;
                }
                if (_cache.ContainsKey(lvl.LevelNumber))
                    Debug.LogWarning($"[LevelDatabase] Duplicate level number {lvl.LevelNumber}: keeping first.");
                else
                    _cache.Add(lvl.LevelNumber, lvl);
            }
        }

        public static bool TryGet(int levelNumber, out LevelData data)
            => All.TryGetValue(levelNumber, out data);

        public static LevelData Get(int levelNumber)
            => All.TryGetValue(levelNumber, out var d) ? d : null;

        public static int Count => All.Count;

        /// <summary>Level numbers sorted ascending.</summary>
        public static List<int> SortedNumbers()
        {
            var list = new List<int>(All.Keys);
            list.Sort();
            return list;
        }

        /// <summary>Clear cache — only for tests.</summary>
        public static void ResetForTests() => _cache = null;
    }
}
