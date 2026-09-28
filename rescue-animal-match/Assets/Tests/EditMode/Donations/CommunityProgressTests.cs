using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Backend;
using RescueAnimalMatch.Backend.Mocks;
using RescueAnimalMatch.Donations;

namespace RescueAnimalMatch.Tests.EditMode.Donations
{
    /// <summary>
    /// Task 4 verification: mock Firestore with local JSON and assert that
    /// ContributeHuellas(1000) updates the community progress correctly.
    /// </summary>
    public class CommunityProgressTests
    {
        private const string SeedJson = @"{
          ""weekly_goal"": {
            ""targetHuellas"": 5000,
            ""currentHuellas"": 1000,
            ""shelterId"": ""sh_1"",
            ""shelterName"": ""Refugio Test"",
            ""rewardDescription"": ""Alimento para 20 animales""
          },
          ""shelters"": [
            { ""id"": ""sh_1"", ""name"": ""Refugio Test"", ""location"": ""Bogotá"",
              ""contact"": ""mailto:t@t.co"", ""needs"": [], ""verified"": true, ""animals"": [] },
            { ""id"": ""sh_2"", ""name"": ""Sin verificar"", ""location"": ""Cali"",
              ""contact"": """", ""needs"": [], ""verified"": false, ""animals"": [] }
          ],
          ""donations"": []
        }";

        private GameObject _go;
        private CommunityProgress _progress;
        private MockFirestoreBackend _backend;

        [SetUp]
        public void SetUp()
        {
            DonationTracker.ResetForTests();
            _backend = new MockFirestoreBackend(SeedJson);
            _go = new GameObject("CommunityProgress");
            _progress = _go.AddComponent<CommunityProgress>();
            _progress.AttachBackend(_backend);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            DonationTracker.ResetForTests();
        }

        [Test]
        public void ContributeHuellas1000_IncreasesLocalProgressBy1000()
        {
            BackendResult<WeeklyGoalSnapshot> captured = null;
            _progress.ContributeHuellas(1000, r => captured = r);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Success, Is.True);
            Assert.That(captured.Data.currentHuellas, Is.EqualTo(2000),
                "seed 1000 + contribution 1000 must equal 2000");
            Assert.That(_progress.Current.currentHuellas, Is.EqualTo(2000));
        }

        [Test]
        public void ContributeHuellas_UpdatesProgressBarRatio()
        {
            _progress.ContributeHuellas(1500);
            // (1000 + 1500) / 5000 = 0.5
            Assert.That(_progress.Progress, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void GoalUpdated_FiresWithNewSnapshot()
        {
            WeeklyGoalSnapshot latest = null;
            _progress.GoalUpdated += s => latest = s;
            _progress.ContributeHuellas(250);
            Assert.That(latest, Is.Not.Null);
            Assert.That(latest.currentHuellas, Is.EqualTo(1250));
        }

        [Test]
        public void ReachingGoal_FiresGoalReachedAndCreatesDonationOnce()
        {
            int reachedCount = 0;
            int confirmedCount = 0;
            _progress.GoalReached += _ => reachedCount++;
            DonationTracker.OnDonationConfirmed += _ => confirmedCount++;

            _progress.ContributeHuellas(4000);   // 1000+4000 = 5000 -> reached
            _progress.ContributeHuellas(500);    // over-contribution, no re-trigger

            Assert.That(_progress.IsGoalReached, Is.True);
            Assert.That(reachedCount, Is.EqualTo(1));
            Assert.That(confirmedCount, Is.EqualTo(1));
            Assert.That(DonationTracker.History.Count, Is.EqualTo(1));
            var record = DonationTracker.LastConfirmed;
            Assert.That(record.shelterId, Is.EqualTo("sh_1"));
            Assert.That(record.amount, Is.EqualTo(5000));
            Assert.That(record.type, Is.EqualTo("weekly_goal"));
            Assert.That(record.verified, Is.False);
            // The mock backend mirrors the onDonationReached Cloud Function:
            Assert.That(_backend.Donations.Count, Is.EqualTo(1));
        }

        [Test]
        public void ZeroOrNegativeContribution_IsRejected()
        {
            BackendResult<WeeklyGoalSnapshot> captured = null;
            _progress.ContributeHuellas(0, r => captured = r);
            Assert.That(captured.Success, Is.False);
            Assert.That(_progress.Current.currentHuellas, Is.EqualTo(1000));
        }

        [Test]
        public void NoBackend_ContributionFailsGracefully()
        {
            _progress.AttachBackend(null);
            BackendResult<WeeklyGoalSnapshot> captured = null;
            _progress.ContributeHuellas(100, r => captured = r);
            Assert.That(captured.Success, Is.False);
        }

        [Test]
        public void RealtimeSubscription_ReceivesOtherPlayersContributions()
        {
            int pushes = 0;
            _progress.GoalUpdated += _ => pushes++;
            int before = pushes;

            // Simulate another player contributing through the same doc.
            _backend.IncrementWeeklyGoal(300);
            Assert.That(_progress.Current.currentHuellas, Is.EqualTo(1300));
            Assert.That(pushes, Is.GreaterThan(before));
        }

        [Test]
        public void GetVerifiedShelters_FiltersUnverified()
        {
            ShelterData[] shelters = null;
            _backend.GetVerifiedShelters(r => shelters = r.Data);
            Assert.That(shelters.Length, Is.EqualTo(1));
            Assert.That(shelters[0].id, Is.EqualTo("sh_1"));
        }
    }
}
