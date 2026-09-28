using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>Estado serializable del guardarropa de cosméticos.</summary>
    [Serializable]
    public class CosmeticSaveState
    {
        public List<string> OwnedIds = new List<string>();
        public string EquippedTheme;
        public string EquippedBackground;
        public string EquippedPet;
        public string EquippedAvatar;
    }

    /// <summary>
    /// Gestor de cosméticos (Task 5): compra con Huellas, equip/unequip por
    /// categoría y persistencia local (PlayerPrefs) + sync opcional a Firestore.
    /// </summary>
    [DisallowMultipleComponent]
    public class CosmeticManager : MonoBehaviour
    {
        private const string SaveKey = "RAM_Cosmetics";

        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly Dictionary<CosmeticType, string> _equipped =
            new Dictionary<CosmeticType, string>();

        public event Action<CosmeticItem> OnCosmeticPurchased;
        public event Action<CosmeticType, CosmeticItem> OnEquippedChanged; // null item = unequipped

        public IReadOnlyCollection<string> OwnedIds => _owned;

        private void Awake()
        {
            LoadLocal();
            EnsureFreeItemsOwnedAndEquipped();
        }

        /// <summary>Los ítems gratis se poseen automáticamente; defaults equipados.</summary>
        private void EnsureFreeItemsOwnedAndEquipped()
        {
            foreach (var item in CosmeticCatalog.Items)
            {
                if (item.IsFree && !_owned.Contains(item.Id))
                {
                    _owned.Add(item.Id);
                    if (!_equipped.ContainsKey(item.Type)) EquipInternal(item);
                }
            }
            SaveLocal();
        }

        public bool Owns(string id) => _owned.Contains(id);

        public bool TryEquip(string id)
        {
            if (!CosmeticCatalog.TryGet(id, out var item)) return false;
            if (!_owned.Contains(id)) return false;
            EquipInternal(item);
            SaveLocal();
            return true;
        }

        public void Unequip(CosmeticType type)
        {
            if (_equipped.ContainsKey(type))
            {
                _equipped.Remove(type);
                OnEquippedChanged?.Invoke(type, null);
                SaveLocal();
            }
        }

        public CosmeticItem GetEquipped(CosmeticType type) =>
            _equipped.TryGetValue(type, out var id) && CosmeticCatalog.TryGet(id, out var item)
                ? item : null;

        /// <summary>Compra con Huellas vía callback del CurrencyManager (Task 4).</summary>
        public void PurchaseWithHuellas(string id, Func<int, bool> spendHuellas)
        {
            if (!CosmeticCatalog.TryGet(id, out var item)) return;
            if (_owned.Contains(id)) return;                       // ya poseído
            if (item.PriceHuellas > 0 && !Spend(spendHuellas, item)) return;

            _owned.Add(id);
            EquipInternal(item);
            OnCosmeticPurchased?.Invoke(item);
            SaveLocal();
            SyncToBackend();
        }

        private static bool Spend(Func<int, bool> spendHuellas, CosmeticItem item)
        {
            if (spendHuellas == null)
            {
                Debug.LogWarning("[CosmeticManager] No spend hook wired; purchase aborted.");
                return false;
            }
            return spendHuellas(item.PriceHuellas);
        }

        private void EquipInternal(CosmeticItem item)
        {
            _equipped[item.Type] = item.Id;
            OnEquippedChanged?.Invoke(item.Type, item);
        }

        // ---------- Persistencia local ----------
        private void SaveLocal()
        {
            var state = new CosmeticSaveState
            {
                OwnedIds = new List<string>(_owned)
            };
            foreach (var kv in _equipped) state.OwnedIds.Add("eq:" + kv.Key + "=" + kv.Value);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        private void LoadLocal()
        {
            _owned.Clear();
            _equipped.Clear();
            var json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var state = JsonUtility.FromJson<CosmeticSaveState>(json);
                if (state?.OwnedIds == null) return;
                foreach (var entry in state.OwnedIds)
                {
                    if (entry.StartsWith("eq:"))
                    {
                        var body = entry.Substring(3);
                        var eq = body.Split('=');
                        if (eq.Length == 2 && Enum.TryParse(eq[0], out CosmeticType t))
                            _equipped[t] = eq[1];
                    }
                    else _owned.Add(entry);
                }
            }
            catch (Exception e)
            {
                Core.CrashReport.NonFatal(e, "Cosmetic save corrupted");
                PlayerPrefs.DeleteKey(SaveKey);
            }
        }

        // ---------- Sync Firestore (users/{uid}.cosmetics) ----------
        private Backend.IFirestoreBackend _backend;

        public void AttachBackend(Backend.IFirestoreBackend backend)
        {
            _backend = backend;
        }

        private void SyncToBackend()
        {
            if (_backend == null) return; // offline: la carga local es suficiente
            _backend.SaveUserData("cosmetics", JsonUtility.ToJson(BuildState()), _ => { });
        }

        private CosmeticSaveState BuildState()
        {
            var s = new CosmeticSaveState { OwnedIds = new List<string>(_owned) };
            foreach (var kv in _equipped)
            {
                switch (kv.Key)
                {
                    case CosmeticType.BoardTheme: s.EquippedTheme = kv.Value; break;
                    case CosmeticType.Background: s.EquippedBackground = kv.Value; break;
                    case CosmeticType.VirtualPet: s.EquippedPet = kv.Value; break;
                    case CosmeticType.Avatar: s.EquippedAvatar = kv.Value; break;
                }
            }
            return s;
        }

        // --- Visibilidad para tests ---
        internal void ResetForTests()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            _owned.Clear();
            _equipped.Clear();
        }
    }
}
