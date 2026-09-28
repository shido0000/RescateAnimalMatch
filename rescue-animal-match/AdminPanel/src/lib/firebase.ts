/**
 * Firebase client bootstrap for the Admin Panel.
 *
 * NEVER hardcode credentials here — values come from NEXT_PUBLIC_* env vars
 * (see .env.example). The panel is restricted to users whose Firestore doc
 * users/{uid} has role "admin" or "editor"; viewers can read only.
 */
import { initializeApp, getApps, type FirebaseApp } from "firebase/app";
import { getAuth, type Auth } from "firebase/auth";
import { getFirestore, type Firestore } from "firebase/firestore";

const required = [
  "NEXT_PUBLIC_FIREBASE_API_KEY",
  "NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN",
  "NEXT_PUBLIC_FIREBASE_PROJECT_ID",
] as const;

function assertEnv(): Record<(typeof required)[number], string> {
  const missing = required.filter((k) => !process.env[k]);
  if (missing.length > 0) {
    throw new Error(
      `Missing Firebase env vars: ${missing.join(", ")}. Copy .env.example to .env.local`,
    );
  }
  return Object.fromEntries(
    required.map((k) => [k, process.env[k] as string]),
  ) as Record<(typeof required)[number], string>;
}

let app: FirebaseApp | undefined;
let auth: Auth | undefined;
let db: Firestore | undefined;

export function firebaseApp(): FirebaseApp {
  if (!app) {
    const env = assertEnv();
    const config = {
      apiKey: env.NEXT_PUBLIC_FIREBASE_API_KEY,
      authDomain: env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
      projectId: env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
      storageBucket: process.env.NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET,
      messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID,
      appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID,
    };
    app = getApps().length > 0 ? getApps()[0] : initializeApp(config);
  }
  return app;
}

export function firebaseDb(): Firestore {
  if (!db) db = getFirestore(firebaseApp());
  return db;
}

export function firebaseAuth(): Auth {
  if (!auth) auth = getAuth(firebaseApp());
  return auth;
}
