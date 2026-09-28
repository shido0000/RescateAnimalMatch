using System;
using UnityEngine;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Adoptable animal offered by a shelter. Mirrors the nested Animal objects
    /// inside the Firestore "shelters" collection documents.
    /// </summary>
    [Serializable]
    public class Animal
    {
        public string id;
        public string name;
        public string species;      // "perro", "gato", ...
        public int ageMonths;
        public string photoUrl;     // remote URL, never packed in the APK
        public string description;
        public bool sterilized;
        public bool vaccinated;

        /// <summary>Human-readable age, e.g. "2 años 3 meses".</summary>
        public string FormattedAge()
        {
            if (ageMonths < 12) return ageMonths + " meses";
            int years = ageMonths / 12;
            int months = ageMonths % 12;
            string label = years == 1 ? "año" : "años";
            return months == 0 ? $"{years} {label}" : $"{years} {label} {months} meses";
        }
    }

    /// <summary>
    /// Model matching the Firestore "shelters" collection:
    /// { name, location, contact, needs: string[], animals: Animal[], verified }.
    /// Only shelters with verified == true may be displayed to players.
    /// </summary>
    [Serializable]
    public class ShelterData
    {
        public string id;
        public string name;
        public string location;
        public string contact;          // email or wa.me link
        public string[] needs;
        public Animal[] animals;
        public bool verified;

        public int AnimalCount => animals == null ? 0 : animals.Length;

        public bool HasContact => !string.IsNullOrEmpty(contact);
    }
}
