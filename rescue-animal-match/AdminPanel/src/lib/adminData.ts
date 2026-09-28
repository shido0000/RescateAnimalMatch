/**
 * Admin data access layer.
 *
 * In production these functions hit Firestore via the Firebase SDK with an
 * admin-privileged session (Custom Token + Cloud Functions proxy). For the
 * build/test pipeline (and local `npm run dev` without credentials) they fall
 * back to deterministic in-memory fixtures so every page renders and the
 * TypeScript build stays green.
 */
import type {
  Donation,
  ImpactMetrics,
  LevelMeta,
  PushNotification,
  Shelter,
  WeeklyGoal,
} from "./types";

// ---------------------------------------------------------------------------
// Fixtures (shape-compatible with Backend/functions/src/types.ts)
// ---------------------------------------------------------------------------

export const fixtureWeeklyGoal: WeeklyGoal = {
  targetHuellas: 10000,
  currentHuellas: 4250,
  shelterId: "shelter_patitas_cdmx",
  shelterName: "Refugio Patitas CDMX",
  rewardDescription: "500 kg de alimento y esterilización de 20 animales",
};

export const fixtureShelters: Shelter[] = [
  {
    id: "shelter_patitas_cdmx",
    name: "Refugio Patitas CDMX",
    location: "Ciudad de México, MX",
    contact: "adoptas@patitas.mx",
    needs: ["alimento", "mantas", "veterinario"],
    animals: [
      { id: "a1", name: "Luna", species: "perro", ageMonths: 18 },
      { id: "a2", name: "Michi", species: "gato", ageMonths: 8 },
    ],
    verified: true,
  },
  {
    id: "shelter_guadalajara",
    name: "Huellitas Guadalajara",
    location: "Guadalajara, MX",
    contact: "hola@huellitas.mx",
    needs: ["correas", "arena"],
    animals: [{ id: "b1", name: "Tobias", species: "perro", ageMonths: 36 }],
    verified: true,
  },
  {
    id: "shelter_pending",
    name: "Albergue Esperanza (pendiente)",
    location: "Monterrey, MX",
    contact: "contacto@esperanza.mx",
    needs: [],
    animals: [],
    verified: false,
  },
];

export const fixtureDonations: Donation[] = [
  {
    id: "d_2026_w36",
    shelterId: "shelter_guadalajara",
    shelterName: "Huellitas Guadalajara",
    amountUsd: 1200,
    huellas: 10000,
    date: "2026-09-07",
    type: "weekly_goal",
    verified: true,
    proofUrl: "https://storage/receipts/d_2026_w36.pdf",
  },
  {
    id: "d_2026_w37",
    shelterId: "shelter_patitas_cdmx",
    shelterName: "Refugio Patitas CDMX",
    amountUsd: 480,
    huellas: 4250,
    date: "2026-09-28",
    type: "weekly_goal",
    verified: false,
  },
];

export const fixtureLevels: LevelMeta[] = Array.from({ length: 30 }, (_, i) => {
  const n = i + 1;
  const difficulty = n <= 10 ? "easy" : n <= 20 ? "medium" : "hard";
  return {
    levelNumber: n,
    levelName: `Nivel ${n}`,
    movesLimit: difficulty === "easy" ? 28 : difficulty === "medium" ? 22 : 17,
    objectiveCount: difficulty === "easy" ? 1 : difficulty === "medium" ? 2 : 3,
    difficulty,
  };
});

export const fixturePushes: PushNotification[] = [
  {
    id: "p1",
    title: "¡Meta semanal alcanzada!",
    body: "Donamos 1200 USD a Huellitas Guadalajara 🐾",
    sent: true,
  },
  {
    id: "p2",
    title: "Nuevo evento comunitario",
    body: "Esta semana ayudamos a Refugio Patitas CDMX",
    sent: false,
  },
];

// ---------------------------------------------------------------------------
// API surface used by pages / routes
// ---------------------------------------------------------------------------

const delay = (ms = 0) => new Promise((r) => setTimeout(r, ms));

let sheltersState: Shelter[] = JSON.parse(JSON.stringify(fixtureShelters));
let donationsState: Donation[] = JSON.parse(
  JSON.stringify(fixtureDonations),
);
let goalState: WeeklyGoal = { ...fixtureWeeklyGoal };

export async function getWeeklyGoal(): Promise<WeeklyGoal> {
  await delay();
  return { ...goalState };
}

export async function setWeeklyGoal(goal: WeeklyGoal): Promise<WeeklyGoal> {
  await delay();
  goalState = { ...goal, currentHuellas: 0 }; // new event resets progress
  return { ...goalState };
}

export async function listShelters(): Promise<Shelter[]> {
  await delay();
  return JSON.parse(JSON.stringify(sheltersState));
}

export async function upsertShelter(shelter: Shelter): Promise<Shelter> {
  await delay();
  const idx = sheltersState.findIndex((s) => s.id === shelter.id);
  if (idx >= 0) sheltersState[idx] = shelter;
  else sheltersState.push(shelter);
  return JSON.parse(JSON.stringify(shelter));
}

export async function deleteShelter(id: string): Promise<void> {
  await delay();
  sheltersState = sheltersState.filter((s) => s.id !== id);
}

export async function listDonations(): Promise<Donation[]> {
  await delay();
  return JSON.parse(JSON.stringify(donationsState));
}

export async function verifyDonation(
  id: string,
  proofUrl?: string,
): Promise<Donation> {
  await delay();
  const d = donationsState.find((x) => x.id === id);
  if (!d) throw new Error(`donation_not_found:${id}`);
  d.verified = true;
  if (proofUrl) d.proofUrl = proofUrl;
  return JSON.parse(JSON.stringify(d));
}

export async function computeMetrics(): Promise<ImpactMetrics> {
  await delay();
  const verified = sheltersState.filter((s) => s.verified);
  return {
    totalPlayers: 18342,
    totalHuellasAllTime: 1_820_450,
    weeklyGoal: await getWeeklyGoal(),
    activeDonations: donationsState.filter((d) => !d.verified).length,
    verifiedDonations: donationsState.filter((d) => d.verified).length,
    totalDonatedUsd: donationsState
      .filter((d) => d.verified)
      .reduce((acc, d) => acc + d.amountUsd, 0),
    revenueUsd: 4_132.5,
    verifiedShelters: verified.length,
    adoptableAnimals: verified.reduce((acc, s) => acc + s.animals.length, 0),
  };
}

export async function listLevelMeta(): Promise<LevelMeta[]> {
  await delay();
  return [...fixtureLevels];
}

export async function listPushes(): Promise<PushNotification[]> {
  await delay();
  return JSON.parse(JSON.stringify(fixturePushes));
}

export async function createPush(
  push: Omit<PushNotification, "id" | "sent">,
): Promise<PushNotification> {
  await delay();
  const created: PushNotification = {
    ...push,
    id: `p${Date.now()}`,
    sent: false,
  };
  fixturePushes.push(created);
  return created;
}
