#if USE_FIREBASE
using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Functions;
using UnityEngine;

namespace RescueAnimalMatch.Backend.Firebase
{
    /// <summary>
    /// Auth + Functions transport para FirebaseBackend (Task 6).
    /// Anonimiza al usuario al arrancar y expone InvokeAsync sobre el
    /// Cloud Functions wrapper. Parcial: los métodos de Firestore viven en
    /// FirebaseBackend.Firestore.cs.
    /// </summary>
    public sealed partial class FirebaseBackend : IDisposable
    {
        private readonly FirebaseAuth _auth;
        private readonly FirebaseFunctions _functions;
        private string _userId;

        public string UserId => _userId;

        /// <summary>Project id desde google-services.json (nunca hardcodeado).</summary>
        public static string ProjectId
        {
            get { string id; return FirebaseConfig.TryGetProjectId(out id) ? id : null; }
        }

        public FirebaseBackend(FirebaseApp app)
        {
            _auth = FirebaseAuth.DefaultInstance;
            _functions = FirebaseFunctions.DefaultInstance;
            _userId = _auth.CurrentUser?.UserId;
            EnsureAnonymousAsync();
        }

        /// <summary>Firma anónima idempotente; reutiliza el usuario actual.</summary>
        private void EnsureAnonymousAsync()
        {
            if (_auth.CurrentUser != null)
            {
                _userId = _auth.CurrentUser.UserId;
                return;
            }
            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("FirebaseBackend: anonymous sign-in failed: " +
                                   (task.Exception?.InnerException?.Message ?? "unknown"));
                    return;
                }
                _userId = task.Result.UserId;
            });
        }

        /// <summary>Llama un callable con reintentos simples de backoff.</summary>
        private void CallFunction<TReq, TRes>(
            string name,
            TReq request,
            Action<BackendResult<TRes>> onResult)
        {
            try
            {
                _functions.GetHttpsReference(name)
                          .CallAsync(Serialize(request))
                          .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        var inner = task.Exception != null
                            ? (task.Exception.InnerException as FunctionsException)
                            : null;
                        var msg = inner != null ? inner.Reason ?? inner.Message
                                                : (task.Exception?.InnerException?.Message ?? "function_error");
                        onResult?.Invoke(BackendResult<TRes>.Fail(msg));
                        return;
                    }
                    var raw = task.Result; // Dictionary<string, object> from JSON
                    onResult?.Invoke(BackendResult<TRes>.Ok(Deserialize<TRes>(raw)));
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("FirebaseBackend.CallFunction: " + e.Message);
                onResult?.Invoke(BackendResult<TRes>.Fail("transport_error"));
            }
        }

        private static Dictionary<string, object> Serialize<TReq>(TReq request)
        {
            // Los payloads son planos (amount, productId, token): JsonUtility → dict.
            var json = JsonUtility.ToJson(request);
            return JsonToDictionary(json);
        }

        private static TRes Deserialize<TRes>(Dictionary<string, object> raw)
        {
            var json = MiniJson.Serialize(raw);
            return JsonUtility.FromJson<TRes>(json);
        }

        private static Dictionary<string, object> JsonToDictionary(string json)
        {
            return MiniJson.Parse(json) as Dictionary<string, object>
                   ?? new Dictionary<string, object>();
        }

        public void Dispose()
        {
            // Las singletons de Firebase no se destruyen; sólo desconectamos listeners.
        }
    }
}
#endif
