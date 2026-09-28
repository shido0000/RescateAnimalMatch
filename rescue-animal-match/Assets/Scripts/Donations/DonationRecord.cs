using System;
using UnityEngine;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Document of the Firestore "donations" collection, written exclusively by
    /// the onDonationReached Cloud Function (firestore.rules):
    /// { shelterId, amount, date, type, verified }.
    /// </summary>
    [Serializable]
    public class DonationRecord
    {
        public string id;
        public string shelterId;
        public string shelterName;
        /// <summary>Amount in Huellas contributed by the community.</summary>
        public int amount;
        /// <summary>ISO-8601 UTC timestamp.</summary>
        public string date;
        /// <summary>"weekly_goal" | "donation_pack" | "ad_revenue_share".</summary>
        public string type;
        public bool verified;

        public static DonationRecord Create(string shelterId, string shelterName,
                                            int amount, string type)
        {
            return new DonationRecord
            {
                shelterId = shelterId,
                shelterName = shelterName,
                amount = amount,
                type = type,
                date = DateTime.UtcNow.ToString("o"),
                verified = false
            };
        }
    }
}
