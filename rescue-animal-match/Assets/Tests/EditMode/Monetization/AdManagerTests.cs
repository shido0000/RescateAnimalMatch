using System;
using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Core;

namespace RescueAnimalMatch.Tests.EditMode.Monetization
{
    /// <summary>
    /// Task 5 verification: AdManager policy — rewarded opcional otorga 50
    /// Huellas, interstitial máximo 1 cada 3 niveles y nunca durante gameplay.
    /// </summary>
    public class AdManagerTests
    {
        private GameObject _go;
        private AdManager _ads;
        private MockAdsProvider _provider;
        private MockAnalyticsLogger _analytics;
        private string _phase = "menu";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            _go = new GameObject("AdManager");
            _ads = _go.AddComponent<AdManager>();
            _provider = new MockAdsProvider();
            _analytics = new MockAnalyticsLogger();
            _ads.Configure(_provider, _analytics);
            _phase = "menu";
            _ads.Initialize(() => _phase);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void Initialize_PreloadsWithGoogleTestUnitIds()
        {
            Assert.AreEqual(AdManager.TestAppId, _provider.AppId);
            Assert.IsTrue(_ads.IsRewardedReady, "rewarded should be preloaded");
        }

        [Test]
        public void ShowRewardedAd_Grants50HuellasToWallet()
        {
            RewardResult received = null;
            _ads.ShowRewardedAd(result => received = result);

            Assert.IsNotNull(received);
            Assert.IsTrue(received.Earned);
            Assert.AreEqual(AdManager.RewardedHuellas, received.HuellasAmount);
            Assert.AreEqual(50, Progression.CurrencyWallet.PeekBalance());
            Assert.AreEqual(1, _provider.RewardedShownCount);
        }

        [Test]
        public void ShowRewardedAd_LogsAdWatchedEvent()
        {
            _ads.ShowRewardedAd(result => { });
            Assert.IsTrue(_analytics.HasEvent(AnalyticsEvents.AdWatched));
        }

        [Test]
        public void ShowRewardedAd_NotReady_ReturnsFalseWithoutCrash()
        {
            // Consumir el preload primero.
            _ads.ShowRewardedAd(r => { });
            var fresh = new MockAdsProvider(); // vacío, sin ready
            _ads.SetProviderForTests(fresh);

            bool? earned = null;
            _ads.ShowRewardedAd((e, r) => earned = e);
            Assert.IsFalse(earned.Value, "must not grant reward when ad unavailable");
            Assert.AreEqual(0, fresh.RewardedShownCount);
        }

        [Test]
        public void ShowRewardedAd_BlockedDuringGameplay()
        {
            _phase = "ingame";
            bool? earned = null;
            _ads.ShowRewardedAd((e, r) => earned = e);

            Assert.IsFalse(earned.Value);
            Assert.AreEqual(0, _provider.RewardedShownCount, "no impression during gameplay");
        }

        [Test]
        public void Interstitial_AtMostOnceEveryThreeLevels()
        {
            _ads.NotifyLevelCompleted(); // nivel 1 -> no toca
            _ads.ShowInterstitial();
            Assert.AreEqual(0, _provider.InterstitialShownCount);

            _ads.NotifyLevelCompleted(); // nivel 2 -> no toca
            _ads.ShowInterstitial();
            Assert.AreEqual(0, _provider.InterstitialShownCount);

            _ads.NotifyLevelCompleted(); // nivel 3 -> sí
            _ads.ShowInterstitial();
            Assert.AreEqual(1, _provider.InterstitialShownCount);

            // Llamada extra inmediata: contador reiniciado, no se repite.
            _ads.ShowInterstitial();
            Assert.AreEqual(1, _provider.InterstitialShownCount);
        }

        [Test]
        public void Interstitial_BlockedDuringGameplay()
        {
            _ads.NotifyLevelCompleted();
            _ads.NotifyLevelCompleted();
            _ads.NotifyLevelCompleted();
            _phase = "board_active";
            _ads.ShowInterstitial();
            Assert.AreEqual(0, _provider.InterstitialShownCount);
        }

        [Test]
        public void Rewarded_SkippedByUser_DoesNotGrantHuellas()
        {
            _provider.RewardedShouldEarn = false;
            _ads.ShowRewardedAd(result => Assert.IsFalse(result.Earned));
            Assert.AreEqual(0, Progression.CurrencyWallet.PeekBalance());
        }

        [Test]
        public void Rewarded_HostHandlerWired_WalletNotDoubleCredited()
        {
            int granted = 0;
            _ads.OnHuellasRewardGranted += amount => granted += amount;
            _ads.ShowRewardedAd(result => { });

            Assert.AreEqual(50, granted);
            Assert.AreEqual(0, Progression.CurrencyWallet.PeekBalance(),
                "host handler owns crediting; default must not double-add");
        }
    }
}
