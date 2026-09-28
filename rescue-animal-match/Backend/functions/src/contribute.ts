/**
 * contributeHuellas (callable): atomically increments config/weekly_goal.
 * Mirrors IFirestoreBackend.IncrementWeeklyGoal on the Unity side.
 */
import { onCall, HttpsError } from 'firebase-functions/v2/https';
import { logger } from 'firebase-functions/v2';
import * as admin from 'firebase-admin';
import type { ContributeRequest, ContributeResponse, WeeklyGoal } from './types';

export const GOAL_DOC = 'config/weekly_goal';
export const MIN_CONTRIB = 1;
export const MAX_CONTRIB = 5000; // anti-abuse cap per call

/**
 * Runs inside a Firestore transaction so concurrent players never lose
 * increments (read-modify-write atomicity).
 */
async function incrementGoalAtomically(
  amount: number,
): Promise<{ goal: WeeklyGoal; reached: boolean }> {
  const db = admin.firestore();
  const ref = db.doc(GOAL_DOC);

  return db.runTransaction(async (tx) => {
    const snap = await tx.get(ref);
    if (!snap.exists) {
      throw new HttpsError('failed-precondition', 'weekly_goal_not_configured');
    }
    const goal = snap.data() as WeeklyGoal;
    const next = Math.min(goal.currentHuellas + amount, goal.targetHuellas);
    const updated: WeeklyGoal = { ...goal, currentHuellas: next };
    tx.set(ref, updated as unknown as Record<string, unknown>);
    return { goal: updated, reached: next >= goal.targetHuellas };
  });
}

export const contributeHuellas = onCall(async (req) => {
  // 1. Auth is mandatory — anonymous Firebase auth is fine, unauthenticated is not.
  const auth = req.auth;
  if (!auth) {
    throw new HttpsError('unauthenticated', 'login_required');
  }

  // 2. Validate payload server-side (never trust the client).
  const body = (req.data ?? {}) as Partial<ContributeRequest>;
  const amount = body.amount;
  if (typeof amount !== 'number' || !Number.isInteger(amount)) {
    throw new HttpsError('invalid-argument', 'amount_must_be_integer');
  }
  if (amount < MIN_CONTRIB || amount > MAX_CONTRIB) {
    throw new HttpsError(
      'invalid-argument',
      `amount_out_of_range_${MIN_CONTRIB}_${MAX_CONTRIB}`,
    );
  }

  // 3. Atomic increment + user ledger write.
  const { goal, reached } = await incrementGoalAtomically(amount);

  const userRef = admin.firestore().doc(`users/${auth.uid}`);
  await admin.firestore().runTransaction(async (tx) => {
    const snap = await tx.get(userRef);
    const data = snap.exists ? (snap.data() ?? {}) : {};
    const contributed = typeof data.huellasContributed === 'number' ? data.huellasContributed : 0;
    tx.set(
      userRef,
      { ...data, huellasContributed: contributed + amount },
      { merge: true },
    );
  });

  logger.info('huellas_contributed', { uid: auth.uid, amount, reached });

  const response: ContributeResponse = {
    currentHuellas: goal.currentHuellas,
    targetHuellas: goal.targetHuellas,
    goalReached: reached,
  };
  return response;
});
