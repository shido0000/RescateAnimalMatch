using System;
using UnityEngine;

namespace RescueAnimalMatch.Progression
{
    /// <summary>
    /// Minimal persistent wallet for the "Huellas" currency, living in the Progression
    /// layer so LevelManager can award rewards without a hard dependency on Task 4's
    /// Donations/CurrencyManager (which will reuse this same PlayerPrefs key).
    /// </summary>
    public class CurrencyWallet : MonoBehaviour
    {
        public const string HuellasKey = "RAM_Huellas";
        private const int MaxHuellas = 10_000_000;

        [SerializeField] private int startingBalance = 0;

        /// <summary>Raised whenever the balance changes.</summary>
        public event Action<int> OnHuellasChanged;

        public int Huellas { get; private set; }

        private void Awake()
        {
            Huellas = PlayerPrefs.GetInt(HuellasKey, startingBalance);
        }

        public void AddHuellas(int amount)
        {
            if (amount <= 0) return;
            Huellas = Mathf.Min(MaxHuellas, Huellas + amount);
            Persist();
        }

        public bool SpendHuellas(int amount)
        {
            if (amount <= 0 || Huellas < amount) return false;
            Huellas -= amount;
            Persist();
            return true;
        }

        private void Persist()
        {
            PlayerPrefs.SetInt(HuellasKey, Huellas);
            PlayerPrefs.Save();
            OnHuellasChanged?.Invoke(Huellas);
        }

        /// <summary>Static helper for non-MonoBehaviour callers (e.g. LevelManager fallback).</summary>
        public static void AddToPersistentWallet(int amount)
        {
            if (amount <= 0) return;
            var current = PlayerPrefs.GetInt(HuellasKey, 0);
            PlayerPrefs.SetInt(HuellasKey, Mathf.Min(MaxHuellas, current + amount));
            PlayerPrefs.Save();
        }

        public static int PeekBalance() => PlayerPrefs.GetInt(HuellasKey, 0);
    }
}
