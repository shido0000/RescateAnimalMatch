#if USE_FIREBASE
using System;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;

namespace RescueAnimalMatch.Backend.Firebase
{
    /// <summary>
    /// Implementación real de IFirestoreBackend sobre Firestore + Cloud
    /// Functions. Sustituye a MockFirestoreBackend en builds con USE_FIREBASE.
    /// Los métodos de auth/functions están en FirebaseBackend.cs (partial).
    /// </summary>
    public sealed partial class FirebaseBackend : IFirestoreBackend
    {
        private readonly FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;
        private DocumentSnapshot _goalListenerRegistration; // usado por Subscribe

        private DocumentReference GoalRef => _db.Collection("config").Document("weekly_goal");
        private DocumentReference UserRef => _db.Collection("users").Document(_userId ?? "_anon");

        // ------------------------------------------------------------------
        // config/weekly_goal
        // ------------------------------------------------------------------
        public void GetWeeklyGoal(Action<BackendResult<WeeklyGoalSnapshot>> onResult)
        {
            GoalRef.GetSnapshotAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Post(() => onResult?.Invoke(
                        BackendResult<WeeklyGoalSnapshot>.Fail("goal_fetch_failed")));
                    return;
                }
                var snap = ToGoalSnapshot(t.Result);
                Post(() => onResult?.Invoke(
                    snap == null
                        ? BackendResult<WeeklyGoalSnapshot>.Fail("goal_missing")
                        : BackendResult<WeeklyGoalSnapshot>.Ok(snap)));
            });
        }

        public void IncrementWeeklyGoal(int amount, Action<BackendResult<WeeklyGoalSnapshot>> onResult)
        {
            // El cliente NUNCA escribe config: pasa por el callable contributeHuellas.
            CallFunction<ContributeRequestDto, ContributeResponseDto>(
                "contributeHuellas",
                new ContributeRequestDto { amount = amount },
                res =>
                {
                    if (!res.Success)
                    {
                        onResult?.Invoke(BackendResult<WeeklyGoalSnapshot>.Fail(res.Error));
                        return;
                    }
                    // Re-leemos para devolver el snapshot completo post-incremento.
                    GetWeeklyGoal(onResult);
                });
        }

        public IDisposable SubscribeWeeklyGoal(Action<WeeklyGoalSnapshot> onUpdate)
        {
            var reg = GoalRef.ListenAsynchronously((docSnap, err) =>
            {
                if (err != null || docSnap == null) return;
                var snap = ToGoalSnapshot(docSnap);
                if (snap != null) Post(() => onUpdate?.Invoke(snap));
            });
            return new Subscription(() => reg.Dispose());
        }

        private WeeklyGoalSnapshot ToGoalSnapshot(DocumentSnapshot doc)
        {
            if (doc == null || !doc.Exists) return null;
            var d = doc.GetData();
            return new WeeklyGoalSnapshot
            {
                targetHuellas = GetInt(d, "targetHuellas"),
                currentHuellas = GetInt(d, "currentHuellas"),
                shelterId = GetString(d, "shelterId"),
                shelterName = GetString(d, "shelterName"),
                rewardDescription = GetString(d, "rewardDescription"),
            };
        }

        // ------------------------------------------------------------------
        // donations & shelters
        // ------------------------------------------------------------------
        public void RecordDonation(DonationRecord donation, Action<BackendResult<DonationRecord>> onResult)
        {
            // Las reglas sólo permiten escribir donations a Cloud Functions:
            // el trigger onDonationReached las crea; el cliente confirma leyendo.
            _db.Collection("donations")
               .WhereEqualTo("shelterId", donation.shelterId)
               .OrderByDescending("date")
               .Limit(1)
               .GetSnapshotAsync()
               .ContinueWith(t =>
               {
                   if (t.IsFaulted || t.Result.Count == 0)
                   {
                       Post(() => onResult?.Invoke(
                           BackendResult<DonationRecord>.Fail("donation_not_found")));
                       return;
                   }
                   var data = t.Result.Documents[0].GetData();
                   donation.id = t.Result.Documents[0].Id;
                   donation.verified = GetBool(data, "verified");
                   Post(() => onResult?.Invoke(BackendResult<DonationRecord>.Ok(donation)));
               });
        }

        public void GetVerifiedShelters(Action<BackendResult<ShelterData[]>> onResult)
        {
            _db.Collection("shelters")
               .WhereEqualTo("verified", true)
               .GetSnapshotAsync()
               .ContinueWith(t =>
               {
                   if (t.IsFaulted)
                   {
                       Post(() => onResult?.Invoke(
                           BackendResult<ShelterData[]>.Fail("shelters_fetch_failed")));
                       return;
                   }
                   var list = new List<ShelterData>();
                   foreach (var doc in t.Result.Documents) list.Add(ToShelter(doc));
                   Post(() => onResult?.Invoke(BackendResult<ShelterData[]>.Ok(list.ToArray())));
               });
        }

        private ShelterData ToShelter(DocumentSnapshot doc)
        {
            var d = doc.GetData();
            var shelter = new ShelterData
            {
                id = doc.Id,
                name = GetString(d, "name"),
                location = GetString(d, "location"),
                contact = GetString(d, "contact"),
                needs = GetStringArray(d, "needs"),
                verified = GetBool(d, "verified"),
                animals = AnimalFromDocs(GetDictArray(d, "animals")),
            };
            return shelter;
        }

        // ------------------------------------------------------------------
        // users/{uid} — escritura propia permitida por firestore.rules
        // ------------------------------------------------------------------
        public void SaveUserCurrency(int huellas, Action<BackendResult<bool>> onResult)
        {
            SaveUserData("huellas", huellas.ToString(), onResult);
        }

        public void LoadUserCurrency(Action<BackendResult<int>> onResult)
        {
            LoadUserData("huellas", res =>
            {
                if (!res.Success)
                {
                    onResult?.Invoke(BackendResult<int>.Fail(res.Error));
                    return;
                }
                int value;
                var parsed = int.TryParse(res.Data, out value);
                onResult?.Invoke(parsed
                    ? BackendResult<int>.Ok(value)
                    : BackendResult<int>.Fail("corrupt_currency_field"));
            });
        }

        public void SaveUserData(string field, string jsonValue, Action<BackendResult<bool>> onResult)
        {
            if (field == "role") // inmutable desde cliente (firestore.rules)
            {
                onResult?.Invoke(BackendResult<bool>.Fail("field_forbidden"));
                return;
            }
            UserRef.UpdateAsync(field, jsonValue).ContinueWith(t =>
                Post(() => onResult?.Invoke(t.IsFaulted
                    ? BackendResult<bool>.Fail("user_write_failed")
                    : BackendResult<bool>.Ok(true))));
        }

        public void LoadUserData(string field, Action<BackendResult<string>> onResult)
        {
            UserRef.GetSnapshotAsync().ContinueWith(t =>
            {
                if (t.IsFaulted || !t.Result.Exists)
                {
                    Post(() => onResult?.Invoke(BackendResult<string>.Ok(null)));
                    return;
                }
                object value;
                t.Result.GetData().TryGetValue(field, out value);
                Post(() => onResult?.Invoke(
                    BackendResult<string>.Ok(value as string)));
            });
        }

        // ------------------------------------------------------------------
        // Helpers de tipos Firestore → C#
        // ------------------------------------------------------------------
        private static int GetInt(IDictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? Convert.ToInt32(v) : 0;
        }

        private static string GetString(IDictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? v as string : null;
        }

        private static bool GetBool(IDictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && Convert.ToBoolean(v);
        }

        private static string[] GetStringArray(IDictionary<string, object> d, string k)
        {
            object v;
            if (!d.TryGetValue(k, out v)) return Array.Empty<string>();
            var list = v as IEnumerable<object>;
            if (list == null) return Array.Empty<string>();
            var result = new List<string>();
            foreach (var item in list) result.Add(item as string ?? "");
            return result.ToArray();
        }

        private static List<IDictionary<string, object>> GetDictArray(IDictionary<string, object> d, string k)
        {
            object v;
            var outer = d.TryGetValue(k, out v) ? v as IEnumerable<object> : null;
            var result = new List<IDictionary<string, object>>();
            if (outer == null) return result;
            foreach (var item in outer)
            {
                var dict = item as IDictionary<string, object>;
                if (dict != null) result.Add(dict);
            }
            return result;
        }

        private static Animal[] AnimalFromDocs(List<IDictionary<string, object>> rows)
        {
            var animals = new Animal[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                animals[i] = new Animal
                {
                    id = GetString(r, "id"),
                    name = GetString(r, "name"),
                    species = GetString(r, "species"),
                    ageMonths = GetInt(r, "ageMonths"),
                    photoUrl = GetString(r, "photoUrl"),
                    description = GetString(r, "description"),
                    sterilized = GetBool(r, "sterilized"),
                    vaccinated = GetBool(r, "vaccinated"),
                };
            }
            return animals;
        }

        private static void Post(Action action)
        {
            if (action == null) return;
            if (SynchronizationContext.Current != null)
            {
                SynchronizationContext.Current.Post(_ => action(), null);
            }
            else
            {
                try { action(); }
                catch (Exception e) { Debug.LogWarning("FirebaseBackend.Post: " + e.Message); }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                if (d != null) d();
            }
        }

        [Serializable]
        private class ContributeRequestDto { public int amount; }

        [Serializable]
        private class ContributeResponseDto
        {
            public int currentHuellas;
            public int targetHuellas;
            public bool goalReached;
        }
    }
}
#endif
