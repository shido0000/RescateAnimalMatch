/**
 * onDonationReached (Firestore trigger): when config/weekly_goal reaches its
 * target, create a donations/{id} document and notify users via FCM.
 * Idempotent: guarded by the donationFired flag written back to the goal doc.
 */
import { onDocumentUpdated } from 'firebase-functions/v2/firestore';
import { logger } from 'firebase-functions/v2';
import * as admin from 'firebase-admin';
import type { DonationDoc, WeeklyGoal } from './types';

export const onDonationReached = onDocumentUpdated('config/weekly_goal', async (event) => {
  const before = event.data?.before?.data() as WeeklyGoal | undefined;
  const after = event.data?.after?.data() as WeeklyGoal | undefined;
  if (!after) return;

  const reachedNow = after.currentHuellas >= after.targetHuellas;
  const alreadyFired = before?.donationFired === true || after.donationFired === true;

  if (!reachedNow || alreadyFired) {
    return; // not this trigger's job
  }

  const db = admin.firestore();
  const goalRef = db.doc('config/weekly_goal');

  // Mark fired FIRST inside a transaction so parallel triggers don't
  // double-create the donation document.
  const shouldCreate = await db.runTransaction(async (tx) => {
    const snap = await tx.get(goalRef);
    const g = snap.data() as WeeklyGoal;
    if (g.donationFired) return false;
    tx.update(goalRef, { donationFired: true });
    return true;
  });
  if (!shouldCreate) return;

  const donation: DonationDoc = {
    shelterId: after.shelterId,
    shelterName: after.shelterName,
    amount: after.targetHuellas,
    date: new Date().toISOString(),
    type: 'weekly_goal',
    verified: false, // an admin verifies the real transfer in the Admin Panel
  };
  const docRef = await db.collection('donations').add(donation);
  logger.info('donation_created', { donationId: docRef.id, shelterId: donation.shelterId });

  // Notify all users with a push token (batched, max 500 tokens per FCM message).
  await sendDonationNotification(after, donation);
});

async function sendDonationNotification(goal: WeeklyGoal, donation: DonationDoc): Promise<void> {
  const tokensSnap = await admin.firestore().collectionGroup('users').get();
  const title = '¡Meta comunitaria alcanzada! 🐾';
  const body = `Gracias a la comunidad donaremos a ${goal.shelterName}. ` +
    `Recompensa: ${goal.rewardDescription}`;

  const batches: string[][] = [];
  let current: string[] = [];
  tokensSnap.forEach((doc) => {
    const token = doc.data().pushToken;
    if (typeof token !== 'string' || token.length === 0) return;
    current.push(token);
    if (current.length === 500) {
      batches.push(current);
      current = [];
    }
  });
  if (current.length > 0) batches.push(current);

  for (const batch of batches) {
    try {
      await admin.messaging().sendEachForMulticast({
        tokens: batch,
        notification: { title, body },
        data: { donationId: donation.shelterId, amount: String(donation.amount) },
      });
    } catch (err) {
      logger.error('fcm_send_failed', { error: err });
    }
  }
  logger.info('donation_notifications_sent', { batches: batches.length });
}
