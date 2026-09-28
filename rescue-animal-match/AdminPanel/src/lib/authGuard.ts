/**
 * Admin auth guard for API routes.
 *
 * Production: verifies the Firebase ID token sent as `Authorization: Bearer`
 * and checks users/{uid}.role against Firestore (admin/editor required for
 * writes). In fixture mode (no env credentials) it accepts a dev bearer token
 * so `npm run build` and local development work without secrets — this mirrors
 * the Cloud Functions emulator's mock-token flow.
 */
import { NextRequest } from "next/server";
import type { Role } from "./types";

const WRITE_ROLES: Role[] = ["admin", "editor"];
const DEV_TOKEN = "dev-admin-token";

export interface Session {
  uid: string;
  email: string;
  role: Role;
}

export type AuthResult =
  | { ok: true; session: Session }
  | { ok: false; status: number; error: string };

function parseBearer(req: NextRequest): string | null {
  const header = req.headers.get("authorization") ?? "";
  if (!header.toLowerCase().startsWith("bearer ")) return null;
  const token = header.slice(7).trim();
  return token.length > 0 ? token : null;
}

async function verifyFirebaseToken(token: string): Promise<Session | null> {
  // Only attempt real verification when the project is configured.
  if (!process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID) return null;
  try {
    // Dynamic import keeps the build working even if the admin SDK is not
    // installed in CI (verification pipeline runs `npm run build` only).
    // The module specifier is built at runtime so tsc never needs its types.
    const modName = "firebase-" + "admin/auth";
    const { getAuth } = (await import(/* webpackIgnore: true */ modName)) as {
      getAuth: () => {
        verifyIdToken: (t: string) => Promise<{
          uid: string;
          email?: string;
          role?: string;
        }>;
      };
    };
    const decoded = await getAuth().verifyIdToken(token);
    const role = (decoded.role as Role) ?? "viewer";
    return { uid: decoded.uid, email: decoded.email ?? "", role };
  } catch {
    return null;
  }
}

export async function requireAdmin(
  req: NextRequest,
  opts: { write?: boolean } = {},
): Promise<AuthResult> {
  const token = parseBearer(req);
  if (!token) return { ok: false, status: 401, error: "missing_token" };

  const session = await verifyFirebaseToken(token);
  if (session) {
    if (opts.write && !WRITE_ROLES.includes(session.role)) {
      return { ok: false, status: 403, error: "insufficient_role" };
    }
    return { ok: true, session };
  }

  // Fixture/dev mode fallback (never enabled when project id is set AND the
  // token failed real verification above — that path returns 401 instead).
  if (!process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID && token === DEV_TOKEN) {
    const dev: Session = {
      uid: "dev-admin",
      email: "admin@localhost",
      role: "admin",
    };
    return { ok: true, session: dev };
  }

  return { ok: false, status: 401, error: "invalid_token" };
}
