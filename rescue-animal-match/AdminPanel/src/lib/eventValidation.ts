import type { WeeklyGoal } from "./types";

/**
 * Validación del payload de creación de evento semanal (POST /api/events).
 * Devuelve la clave de error o null si el payload es válido.
 */
export function validateEventPayload(body: unknown): string | null {
  const b = body as Partial<WeeklyGoal>;
  if (
    typeof b.targetHuellas !== "number" ||
    !Number.isInteger(b.targetHuellas) ||
    b.targetHuellas < 1000
  ) {
    return "target_huellas_invalid";
  }
  if (typeof b.shelterId !== "string" || b.shelterId.length === 0) {
    return "shelter_id_required";
  }
  if (typeof b.shelterName !== "string" || b.shelterName.length === 0) {
    return "shelter_name_required";
  }
  if (
    typeof b.rewardDescription !== "string" ||
    b.rewardDescription.length < 10
  ) {
    return "reward_description_too_short";
  }
  return null;
}
