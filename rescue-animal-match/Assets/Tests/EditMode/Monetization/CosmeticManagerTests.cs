using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Backend;
using RescueAnimalMatch.Core;
using RescueAnimalMatch.Donations;
using RescueAnimalMatch.Monetization;

namespace RescueAnimalMatch.Tests.EditMode.Monetization
{
    /// <summary>
    /// Backend falso en memoria que captura las escrituras de users/{uid}
    /// (campo "cosmetics") para verificar la sincronización del guardarropa.
    /// </summary>
    internal sealed class FakeCosmeticBackend : IFirestoreBackend
    {
        public int SaveUserDataCalls;
        public string LastCosmeticsJson;

        public string UserId => "test-user";

        public void GetWeeklyGoal(Action<BackendResult<WeeklyGoalSnapshot>> onResult)
            => onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Ok(null));

        public void IncrementWeeklyGoal(int amount, Action<BackendResult<WeeklyGoalSnapshot>> onResult)
            => onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Ok(null));

        public void RecordDonation(DonationRecord donation, Action<BackendResult<DonationRecord>> onResult)
            => onResult?.Invoke(BackendResult<DonationRecord>.Ok(donation));

        public void GetVerifiedShelters(Action<BackendResult<ShelterData[]>> onResult)
            => onResult?.Invoke(BackendResult<ShelterData[]>.Ok(Array.Empty<ShelterData>()));

        public void SaveUserCurrency(int huellas, Action<BackendResult<bool>> onResult)
            => onResult?.Invoke(BackendResult<bool>.Ok(true));

        public void LoadUserCurrency(Action<BackendResult<int>> onResult)
            => onResult?.Invoke(BackendResult<int>.Ok(0));

        public void SaveUserData(string field, string jsonValue, Action<BackendResult<bool>> onResult)
        {
            if (field == "cosmetics")
            {
                SaveUserDataCalls++;
                LastCosmeticsJson = jsonValue;
            }
            onResult?.Invoke(BackendResult<bool>.Ok(true));
        }

        public void LoadUserData(string field, Action<BackendResult<string>> onResult)
            => onResult?.Invoke(BackendResult<string>.Ok(null));

        public IDisposable SubscribeWeeklyGoal(Action<WeeklyGoalSnapshot> onUpdate)
            => new NoopSubscription();

        private sealed class NoopSubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Task 5 verification: CosmeticManager — compra con Huellas vía el hook
    /// de CurrencyManager, equip/unequip por categoría y persistencia
    /// local (PlayerPrefs) + sync a Firestore (users/{uid}.cosmetics).
    /// </summary>
    [TestFixture]
    public class CosmeticManagerTests
    {
        private GameObject _go;
        private CosmeticManager _mgr;
        private FakeCosmeticBackend _backend;
        private CurrencyManager _currency;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            CosmeticCatalog.ResetCacheForTests(); // fuerza catálogo por defecto

            _backend = new FakeCosmeticBackend();

            var currencyGo = new GameObject("Currency");
            _currency = currencyGo.AddComponent<CurrencyManager>();
            _currency.AttachBackend(_backend);

            _go = new GameObject("Cosmetics");
            _mgr = _go.AddComponent<CosmeticManager>();
            _mgr.AttachBackend(_backend);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_currency != null) UnityEngine.Object.DestroyImmediate(_currency.gameObject);
            PlayerPrefs.DeleteAll();
            CosmeticCatalog.ResetCacheForTests();
        }

        private Func<int, bool> SpendHook => amount => _currency.SpendHuellas(amount);

        [Test]
        public void Catalog_HasAllFourCategories_AndFreeDefaultsOwned()
        {
            var categories = new HashSet<CosmeticType>();
            foreach (var item in CosmeticCatalog.Items)
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.Id));
                Assert.IsFalse(string.IsNullOrEmpty(item.LocalizedKey),
                    "Los textos van por clave de localización, no hardcodeados");
                categories.Add(item.Type);
            }
            CollectionAssert.AreEquivalent(
                new[] { CosmeticType.BoardTheme, CosmeticType.Background,
                        CosmeticType.VirtualPet, CosmeticType.Avatar },
                new List<CosmeticType>(categories));

            // Ítems gratis poseídos automáticamente al Awake.
            Assert.IsTrue(_mgr.Owns(DefaultCosmeticCatalog.PawTheme.Id));
            Assert.IsTrue(_mgr.Owns(DefaultCosmeticCatalog.MeadowBg.Id));
            Assert.IsTrue(_mgr.Owns(DefaultCosmeticCatalog.ExplorerAvatar.Id));
        }

        [Test]
        public void FreeItem_EquippedByDefault_OnAwake()
        {
            var equipped = _mgr.GetEquipped(CosmeticType.BoardTheme);
            Assert.IsNotNull(equipped);
            Assert.AreEqual(DefaultCosmeticCatalog.PawTheme.Id, equipped.Id);
        }

        [Test]
        public void PurchaseWithHuellas_DeductsBalance_OwnsAndEquips_SyncsFirestore()
        {
            var item = DefaultCosmeticCatalog.NightTheme; // 800 Huellas
            _currency.AddHuellas(1000);

            CosmeticItem purchased = null;
            _mgr.OnCosmeticPurchased += i => purchased = i;

            _mgr.PurchaseWithHuellas(item.Id, SpendHook);

            Assert.IsNotNull(purchased, "Evento OnCosmeticPurchased emitido");
            Assert.AreEqual(200, _currency.Huellas, "Se descuentan exactamente 800 Huellas");
            Assert.IsTrue(_mgr.Owns(item.Id));
            Assert.AreEqual(item.Id, _mgr.GetEquipped(CosmeticType.BoardTheme).Id,
                "La compra equipa el ítem automáticamente");
            Assert.GreaterOrEqual(_backend.SaveUserDataCalls, 1,
                "Sync a Firestore tras comprar");
            Assert.IsNotNull(_backend.LastCosmeticsJson);
            StringAssert.Contains(item.Id, _backend.LastCosmeticsJson);
        }

        [Test]
        public void Purchase_InsufficientHuellas_FailsSilently_NoStateChange()
        {
            var item = DefaultCosmeticCatalog.PuppyPet; // 1200 Huellas
            _currency.AddHuellas(100);

            _mgr.PurchaseWithHuellas(item.Id, SpendHook);

            Assert.IsFalse(_mgr.Owns(item.Id));
            Assert.AreEqual(100, _currency.Huellas, "No se cobra una compra fallida");
            Assert.AreEqual(0, _backend.SaveUserDataCalls);
        }

        [Test]
        public void Purchase_AlreadyOwned_DoesNotChargeTwice()
        {
            var item = DefaultCosmeticCatalog.SunsetBg; // 500 Huellas
            _currency.AddHuellas(2000);

            _mgr.PurchaseWithHuellas(item.Id, SpendHook);
            _mgr.PurchaseWithHuellas(item.Id, SpendHook); // segunda vez: no-op

            Assert.AreEqual(1500, _currency.Huellas, "Solo se cobra una vez");
        }

        [Test]
        public void TryEquip_NotOwned_Fails()
        {
            Assert.IsFalse(_mgr.TryEquip(DefaultCosmeticCatalog.HeroAvatar.Id),
                "No se puede equipar algo no poseído");
        }

        [Test]
        public void Unequip_ReturnsEventWithNullItem_AndClearsSlot()
        {
            var item = DefaultCosmeticCatalog.ParrotPet; // 1500 Huellas
            _currency.AddHuellas(2000);
            _mgr.PurchaseWithHuellas(item.Id, SpendHook);
            Assert.IsNotNull(_mgr.GetEquipped(CosmeticType.VirtualPet));

            CosmeticType changedType = (CosmeticType)(-1);
            bool sawUnequip = false;
            _mgr.OnEquippedChanged += (t, i) => { changedType = t; sawUnequip = i == null; };

            _mgr.Unequip(CosmeticType.VirtualPet);

            Assert.IsTrue(sawUnequip, "El evento señala unequip con item null");
            Assert.AreEqual(CosmeticType.VirtualPet, changedType);
            Assert.IsNull(_mgr.GetEquipped(CosmeticType.VirtualPet));
        }

        [Test]
        public void Persistence_LocalSaveRoundTrip_RestoresOwnedAndEquipped()
        {
            var item = DefaultCosmeticCatalog.NightTheme;
            _currency.AddHuellas(1000);
            _mgr.PurchaseWithHuellas(item.Id, SpendHook);

            // Simula reinicio de app: nuevo manager sobre el mismo PlayerPrefs.
            var go2 = new GameObject("Cosmetics2");
            var mgr2 = go2.AddComponent<CosmeticManager>();

            Assert.IsTrue(mgr2.Owns(item.Id), "La propiedad sobrevive al reinicio");
            Assert.AreEqual(item.Id, mgr2.GetEquipped(CosmeticType.BoardTheme).Id,
                "Lo equipado sobrevive al reinicio");

            UnityEngine.Object.DestroyImmediate(go2);
        }

        [Test]
        public void UnknownProductId_IsIgnored()
        {
            _currency.AddHuellas(5000);
            Assert.DoesNotThrow(() => _mgr.PurchaseWithHuellas("no_existe", SpendHook));
            Assert.IsFalse(_mgr.TryEquip("no_existe"));
            Assert.AreEqual(5000, _currency.Huellas);
        }
    }
}
