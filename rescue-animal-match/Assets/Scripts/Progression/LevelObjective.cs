using System;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Kind of thing the player must do to pass a level.
    /// </summary>
    public enum ObjectiveType
    {
        RescueAnimals = 0,
        CollectResources = 1,
        ClearDebris = 2,
        FeedAnimals = 3
    }

    /// <summary>
    /// One objective entry inside a <see cref="LevelData"/> asset.
    /// Immutable value type so level assets cannot be mutated at runtime.
    /// </summary>
    [Serializable]
    public struct LevelObjective
    {
        public ObjectiveType Type;
        public int TargetAmount;

        /// <summary>Optional piece filter (e.g. collect 20 Hearts). 0 = any.</summary>
        public int PieceFilter;

        public LevelObjective(ObjectiveType type, int targetAmount, int pieceFilter = 0)
        {
            Type = type;
            TargetAmount = targetAmount;
            PieceFilter = pieceFilter;
        }

        public bool IsValid => TargetAmount > 0;
    }
}
