using System;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Result wrapper for all backend operations. Success carries Data; failure
    /// carries a human-readable Error message (never exposes stack traces to UI).
    /// </summary>
    public class BackendResult<T>
    {
        public bool Success { get; private set; }
        public T Data { get; private set; }
        public string Error { get; private set; }

        public static BackendResult<T> Ok(T data)
        {
            return new BackendResult<T> { Success = true, Data = data };
        }

        public static BackendResult<T> Fail(string error)
        {
            return new BackendResult<T> { Success = false, Error = error };
        }
    }

    /// <summary>
    /// Abstraction over the Firestore backend so gameplay code never depends on
    /// the Firebase SDK directly. The production implementation wraps
    /// FirebaseFirestore + Cloud Functions callables (Task 6); tests and offline
    /// mode use MockFirestoreBackend backed by local JSON.
    /// </summary>
    public interface IFirestoreBackend
    {
        /// <summary>Local user id (anonymous auth in production).</summary>
        string UserId { get; }

        /// <summary>Fetches config/weekly_goal.</summary>
        void GetWeeklyGoal(Action<BackendResult<WeeklyGoalSnapshot>> onResult);

        /// <summary>
        /// Calls the contributeHuellas Cloud Function, which atomically increments
        /// config/weekly_goal.currentHuellas. Never mutates the goal client-side.
        /// </summary>
        void IncrementWeeklyGoal(int amount, Action<BackendResult<WeeklyGoalSnapshot>> onResult);

        /// <summary>Creates a pending donation record via the onDonationReached path.</summary>
        void RecordDonation(DonationRecord donation, Action<BackendResult<DonationRecord>> onResult);

        /// <summary>Queries shelters where verified == true.</summary>
        void GetVerifiedShelters(Action<BackendResult<ShelterData[]>> onResult);

        /// <summary>Persists the player's Huellas balance snapshot to users/{uid}.</summary>
        void SaveUserCurrency(int huellas, Action<BackendResult<bool>> onResult);

        /// <summary>Loads the player's Huellas balance from users/{uid}.</summary>
        void LoadUserCurrency(Action<BackendResult<int>> onResult);

        /// <summary>
        /// Generic field write into users/{uid} (own progress, purchases,
        /// cosmetics). Only the caller's own document is writable per rules.
        /// </summary>
        void SaveUserData(string field, string jsonValue, Action<BackendResult<bool>> onResult);

        /// <summary>Generic field read from users/{uid}; null if absent.</summary>
        void LoadUserData(string field, Action<BackendResult<string>> onResult);

        /// <summary>Real-time subscription to config/weekly_goal changes.</summary>
        IDisposable SubscribeWeeklyGoal(Action<WeeklyGoalSnapshot> onUpdate);
    }
}
