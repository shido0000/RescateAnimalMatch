/**
 * Rescate Animal Match — Admin Panel data model.
 * Mirrors the Firestore schema defined in Backend/functions/src/types.ts
 * so the panel and Cloud Functions stay in sync.
 */

export type Role = "admin" | "editor" | "viewer";

export interface AdminUser {
  uid: string;
  email: string;
  displayName?: string;
  role: Role;
}

/** config/weekly_goal document */
export interface WeeklyGoal {
  targetHuellas: number;
  currentHuellas: number;
  shelterId: string;
  shelterName: string;
  rewardDescription: string;
  startedAt?: string;
  endsAt?: string;
}

/** shelters/{id} document */
export interface ShelterAnimal {
  id: string;
  name: string;
  species: string;
  ageMonths: number;
  photoUrl?: string;
  description?: string;
}

export interface Shelter {
  id: string;
  name: string;
  location: string;
  contact: string;
  needs: string[];
  animals: ShelterAnimal[];
  verified: boolean;
}

/** donations/{id} document */
export type DonationType = "weekly_goal" | "donation_pack" | "manual";

export interface Donation {
  id: string;
  shelterId: string;
  shelterName?: string;
  amountUsd: number;
  huellas: number;
  date: string; // ISO
  type: DonationType;
  verified: boolean;
  proofUrl?: string;
  notes?: string;
}

/** Aggregated dashboard metrics (GET /api/metrics) */
export interface ImpactMetrics {
  totalPlayers: number;
  totalHuellasAllTime: number;
  weeklyGoal: WeeklyGoal | null;
  activeDonations: number;
  verifiedDonations: number;
  totalDonatedUsd: number;
  revenueUsd: number;
  verifiedShelters: number;
  adoptableAnimals: number;
}

/** content/levels metadata row (managed on /content) */
export interface LevelMeta {
  levelNumber: number;
  levelName: string;
  movesLimit: number;
  objectiveCount: number;
  difficulty: "easy" | "medium" | "hard";
}

/** push notification draft (managed on /content) */
export interface PushNotification {
  id: string;
  title: string;
  body: string;
  scheduledAt?: string;
  sent: boolean;
}

export function isWeeklyGoalComplete(goal: WeeklyGoal): boolean {
  return goal.currentHuellas >= goal.targetHuellas;
}

export function goalProgressRatio(goal: WeeklyGoal): number {
  if (goal.targetHuellas <= 0) return 0;
  return Math.min(1, goal.currentHuellas / goal.targetHuellas);
}
