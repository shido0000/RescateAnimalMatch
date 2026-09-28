using System;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Thin persistence abstraction over PlayerPrefs so LevelManager stays testable.
    /// The Firestore mirror lives in Backend/FirestoreProgressSync (Task 4/6); this class
    /// only raises an event after a local save so the sync layer can pick it up.
    /// </summary>
    public static class ProgressStorage
    {
        public const string DefaultKey = "RAM_PlayerProgress";

        /// <summary>Raised after Save() — Firestore sync subscribes to this.</summary>
        public static event Action<PlayerProgress> OnLocalSaved;

        public static PlayerProgress Load(string key = DefaultKey)
        {
            var json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new PlayerProgress();
            try
            {
                var p = JsonUtility.FromJson<ProgressDto>(json);
                return p == null ? new PlayerProgress() : p.ToDomain();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProgressStorage] Corrupt save, starting fresh: {e.Message}");
                return new PlayerProgress();
            }
        }

        public static void Save(PlayerProgress progress, string key = DefaultKey)
        {
            var json = JsonUtility.ToJson(ProgressDto.FromDomain(progress));
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            OnLocalSaved?.Invoke(progress);
        }

        public static void Clear(string key = DefaultKey)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
