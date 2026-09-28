using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RescueAnimalMatch.Backend;
using RescueAnimalMatch.Backend.Mocks;
using RescueAnimalMatch.UI;

namespace RescueAnimalMatch.Tests.EditMode.Donations
{
    public class AdoptionScreenTests
    {
        private const string SeedJson = @"{
          ""weekly_goal"": { ""targetHuellas"": 100, ""currentHuellas"": 0,
            ""shelterId"": ""s1"", ""shelterName"": ""A"", ""rewardDescription"": ""r"" },
          ""shelters"": [
            { ""id"": ""s1"", ""name"": ""Patitas"", ""location"": ""Bogotá"",
              ""contact"": ""mailto:adopciones@patitas.co"", ""needs"": [""alimento""],
              ""verified"": true,
              ""animals"": [
                { ""id"": ""a1"", ""name"": ""Max"", ""species"": ""perro"", ""ageMonths"": 26,
                  ""photoUrl"": ""https://x/max.jpg"", ""description"": ""d"",
                  ""sterilized"": true, ""vaccinated"": true },
                { ""id"": ""a2"", ""name"": ""Luna"", ""species"": ""gato"", ""ageMonths"": 8,
                  ""photoUrl"": ""https://x/luna.jpg"", ""description"": ""d"",
                  ""sterilized"": false, ""vaccinated"": true } ] },
            { ""id"": ""s2"", ""name"": ""Huellitas"", ""location"": ""Medellín"",
              ""contact"": ""https://wa.me/573001112233"", ""needs"": [],
              ""verified"": true,
              ""animals"": [
                { ""id"": ""a3"", ""name"": ""Tobi"", ""species"": ""perro"", ""ageMonths"": 48,
                  ""photoUrl"": """", ""description"": ""d"",
                  ""sterilized"": true, ""vaccinated"": true } ] },
            { ""id"": ""s3"", ""name"": ""NoVerificado"", ""location"": ""Cali"",
              ""contact"": ""mailto:x@x.co"", ""needs"": [], ""verified"": false, ""animals"": [] }
          ],
          ""donations"": []
        }";

        private GameObject _go;
        private AdoptionScreen _screen;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AdoptionScreen");
            _screen = _go.AddComponent<AdoptionScreen>();
            _screen.AttachBackend(new MockFirestoreBackend(SeedJson));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void LoadAdoptableAnimals_ReturnsOnlyVerifiedShelters()
        {
            bool ok = false;
            _screen.LoadAdoptableAnimals(v => ok = v);
            Assert.That(ok, Is.True);
            Assert.That(_screen.Shelters.Length, Is.EqualTo(2));
            Assert.That(_screen.Shelters.Any(s => s.id == "s3"), Is.False,
                "unverified shelters must never be displayed");
        }

        [Test]
        public void FilterAnimals_BySpecies()
        {
            var dogs = AdoptionScreen.FilterAnimals(_screen.Shelters ?? new ShelterData[0], "perro")
                .ToList();
            // Load synchronously first (mock backend is immediate).
            _screen.LoadAdoptableAnimals();
            dogs = AdoptionScreen.FilterAnimals(_screen.Shelters, "perro").ToList();
            Assert.That(dogs.Count, Is.EqualTo(2));
            Assert.That(dogs.All(a => a.species == "perro"), Is.True);

            var all = AdoptionScreen.FilterAnimals(_screen.Shelters, null).ToList();
            Assert.That(all.Count, Is.EqualTo(3));
        }

        [Test]
        public void BuildContactUri_EmailShelter_ProducesMailtoWithSubject()
        {
            _screen.LoadAdoptableAnimals();
            var shelter = _screen.Shelters.First(s => s.id == "s1");
            var animal = shelter.animals[0];
            string uri = AdoptionScreen.BuildContactUri(shelter, animal);
            Assert.That(uri, Does.StartWith("mailto:adopciones@patitas.co?subject="));
            Assert.That(uri, Does.Contain("body="));
        }

        [Test]
        public void BuildContactUri_WhatsappShelter_ProducesWaMeText()
        {
            _screen.LoadAdoptableAnimals();
            var shelter = _screen.Shelters.First(s => s.id == "s2");
            string uri = AdoptionScreen.BuildContactUri(shelter, shelter.animals[0]);
            Assert.That(uri, Does.StartWith("https://wa.me/573001112233?text="));
        }

        [Test]
        public void Animal_FormattedAge()
        {
            Assert.That(new Animal { ageMonths = 8 }.FormattedAge(), Is.EqualTo("8 meses"));
            Assert.That(new Animal { ageMonths = 24 }.FormattedAge(), Is.EqualTo("2 años"));
            Assert.That(new Animal { ageMonths = 26 }.FormattedAge(), Is.EqualTo("2 años 2 meses"));
        }
    }
}
