using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>Visual state of a node in the world map.</summary>
    public enum NodeState { Locked, Unlocked, Completed }

    /// <summary>
    /// World map with one node per level. Unlock rule: level N is unlocked only if
    /// level N-1 has at least 1 star (level 1 always unlocked). Pure logic lives in
    /// <see cref="ComputeStates"/> so it is unit-testable without a scene.
    /// </summary>
    public class ProgressionMap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int columns = 5;

        /// <summary>Computed states keyed by level number.</summary>
        public Dictionary<int, NodeState> States { get; private set; } = new Dictionary<int, NodeState>();

        /// <summary>Raised after Refresh() — the map UI rebinds to this.</summary>
        public event Action<IReadOnlyDictionary<int, NodeState>> OnMapRefreshed;

        /// <summary>
        /// Pure unlock/state computation from a progress snapshot + total level count.
        /// Rule: level 1 → Unlocked/Completed per stars; level N (N&gt;1) unlocked iff
        /// stars(N-1) ≥ 1.
        /// </summary>
        public static Dictionary<int, NodeState> ComputeStates(PlayerProgress progress, int totalLevels)
        {
            var states = new Dictionary<int, NodeState>();
            for (int n = 1; n <= totalLevels; n++)
            {
                if (progress != null && progress.StarsFor(n) > 0)
                {
                    states[n] = NodeState.Completed;
                    continue;
                }

                bool unlocked = n == 1 || (progress != null && progress.StarsFor(n - 1) >= 1);
                states[n] = unlocked ? NodeState.Unlocked : NodeState.Locked;
            }
            return states;
        }

        /// <summary>Rebuilds States from LevelDatabase + given progress.</summary>
        public void Refresh(PlayerProgress progress)
        {
            States = ComputeStates(progress, LevelDatabase.Count);
            OnMapRefreshed?.Invoke(States);
        }

        /// <summary>Local anchor position for node n (serpentine path across columns).</summary>
        public Vector3 NodePosition(int levelNumber)
        {
            int idx = Mathf.Max(0, levelNumber - 1);
            int row = idx / Mathf.Max(1, columns);
            int colInRow = idx % Mathf.Max(1, columns);
            // Serpentine: even rows left→right, odd rows right→left.
            int col = (row % 2 == 0) ? colInRow : (columns - 1 - colInRow);
            return new Vector3(col * 2f, -row * 1.6f, 0f);
        }

        public NodeState StateFor(int levelNumber)
            => States.TryGetValue(levelNumber, out var s) ? s : NodeState.Locked;
    }
}
