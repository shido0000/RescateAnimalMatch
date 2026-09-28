using System;
using UnityEngine;
using RescueAnimalMatch.Backend;
using RescueAnimalMatch.Progression;

namespace RescueAnimalMatch.Donations
{
    /// <summary>
    /// Single source of truth for the "Huellas" currency. Persists locally via
    /// PlayerPrefs (same key used by CurrencyWallet/LevelManager) and syncs the
    /// balance to Firestore users/{uid} when a backend is attached.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurrencyManager : MonoBehaviour
    {
        private const int MaxHuellas = 10_000_000;
        private const string TotalContributedKey = "RAM_TotalContributed";

        [Tooltip("Optional backend for cloud sync; offline mode keeps local-only.")]
        [SerializeField] private IFirestoreBackend _injectedBackend;

        public int Huellas { get; private set; }

        /// <summary>Huella total ever contributed to community goals.</summary>
        public int TotalContributed { get; private set; }

        /// <summary>Raised whenever the balance changes (Task 4 requirement).</summary>
        public event Action<int> OnHuellasChanged;

        private IFirestoreBackend Backend { get; set; }

        public void AttachBackend(IFirestoreBackend backend)
        {
            Backend = backend;
        }

        private void Awake()
        {
            Backend = _injectedBackend;
            Huellas = PlayerPrefs.GetInt(CurrencyWallet.HuellasKey, 0);
            TotalContributed = PlayerPrefs.GetInt(TotalContributedKey, 0);
        }

        public void AddHuellas(int amount)
        {
            if (amount <= 0) return;
            Huellas = Mathf.Min(MaxHuellas, Huellas + amount);
            PersistLocal();
        }

        /// <summary>Returns false (without changing state) if funds are insufficient.</summary>
        public bool SpendHuellas(int amount)
        {
            if (amount <= 0 || Huellas < amount) return false;
            Huellas -= amount;
            PersistLocal();
            return true;
        }

        /// <summary>
        /// Moves Huellas from the player wallet into the community goal. The
        /// spend happens locally first; if the Cloud Function rejects the
        /// increment the amount is refunded.
        /// </summary>
        public void ContributeToCommunity(int amount, Action<bool> onResult = null)
        {
            if (!SpendHuellas(amount))
            {
                onResult?.Invoke(false);
                return;
            }

            var progress = CommunityProgress.Instance;
            if (progress == null)
            {
                Refund(amount);
                onResult?.Invoke(false);
                return;
            }

            progress.ContributeHuellas(amount, result =>
            {
                if (result != null && result.Success)
                {
                    TotalContributed += amount;
                    PlayerPrefs.SetInt(TotalContributedKey, TotalContributed);
                    PlayerPrefs.Save();
                    onResult?.Invoke(true);
                }
                else
                {
                    Refund(amount);
                    onResult?.Invoke(false);
                }
            });
        }

        /// <summary>Pushes the current balance to users/{uid}.</summary>
        public void SyncToCloud(Action<bool> onDone = null)
        {
            if (Backend == null)
            {
                onDone?.Invoke(false);
                return;
            }
            Backend.SaveUserCurrency(Huellas, _ => onDone?.Invoke(true));
        }

        /// <summary>Pulls the cloud balance and adopts it if greater than local.</summary>
        public void PullFromCloud(Action<bool> onDone = null)
        {
            if (Backend == null)
            {
                onDone?.Invoke(false);
                return;
            }
            Backend.LoadUserCurrency(result =>
            {
                if (result.Success && result.Data > Huellas)
                {
                    Huellas = result.Data;
                    PersistLocal();
                }
                onDone?.Invoke(result.Success);
            });
        }

        private void Refund(int amount)
        {
            Huellas = Mathf.Min(MaxHuellas, Huellas + amount);
            PersistLocal();
        }

        private void PersistLocal()
        {
            PlayerPrefs.SetInt(CurrencyWallet.HuellasKey, Huellas);
            PlayerPrefs.Save();
            OnHuellasChanged?.Invoke(Huellas);
            if (Backend != null) Backend.SaveUserCurrency(Huellas, null);
        }
    }
}
