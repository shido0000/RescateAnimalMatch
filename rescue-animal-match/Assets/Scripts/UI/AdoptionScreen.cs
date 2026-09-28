using System;
using System.Collections.Generic;
using UnityEngine;
using RescueAnimalMatch.Backend;

namespace RescueAnimalMatch.UI
{
    /// <summary>
    /// Controller for the AdoptionScreen scene. Queries verified shelters,
    /// flattens their adoptable animals and exposes contact actions (email or
    /// WhatsApp). Pure data methods are static so EditMode tests can run them
    /// without a GameObject.
    /// </summary>
    public class AdoptionScreen : MonoBehaviour
    {
        [SerializeField] private IFirestoreBackend _injectedBackend;

        public event Action<ShelterData[]> OnSheltersLoaded;
        public event Action<string> OnContactOpened;   // localization key on failure

        private IFirestoreBackend _backend;
        public ShelterData[] Shelters { get; private set; } = Array.Empty<ShelterData>();

        private void Awake()
        {
            _backend = _injectedBackend;
        }

        public void AttachBackend(IFirestoreBackend backend)
        {
            _backend = backend;
        }

        public void LoadAdoptableAnimals(Action<bool> onDone = null)
        {
            if (_backend == null)
            {
                onDone?.Invoke(false);
                return;
            }

            _backend.GetVerifiedShelters(result =>
            {
                if (result.Success)
                {
                    Shelters = result.Data ?? Array.Empty<ShelterData>();
                    OnSheltersLoaded?.Invoke(Shelters);
                }
                else
                {
                    Debug.LogWarning($"[AdoptionScreen] {result.Error}");
                    OnContactOpened?.Invoke("adoption_error_load");
                }
                onDone?.Invoke(result.Success);
            });
        }

        /// <summary>All animals of the given shelter matching a species filter (null = all).</summary>
        public static IEnumerable<Animal> FilterAnimals(ShelterData[] shelters, string species)
        {
            var list = new List<Animal>();
            if (shelters == null) return list;
            foreach (var shelter in shelters)
            {
                if (shelter == null || shelter.animals == null) continue;
                foreach (var animal in shelter.animals)
                {
                    if (animal == null) continue;
                    if (string.IsNullOrEmpty(species) ||
                        string.Equals(animal.species, species, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(animal);
                    }
                }
            }
            return list;
        }

        /// <summary>Builds the mailto/wa.me URI for an animal's shelter contact.</summary>
        public static string BuildContactUri(ShelterData shelter, Animal animal)
        {
            if (shelter == null || string.IsNullOrEmpty(shelter.contact)) return null;
            string subject = Uri.EscapeDataString(
                $"Adopción responsable: {animal?.name ?? ""} ({animal?.species ?? ""})");
            string body = Uri.EscapeDataString(
                "Hola, vi a este animal en Rescate Animal Match y quiero adoptar responsablemente.");
            if (shelter.contact.StartsWith("mailto:"))
            {
                return $"{shelter.contact}?subject={subject}&body={body}";
            }
            if (shelter.contact.StartsWith("https://wa.me") || shelter.contact.StartsWith("http://wa.me"))
            {
                return $"{shelter.contact}?text={body} {subject}";
            }
            return shelter.contact;
        }

        public void OpenContact(ShelterData shelter, Animal animal)
        {
            string uri = BuildContactUri(shelter, animal);
            if (string.IsNullOrEmpty(uri))
            {
                OnContactOpened?.Invoke("adoption_error_no_contact");
                return;
            }

            try
            {
                Application.OpenURL(uri);
                OnContactOpened?.Invoke("adoption_contact_ok");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdoptionScreen] OpenURL failed: {e.Message}");
                OnContactOpened?.Invoke("adoption_error_open");
            }
        }
    }
}
