import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";

/**
 * GET /api/auth/session — validates the bearer token and returns the admin
 * session. Used by /login to enforce the admin-role restriction.
 */
export async function GET(req: NextRequest) {
  const result = await requireAdmin(req);
  if (!result.ok) {
    return NextResponse.json({ error: result.error }, { status: result.status });
  }
  return NextResponse.json({
    uid: result.session.uid,
    email: result.session.email,
    role: result.session.role,
  });
}
