import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";
import { listShelters, upsertShelter, deleteShelter } from "@/lib/adminData";
import type { Shelter } from "@/lib/types";

/**
 * /api/shelters — CRUD de refugios (protegido por rol admin/editor).
 *  GET    → lista todos los refugios
 *  POST   → crea o actualiza (body: Shelter completo con id)
 *  DELETE → ?id=... elimina un refugio
 */

export const dynamic = "force-dynamic";

function isValidShelter(input: unknown): input is Shelter {
  if (typeof input !== "object" || input === null) return false;
  const s = input as Partial<Shelter>;
  return (
    typeof s.id === "string" &&
    s.id.length > 0 &&
    typeof s.name === "string" &&
    s.name.length > 0 &&
    typeof s.location === "string" &&
    typeof s.contact === "string" &&
    Array.isArray(s.needs) &&
    Array.isArray(s.animals) &&
    typeof s.verified === "boolean"
  );
}

export async function GET(req: NextRequest) {
  const auth = await requireAdmin(req);
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  return NextResponse.json(await listShelters());
}

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
  if (!isValidShelter(body)) {
    return NextResponse.json(
      { error: "invalid_shelter_schema" },
      { status: 400 },
    );
  }
  const saved = await upsertShelter(body);
  return NextResponse.json(saved);
}

export async function DELETE(req: NextRequest) {
  const auth = await requireAdmin(req, { write: true });
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  const id = req.nextUrl.searchParams.get("id");
  if (!id) {
    return NextResponse.json({ error: "missing_id" }, { status: 400 });
  }
  await deleteShelter(id);
  return NextResponse.json({ deleted: id });
}
