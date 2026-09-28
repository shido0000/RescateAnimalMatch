using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Backend.Mocks;
using RescueAnimalMatch.Donations;
using RescueAnimalMatch.Progression;

namespace RescueAnimalMatch.Tests.EditMode.Donations
{
    public class CurrencyManagerTests
    {
        private GameObject _go;
        private GameObject _progressGo;
        private CurrencyManager _currency;
        private MockFirestoreBackend _backend;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(CurrencyWallet.HuellasKey);
            PlayerPrefs.DeleteKey("RAM_TotalContributed");
            DonationTracker.ResetForTests();

            _backend = new MockFirestoreBackend(CommunityProgressSeed());
            _progressGo = new GameObject("CommunityProgress");
            var progress = _progressGo.AddComponent<CommunityProgress>();
            progress.AttachBackend(_backend);

            _go = new GameObject("CurrencyManager");
            _currency = _go.AddComponent<CurrencyManager>();
            _currency.AttachBackend(_backend);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_progressGo);
            PlayerPrefs.DeleteKey(CurrencyWallet.HuellasKey);
            PlayerPrefs.DeleteKey("RAM_TotalContributed");
            DonationTracker.ResetForTests();
        }

        private static string CommunityProgressSeed()
        {
            return @"{ ""weekly_goal"": { ""targetHuellas"": 5000, ""currentHuellas"": 1000,
                     ""shelterId"": ""sh_1"", ""shelterName"": ""Refugio Test"",
                     ""rewardDescription"": ""x"" },
                     ""shelters"": [], ""donations"": [] }";
        }

        [Test]
        public void AddHuellas_RaisesBalanceAndFiresEvent()
        {
            int lastSeen = -1;
            _currency.OnHuellasChanged += v => lastSeen = v;
            _currency.AddHuellas(120);
            Assert.That(_currency.Huellas, Is.EqualTo(120));
            Assert.That(lastSeen, Is.EqualTo(120));
        }

        [Test]
        public void SpendHuellas_InsufficientFunds_ReturnsFalseAndKeepsBalance()
        {
            _currency.AddHuellas(50);
            Assert.That(_currency.SpendHuellas(80), Is.False);
            Assert.That(_currency.Huellas, Is.EqualTo(50));
        }

        [Test]
        public void Balance_PersistsToPlayerPrefs()
        {
            _currency.AddHuellas(300);
            Assert.That(PlayerPrefs.GetInt(CurrencyWallet.HuellasKey, -1), Is.EqualTo(300));
        }

        [Test]
        public void SyncToCloud_WritesBalanceToUserDocument()
        {
            _currency.AddHuellas(420);
            bool ok = false;
            _currency.SyncToCloud(v => ok = v);
            Assert.That(ok, Is.True);

            int cloudValue = -1;
            _backend.LoadUserCurrency(r => cloudValue = r.Data);
            Assert.That(cloudValue, Is.EqualTo(420));
        }

        [Test]
        public void ContributeToCommunity_MovesHuellasFromWalletToGoal()
        {
            _currency.AddHuellas(1000);
            bool ok = false;
            _currency.ContributeToCommunity(600, v => ok = v);

            Assert.That(ok, Is.True);
            Assert.That(_currency.Huellas, Is.EqualTo(400));
            Assert.That(_currency.TotalContributed, Is.EqualTo(600));
            Assert.That(CommunityProgress.Instance.Current.currentHuellas, Is.EqualTo(1600));
        }

        [Test]
        public void ContributeToCommunity_WithoutFunds_RefundsNothingAndFails()
        {
            _currency.AddHuellas(100);
            bool ok = true;
            _currency.ContributeToCommunity(500, v => ok = v);
            Assert.That(ok, Is.False);
            Assert.That(_currency.Huellas, Is.EqualTo(100));
        }
    }
}
