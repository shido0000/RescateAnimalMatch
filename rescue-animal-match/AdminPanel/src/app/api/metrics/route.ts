import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";
import { computeMetrics } from "@/lib/adminData";

/**
 * GET /api/metrics — métricas agregadas de impacto para el dashboard:
 * jugadores totales, Huellas acumuladas, donaciones activas/verificadas,
 * USD donados, ingresos y refugios/animales disponibles.
 */
export async function GET(req: NextRequest) {
  const auth = await requireAdmin(req);
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  return NextResponse.json(await computeMetrics());
}
