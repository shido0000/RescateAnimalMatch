using System;
using UnityEngine;
using RescueAnimalMatch.Backend;

namespace RescueAnimalMatch.Donations
{
    /// <summary>
    /// Client view of the community weekly goal (Firestore config/weekly_goal).
    /// Fetches the goal, keeps a real-time subscription, and routes player
    /// contributions through the contributeHuellas Cloud Function.
    /// </summary>
    [DisallowMultipleComponent]
    public class CommunityProgress : MonoBehaviour
    {
        public static CommunityProgress Instance { get; private set; }

        [SerializeField] private IFirestoreBackend _injectedBackend;

        private IFirestoreBackend _backend;
        private WeeklyGoalSnapshot _snapshot = new WeeklyGoalSnapshot();
        private IDisposable _subscription;
        private bool _subscribed;

        // True once the current donation cycle has been announced; reset
        // when a new goal (different target/shelter) arrives from the backend.
        private bool _donationCycleFired;

        /// <summary>Raised on every goal snapshot change (fetch, contribution, push).</summary>
        public event Action<WeeklyGoalSnapshot> GoalUpdated;

        /// <summary>Raised once per donation cycle when the goal is reached.</summary>
        public event Action<WeeklyGoalSnapshot> GoalReached;

        public WeeklyGoalSnapshot Current => _snapshot;
        public float Progress => _snapshot.Progress;
        public bool IsGoalReached => _snapshot.IsReached;

        public void AttachBackend(IFirestoreBackend backend)
        {
            _donationCycleFired = false;
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
                _subscribed = false;
            }
            _backend = backend;
        }

        private void OnEnable()
        {
            Instance = this;
            if (_backend == null) _backend = _injectedBackend;
            Refresh();
            Subscribe();
        }

        private void OnDisable()
        {
            if (_subscription != null) _subscription.Dispose();
            _subscription = null;
            _subscribed = false;
            if (Instance == this) Instance = null;
        }

        /// <summary>One-shot fetch of config/weekly_goal.</summary>
        public void Refresh(Action<bool> onDone = null)
        {
            if (_backend == null)
            {
                onDone?.Invoke(false);
                return;
            }
            _backend.GetWeeklyGoal(result =>
            {
                if (result.Success) ApplySnapshot(result.Data);
                else Debug.LogWarning($"[CommunityProgress] Fetch failed: {result.Error}");
                onDone?.Invoke(result.Success);
            });
        }

        /// <summary>
        /// Sends an amount of Huellas to the shared weekly goal via the Cloud
        /// Function. onResult receives the backend result with the updated goal.
        /// </summary>
        public void ContributeHuellas(int amount,
            Action<BackendResult<WeeklyGoalSnapshot>> onResult = null)
        {
            if (_backend == null)
            {
                onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Fail("no backend attached"));
                return;
            }

            _backend.IncrementWeeklyGoal(amount, result =>
            {
                if (result.Success)
                {
                    // La fuente de verdad tras la escritura atómica del Cloud
                    // Function es el snapshot devuelto por el backend; si el
                    // tiempo real ya lo aplicó, no se duplica la notificación.
                    if (!SameState(result.Data)) ApplySnapshot(result.Data);
                }
                else
                {
                    Debug.LogWarning($"[CommunityProgress] Contribution failed: {result.Error}");
                }
                onResult?.Invoke(result);
            });
        }

        private void Subscribe()
        {
            if (_backend == null || _subscribed) return;
            _subscribed = true;
            _subscription = _backend.SubscribeWeeklyGoal(ApplySnapshot);
        }

        /// <summary>True when the incoming snapshot equals the currently applied one.</summary>
        private bool SameState(WeeklyGoalSnapshot other)
        {
            if (other == null) return false;
            return other.currentHuellas == _snapshot.currentHuellas &&
                   other.targetHuellas == _snapshot.targetHuellas &&
                   other.shelterId == _snapshot.shelterId;
        }

        private void ApplySnapshot(WeeklyGoalSnapshot incoming)
        {
            if (incoming == null) return;
            // La marca de ciclo donado se actualiza ANTES de notificar para que
            // los suscriptores no vuelvan a disparar GoalReached en transiciones
            // posteriores (sobre-contribuciones, pushes de otros jugadores).
            bool cycleJustStarted = incoming.IsReached && !_donationCycleFired;
            if (cycleJustStarted) _donationCycleFired = true;
            _snapshot = incoming;
            GoalUpdated?.Invoke(_snapshot);
            DonationTracker.NotifyGoalState(_snapshot);
            if (cycleJustStarted)
            {
                GoalReached?.Invoke(_snapshot);
                DonationTracker.OnWeeklyGoalReached(_snapshot);
            }
        }
    }
}
