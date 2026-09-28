using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Backend.Mocks
{
    /// <summary>
    /// In-memory IFirestoreBackend seeded from Resources/Mocks/mock_firestore.json.
    /// Used by EditMode tests and offline/dev mode. It emulates the Cloud Function
    /// semantics of Task 6: IncrementWeeklyGoal atomically raises currentHuellas
    /// and, when the target is reached, creates a donations document exactly once
    /// (mirroring the onDonationReached Firestore trigger).
    /// </summary>
    public class MockFirestoreBackend : IFirestoreBackend
    {
        private readonly WeeklyGoalSnapshot _goal;
        private readonly List<ShelterData> _shelters = new List<ShelterData>();
        private readonly List<DonationRecord> _donations = new List<DonationRecord>();
        private readonly List<Action<WeeklyGoalSnapshot>> _subscribers =
            new List<Action<WeeklyGoalSnapshot>>();

        private int _userHuellas;
        private bool _donationOpen; // true while goal not yet reached this cycle

        /// <summary>Number of completed donation cycles (for tests).</summary>
        public int DonationCyclesCompleted { get; private set; }

        public string UserId { get; private set; }

        public IReadOnlyList<DonationRecord> Donations => _donations;

        public MockFirestoreBackend(string json = null, string userId = "mock-user-01")
        {
            UserId = userId;
            if (string.IsNullOrEmpty(json))
            {
                var ta = Resources.Load<TextAsset>("Mocks/mock_firestore");
                json = ta != null ? ta.text : "{}";
            }

            MockFirestoreJson parsed;
            try
            {
                parsed = JsonUtility.FromJson<MockFirestoreJson>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MockFirestore] Invalid seed JSON: {e.Message}");
                parsed = null;
            }

            _goal = (parsed != null && parsed.weekly_goal != null)
                ? parsed.weekly_goal
                : new WeeklyGoalSnapshot { targetHuellas = 10000 };
            _donationOpen = !_goal.IsReached;

            if (parsed != null)
            {
                if (parsed.shelters != null) _shelters.AddRange(parsed.shelters);
                if (parsed.donations != null) _donations.AddRange(parsed.donations);
            }
        }

        public void GetWeeklyGoal(Action<BackendResult<WeeklyGoalSnapshot>> onResult)
        {
            onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Ok(_goal.Clone()));
        }

        public void IncrementWeeklyGoal(int amount,
            Action<BackendResult<WeeklyGoalSnapshot>> onResult)
        {
            if (amount <= 0)
            {
                onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Fail(
                    "amount must be positive"));
                return;
            }

            _goal.currentHuellas += amount;
            MaybeTriggerDonation();
            NotifySubscribers();
            onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Ok(_goal.Clone()));
        }

        public void RecordDonation(DonationRecord donation,
            Action<BackendResult<DonationRecord>> onResult)
        {
            if (donation == null)
            {
                onResult?.Invoke(BackendResult<DonationRecord>.Fail("null donation"));
                return;
            }
            _donations.Add(donation);
            onResult?.Invoke(BackendResult<DonationRecord>.Ok(donation));
        }

        public void GetVerifiedShelters(Action<BackendResult<ShelterData[]>> onResult)
        {
            var verified = new List<ShelterData>();
            foreach (var s in _shelters)
            {
                if (s != null && s.verified) verified.Add(s);
            }
            onResult?.Invoke(BackendResult<ShelterData[]>.Ok(verified.ToArray()));
        }

        public void SaveUserCurrency(int huellas, Action<BackendResult<bool>> onResult)
        {
            _userHuellas = Mathf.Max(0, huellas);
            onResult?.Invoke(BackendResult<bool>.Ok(true));
        }

        public void LoadUserCurrency(Action<BackendResult<int>> onResult)
        {
            onResult?.Invoke(BackendResult<int>.Ok(_userHuellas));
        }

        public IDisposable SubscribeWeeklyGoal(Action<WeeklyGoalSnapshot> onUpdate)
        {
            _subscribers.Add(onUpdate);
            onUpdate?.Invoke(_goal.Clone());
            return new Subscription(this, onUpdate);
        }

        /// <summary>Test helper: simulates the admin starting a new weekly event.</summary>
        public void ResetWeeklyGoal(int targetHuellas)
        {
            _goal.targetHuellas = targetHuellas;
            _goal.currentHuellas = 0;
            _donationOpen = true;
            NotifySubscribers();
        }

        private void MaybeTriggerDonation()
        {
            if (!_donationOpen || !_goal.IsReached) return;

            _donationOpen = false;
            DonationCyclesCompleted++;
            var record = DonationRecord.Create(
                _goal.shelterId, _goal.shelterName, _goal.targetHuellas, "weekly_goal");
            record.id = $"don_mock_{_donations.Count + 1}";
            _donations.Add(record);
            Debug.Log($"[MockFirestore] onDonationReached -> donations/{record.id}");
        }

        private void NotifySubscribers()
        {
            foreach (var sub in _subscribers)
            {
                sub?.Invoke(_goal.Clone());
            }
        }

        private void Unsubscribe(Action<WeeklyGoalSnapshot> handler)
        {
            _subscribers.Remove(handler);
        }

        private class Subscription : IDisposable
        {
            private MockFirestoreBackend _owner;
            private readonly Action<WeeklyGoalSnapshot> _handler;

            public Subscription(MockFirestoreBackend owner, Action<WeeklyGoalSnapshot> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_owner == null) return;
                _owner.Unsubscribe(_handler);
                _owner = null;
            }
        }
    }
}
