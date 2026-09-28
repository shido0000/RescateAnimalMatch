using System;
using UnityEngine;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Punto único de selección del backend (Composition Root).
    /// Con USE_FIREBASE + google-services.json disponible usa FirebaseBackend
    /// real; en caso contrario cae a MockFirestoreBackend (editor/tests/offline).
    /// Ningún sistema de juego recibe IFirestoreBackend concreto: siempre la interfaz.
    /// </summary>
    public static class BackendBootstrap
    {
        private static IFirestoreBackend _current;

        /// <summary>Backend activo; se crea perezosamente según defines/config.</summary>
        public static IFirestoreBackend Current
        {
            get
            {
                if (_current == null) _current = Create();
                return _current;
            }
        }

        /// <summary>Inyección para tests (pasar null restablece la detección).</summary>
        public static void OverrideForTesting(IFirestoreBackend backend)
        {
            _current = backend;
        }

        private static IFirestoreBackend Create()
        {
#if USE_FIREBASE
            try
            {
                var app = Firebase.App.FirebaseApp.DefaultInstance;
                if (app != null)
                {
                    return new Firebase.FirebaseBackend(app);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("BackendBootstrap: Firebase unavailable, falling back to mock: " + e.Message);
            }
#endif
            Debug.Log("BackendBootstrap: using MockFirestoreBackend (offline mode)");
            return new MockFirestoreBackend();
        }
    }
}
