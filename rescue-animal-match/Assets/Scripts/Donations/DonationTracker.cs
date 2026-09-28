using System;
using System.Collections.Generic;
using UnityEngine;
using RescueAnimalMatch.Backend;

namespace RescueAnimalMatch.Donations
{
    /// <summary>
    /// Tracks donation lifecycle on the client. The authoritative donations
    /// document is created by the onDonationReached Cloud Function (Task 6);
    /// this class observes goal snapshots, mirrors the confirmation state for
    /// the UI, and keeps a local history of donations seen this session.
    /// </summary>
    public static class DonationTracker
    {
        private static readonly List<DonationRecord> _history = new List<DonationRecord>();
        private static readonly HashSet<string> _announced = new HashSet<string>();
        private static IFirestoreBackend _backend;

        /// <summary>Raised when a weekly-goal donation cycle completes (confirmation UI).</summary>
        public static event Action<DonationRecord> OnDonationConfirmed;

        /// <summary>Raised whenever the tracked history changes.</summary>
        public static event Action<IReadOnlyList<DonationRecord>> OnHistoryUpdated;

        public static IReadOnlyList<DonationRecord> History => _history;

        public static DonationRecord LastConfirmed { get; private set; }

        public static void AttachBackend(IFirestoreBackend backend)
        {
            _backend = backend;
        }

        /// <summary>Called by CommunityProgress on every snapshot transition.</summary>
        public static void NotifyGoalState(WeeklyGoalSnapshot snapshot)
        {
            // Hook for future analytics (e.g. near-completion nudges). Kept
            // explicit so CommunityProgress has a single integration point.
        }

        /// <summary>
        /// Entry point invoked when currentHuellas >= targetHuellas. Creates the
        /// donation record through the backend (mirroring what the Cloud Function
        /// does server-side) and fires the confirmation event exactly once per id.
        /// </summary>
        public static void OnWeeklyGoalReached(WeeklyGoalSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsReached) return;

            var record = DonationRecord.Create(
                snapshot.shelterId, snapshot.shelterName,
                snapshot.targetHuellas, "weekly_goal");

            Announce(record);
        }

        /// <summary>Folds donation docs fetched from the backend into the history.</summary>
        public static void IngestServerDonations(IEnumerable<DonationRecord> records)
        {
            if (records == null) return;
            foreach (var record in records)
            {
                if (record != null) Announce(record);
            }
        }

        /// <summary>Resets session state (used by tests and account switch).</summary>
        public static void ResetForTests()
        {
            _history.Clear();
            _announced.Clear();
            LastConfirmed = null;
            _backend = null;
            OnDonationConfirmed = null;
            OnHistoryUpdated = null;
        }

        private static void Announce(DonationRecord record)
        {
            string key = record.id ?? $"{record.shelterId}:{record.amount}:{record.date}";
            bool isNew = false;
            lock (_announced)
            {
                if (!_announced.Contains(key))
                {
                    _announced.Add(key);
                    _history.Add(record);
                    LastConfirmed = record;
                    isNew = true;
                }
            }

            if (!isNew) return;
            OnDonationConfirmed?.Invoke(record);
            OnHistoryUpdated?.Invoke(_history);
        }
    }
}
