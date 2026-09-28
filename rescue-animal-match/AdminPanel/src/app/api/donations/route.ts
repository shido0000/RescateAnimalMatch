import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";
import { listDonations } from "@/lib/adminData";

/** GET /api/donations — lista de donaciones (snapshot para la UI). */
export async function GET(req: NextRequest) {
  const auth = await requireAdmin(req);
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  return NextResponse.json(await listDonations());
}
