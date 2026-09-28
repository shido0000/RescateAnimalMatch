/**
 * Shared types for Rescate Animal Match Cloud Functions.
 * These mirror the C# models in Assets/Scripts (Backend/, Donations/) —
 * keep both sides in sync when evolving the schema.
 */

/** Document config/weekly_goal */
export interface WeeklyGoal {
  targetHuellas: number;
  currentHuellas: number;
  shelterId: string;
  shelterName: string;
  rewardDescription: string;
  /** ISO-8601 week key, e.g. "2026-W40". Used to reset weekly. */
  weekKey: string;
  /** True once onDonationReached fired for this cycle (idempotency guard). */
  donationFired: boolean;
}

/** Document donations/{id} — written only by Cloud Functions. */
export interface DonationDoc {
  shelterId: string;
  shelterName: string;
  amount: number;
  date: string; // ISO-8601 UTC
  type: 'weekly_goal' | 'donation_pack' | 'ad_revenue_share';
  verified: boolean;
  proofUrl?: string;
}

/** Nested animal inside shelters/{id}.animals[] */
export interface AnimalDoc {
  id: string;
  name: string;
  species: string;
  ageMonths: number;
  photoUrl: string;
  description: string;
  sterilized: boolean;
  vaccinated: boolean;
}

/** Document shelters/{id} */
export interface ShelterDoc {
  name: string;
  location: string;
  contact: string;
  needs: string[];
  animals: AnimalDoc[];
  verified: boolean;
}

/** Payload accepted by the contributeHuellas callable. */
export interface ContributeRequest {
  amount: number;
}

/** Response returned by contributeHuellas. */
export interface ContributeResponse {
  currentHuellas: number;
  targetHuellas: number;
  goalReached: boolean;
}

/** Payload accepted by the validatePurchase callable. */
export interface ValidatePurchaseRequest {
  productId: string;
  purchaseToken: string;
}

/** Response returned by validatePurchase. */
export interface ValidatePurchaseResponse {
  valid: boolean;
  productId: string;
  error?: string;
}
