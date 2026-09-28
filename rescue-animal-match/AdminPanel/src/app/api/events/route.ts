import { NextRequest, NextResponse } from "next/server";
import { requireAdmin } from "@/lib/authGuard";
import { getWeeklyGoal, setWeeklyGoal } from "@/lib/adminData";
import { validateEventPayload } from "@/lib/eventValidation";
import type { WeeklyGoal } from "@/lib/types";

/**
 * /api/events — gestión del evento semanal (config/weekly_goal).
 *  GET  → objetivo actual
 *  POST → crea un nuevo evento semanal. Body:
 *         { targetHuellas, shelterId, shelterName, rewardDescription }
 *         Reinicia currentHuellas a 0 y notifica vía FCM (Cloud Function).
 */

export async function GET(req: NextRequest) {
  const auth = await requireAdmin(req);
  if (!auth.ok) {
    return NextResponse.json({ error: auth.error }, { status: auth.status });
  }
  return NextResponse.json(await getWeeklyGoal());
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
  const problem = validateEventPayload(body);
  if (problem) {
    return NextResponse.json({ error: problem }, { status: 400 });
  }
  const b = body as WeeklyGoal;
  const now = new Date().toISOString();
  const goal: WeeklyGoal = {
    targetHuellas: b.targetHuellas,
    currentHuellas: 0,
    shelterId: b.shelterId,
    shelterName: b.shelterName,
    rewardDescription: b.rewardDescription,
    startedAt: now,
    endsAt: new Date(Date.now() + 7 * 24 * 3600 * 1000).toISOString(),
  };
  const saved = await setWeeklyGoal(goal);
  return NextResponse.json(saved);
}
