using System;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Core
{
    /// <summary>
    /// Envolvente de Crashlytics (Task 9). Detrás de USE_FIREBASE delega a
    /// FirebaseCrashlytics; fuera de él (editor/tests) registra en memoria y
    /// en Debug.Log. Nunca lanza excepciones desde el camino de error.
    /// </summary>
    public interface ICrashReporter
    {
        void SetCustomKey(string key, string value);
        void LogNonFatal(Exception error, string message = null);
        void RecordBreadcrumb(string message);
    }

    /// <summary>Implementación en memoria para tests y editor.</summary>
    public sealed class MockCrashReporter : ICrashReporter
    {
        public sealed class NonFatalEntry
        {
            public string Message;
            public Exception Error;
            public IReadOnlyDictionary<string, string> Keys;
        }

        private readonly Dictionary<string, string> _keys = new Dictionary<string, string>();
        private readonly List<NonFatalEntry> _nonFatals = new List<NonFatalEntry>();
        private readonly List<string> _breadcrumbs = new List<string>();

        public IReadOnlyList<NonFatalEntry> NonFatals => _nonFatals;
        public IReadOnlyList<string> Breadcrumbs => _breadcrumbs;

        public void SetCustomKey(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _keys[key] = value ?? string.Empty;
        }

        public void LogNonFatal(Exception error, string message = null)
        {
            if (error == null) return;
            _nonFatals.Add(new NonFatalEntry
            {
                Message = message ?? error.Message,
                Error = error,
                Keys = new Dictionary<string, string>(_keys),
            });
            Debug.Log($"[Crashlytics-Mock] non-fatal: {message} | {error.Message}");
        }

        public void RecordBreadcrumb(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _breadcrumbs.Add(message);
        }

        public bool HasKey(string key) => _keys.ContainsKey(key);
        public string GetKey(string key) => _keys.TryGetValue(key, out var v) ? v : null;
    }

#if USE_FIREBASE
    /// <summary>Adaptador real de Firebase Crashlytics.</summary>
    public sealed class FirebaseCrashReporter : ICrashReporter
    {
        public void SetCustomKey(string key, string value)
        {
            try { Firebase.Crashlytics.Crashlytics.Log(key + "=" + value); }
            catch (Exception e) { Debug.LogWarning("Crashlytics SetCustomKey failed: " + e.Message); }
        }

        public void LogNonFatal(Exception error, string message = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(message)) Firebase.Crashlytics.Crashlytics.Log(message);
                Firebase.Crashlytics.Crashlytics.LogException(error);
            }
            catch (Exception e) { Debug.LogWarning("Crashlytics Log failed: " + e.Message); }
        }

        public void RecordBreadcrumb(string message)
        {
            try { Firebase.Crashlytics.Crashlytics.Log(message); }
            catch { /* never crash the game because of telemetry */ }
        }
    }
#endif

    /// <summary>Punto de acceso global (inicializado por GameBootstrap).</summary>
    public static class CrashReport
    {
        public const string KeyLevelNumber = "level_number";
        public const string KeyBoardStateHash = "board_state_hash";

        private static ICrashReporter _current = new MockCrashReporter();
        public static ICrashReporter Current => _current;

        public static void Init(ICrashReporter reporter)
        {
            _current = reporter ?? new MockCrashReporter();
        }

        public static void SetBoardContext(int levelNumber, string boardStateHash)
        {
            _current?.SetCustomKey(KeyLevelNumber, levelNumber.ToString());
            _current?.SetCustomKey(KeyBoardStateHash, boardStateHash ?? string.Empty);
        }

        public static void NonFatal(Exception error, string message = null)
        {
            try { _current?.LogNonFatal(error, message); }
            catch (Exception e) { Debug.LogError("Crash reporter itself failed: " + e.Message); }
        }
    }
}
