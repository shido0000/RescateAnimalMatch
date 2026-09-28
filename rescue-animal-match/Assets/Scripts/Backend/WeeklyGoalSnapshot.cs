using System.Globalization;
using UnityEngine;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Snapshot of the Firestore document config/weekly_goal:
    /// { targetHuellas, currentHuellas, shelterId, shelterName, rewardDescription }.
    /// Field names match the backend schema (Task 6) exactly.
    /// </summary>
    [Serializable]
    public class WeeklyGoalSnapshot
    {
        public int targetHuellas;
        public int currentHuellas;
        public string shelterId;
        public string shelterName;
        public string rewardDescription;

        /// <summary>Clamped 0..1 progress ratio for UI bars.</summary>
        public float Progress
        {
            get
            {
                if (targetHuellas <= 0) return 0f;
                return Mathf.Clamp01((float)currentHuellas / targetHuellas);
            }
        }

        public bool IsReached => targetHuellas > 0 && currentHuellas >= targetHuellas;

        public int Remaining => Mathf.Max(0, targetHuellas - currentHuellas);

        public WeeklyGoalSnapshot Clone()
        {
            return new WeeklyGoalSnapshot
            {
                targetHuellas = targetHuellas,
                currentHuellas = currentHuellas,
                shelterId = shelterId,
                shelterName = shelterName,
                rewardDescription = rewardDescription
            };
        }

        /// <summary>Formats the currency amount with es-ES thousand separators.</summary>
        public string FormatCurrent()
        {
            return currentHuellas.ToString("N0", new CultureInfo("es-ES"));
        }
    }
}
