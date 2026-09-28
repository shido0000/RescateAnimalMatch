using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Orchestrates a single level run: loads LevelData, tracks moves/score/objectives,
    /// and on completion awards Huellas + unlocks the next level (persisted locally and
    /// mirrored to Firestore through ProgressStorage.OnLocalSaved).
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CurrencyWallet wallet; // optional; see Donations task

        // ---- Runtime state for the active level ----
        public LevelData CurrentLevel { get; private set; }
        public int MovesRemaining { get; private set; }
        public int Score { get; private set; }
        public bool LevelActive { get; private set; }

        private readonly Dictionary<ObjectiveType, int> _objectiveProgress =
            new Dictionary<ObjectiveType, int>();

        // ---- Persisted progression ----
        public PlayerProgress Progress { get; private set; }

        // ---- Events (UI / analytics hooks) ----
        public event Action<LevelData> OnLevelStarted;
        public event Action<int /*stars*/> OnLevelCompleted;
        public event Action OnLevelFailed;
        public event Action<int /*movesRemaining*/> OnMovesChanged;
        public event Action<ObjectiveType, int /*current*/, int /*target*/> OnObjectiveProgress;

        private void Awake()
        {
            if (Progress == null) Progress = ProgressStorage.Load();
        }

        /// <summary>Explicit load used by tests (no scene dependency).</summary>
        public void Init(PlayerProgress progress = null)
        {
            Progress = progress ?? ProgressStorage.Load();
        }

        public bool IsLevelUnlocked(int levelNumber)
            => levelNumber >= 1 && levelNumber <= Progress.highestLevelUnlocked;

        /// <summary>Loads a level from Resources/Levels and starts a run.</summary>
        public bool StartLevel(int levelNumber)
        {
            if (!LevelDatabase.TryGet(levelNumber, out var data))
            {
                Debug.LogError($"[LevelManager] Level {levelNumber} not found in Resources/{LevelDatabase.LevelsResourcePath}.");
                return false;
            }
            if (!IsLevelUnlocked(levelNumber))
            {
                Debug.LogWarning($"[LevelManager] Level {levelNumber} is locked.");
                return false;
            }

            CurrentLevel = data;
            MovesRemaining = data.MovesLimit;
            Score = 0;
            LevelActive = true;
            _objectiveProgress.Clear();
            foreach (var o in data.Objectives)
                _objectiveProgress[o.Type] = 0;

            OnMovesChanged?.Invoke(MovesRemaining);
            OnLevelStarted?.Invoke(data);
            return true;
        }

        /// <summary>Consume one player move and optionally add score/objective progress.</summary>
        public void RegisterMove(int scoreGained = 0, ObjectiveType? objective = null, int objectiveDelta = 0)
        {
            if (!LevelActive || CurrentLevel == null) return;

            MovesRemaining = Mathf.Max(0, MovesRemaining - 1);
            Score += Mathf.Max(0, scoreGained);
            OnMovesChanged?.Invoke(MovesRemaining);

            if (objective.HasValue && objectiveDelta > 0 && _objectiveProgress.ContainsKey(objective.Value))
            {
                _objectiveProgress[objective.Value] += objectiveDelta;
                var target = TargetFor(objective.Value);
                OnObjectiveProgress?.Invoke(objective.Value, _objectiveProgress[objective.Value], target);
            }

            if (AllObjectivesMet())
            {
                CompleteLevel(StarsForCurrentScore());
                return;
            }

            if (MovesRemaining == 0)
                FailLevel();
        }

        public int ProgressFor(ObjectiveType type)
            => _objectiveProgress.TryGetValue(type, out var v) ? v : 0;

        public int TargetFor(ObjectiveType type)
        {
            if (CurrentLevel == null) return 0;
            foreach (var o in CurrentLevel.Objectives)
                if (o.Type == type) return o.TargetAmount;
            return 0;
        }

        public bool AllObjectivesMet()
        {
            if (CurrentLevel == null) return false;
            foreach (var o in CurrentLevel.Objectives)
                if (ProgressFor(o.Type) < o.TargetAmount) return false;
            return true;
        }

        public int StarsForCurrentScore()
            => CurrentLevel != null ? CurrentLevel.StarsForScore(Score) : 0;

        /// <summary>
        /// Win path: records stars, unlocks next level, awards Huellas, persists.
        /// stars is clamped to [1..3]; 0 stars is treated as 1 (objectives were met).
        /// </summary>
        public void CompleteLevel(int stars)
        {
            if (!LevelActive || CurrentLevel == null) return;
            LevelActive = false;

            stars = Mathf.Clamp(stars, 1, 3);
            var level = CurrentLevel;

            Progress.RecordCompletion(level.LevelNumber, stars, level.AnimalRewards);

            var huellas = level.HuellasReward * stars; // bonus scales with stars
            if (wallet != null) wallet.AddHuellas(huellas);
            else CurrencyWallet.AddToPersistentWallet(huellas);

            ProgressStorage.Save(Progress);

            Debug.Log($"[LevelManager] Level {level.LevelNumber} complete: {stars}★, +{huellas} Huellas.");
            OnLevelCompleted?.Invoke(stars);
        }

        /// <summary>Lose path: keeps progress untouched, notifies UI to show retry.</summary>
        public void FailLevel()
        {
            if (!LevelActive) return;
            LevelActive = false;
            Debug.Log("[LevelManager] Level failed — show retry UI.");
            OnLevelFailed?.Invoke();
        }
    }
}
