import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";
import { verifyDonation } from "@/lib/adminData";

/**
 * POST /api/donations/verify — marca una donación como verificada y adjunta
 * opcionalmente la URL del comprobante (receipt/proof) subido a Storage.
 * Body: { donationId: string, proofUrl?: string }
 */
export async function POST(req: NextRequest) {
  const auth = await requireAdmin(req, { write: true });
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }
  const b = body as { donationId?: unknown; proofUrl?: unknown };
  if (typeof b.donationId !== "string" || b.donationId.length === 0) {
    return NextResponse.json(
      { error: "missing_donation_id" },
      { status: 400 },
    );
  }
  if (b.proofUrl !== undefined && typeof b.proofUrl !== "string") {
    return NextResponse.json({ error: "invalid_proof_url" }, { status: 400 });
  }
  try {
    const updated = await verifyDonation(
      b.donationId,
      b.proofUrl as string | undefined,
    );
    return NextResponse.json(updated);
  } catch {
    return NextResponse.json(
      { error: "donation_not_found" },
      { status: 404 },
    );
  }
}
