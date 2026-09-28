using System;
using System.Collections.Generic;

namespace RescueAnimalMatch.Core
{
    /// <summary>
    /// Abstracción de analítica (Task 9). La implementación real usa Firebase
    /// Analytics detrás del define USE_FIREBASE; el modo EditMode/test usa
    /// <see cref="MockAnalyticsLogger"/>. Nunca lanza excepciones: la telemetría
    /// no debe romper el juego.
    /// </summary>
    public interface IAnalyticsLogger
    {
        void LogEvent(string eventName, IDictionary<string, object> parameters = null);
        void SetUserProperty(string key, string value);
    }

    /// <summary>Nombres canónicos de eventos exigidos por la especificación.</summary>
    public static class AnalyticsEvents
    {
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string AdWatched = "ad_watched";
        public const string IapPurchased = "iap_purchased";
        public const string HuellasContributed = "huellas_contributed";
        public const string DonationReached = "donation_reached";
        public const string ShelterViewed = "shelter_viewed";
        public const string AnimalContacted = "animal_contacted";

        /// <summary>Lista usada por los tests para validar cobertura de eventos.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            LevelStart, LevelComplete, LevelFail, AdWatched, IapPurchased,
            HuellasContributed, DonationReached, ShelterViewed, AnimalContacted
        };
    }

    /// <summary>Claves canónicas de user properties (Task 9).</summary>
    public static class AnalyticsUserProperties
    {
        public const string TotalHuellas = "total_huellas";
        public const string LevelsCompleted = "levels_completed";
        public const string PurchasesCount = "purchases_count";
    }

    /// <summary>
    /// Logger en memoria para EditMode/integración: registra todo para asserts.
    /// </summary>
    public sealed class MockAnalyticsLogger : IAnalyticsLogger
    {
        public sealed class Entry
        {
            public string EventName;
            public IReadOnlyDictionary<string, object> Parameters;

            public Entry(string name, IDictionary<string, object> p)
            {
                EventName = name;
                Parameters = p == null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(p);
            }
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<string, string> _userProps = new Dictionary<string, string>();

        public IReadOnlyList<Entry> Entries => _entries;
        public IReadOnlyDictionary<string, string> UserProperties => _userProps;

        public void LogEvent(string eventName, IDictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            _entries.Add(new Entry(eventName, parameters));
        }

        public void SetUserProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _userProps[key] = value ?? string.Empty;
        }

        public bool HasEvent(string eventName) =>
            _entries.Exists(e => e.EventName == eventName);

        public int CountOf(string eventName) =>
            _entries.FindAll(e => e.EventName == eventName).Count;

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
