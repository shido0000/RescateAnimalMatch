/**
 * validatePurchase (callable): verifies a Google Play purchase token with the
 * Android Publisher API before granting anything. The Unity client NEVER
 * delivers products without a { valid: true } response from this function.
 */
import { onCall, HttpsError } from 'firebase-functions/v2/https';
import { logger } from 'firebase-functions/v2';
import * as admin from 'firebase-admin';
import { google } from 'googleapis';
import type { ValidatePurchaseRequest, ValidatePurchaseResponse } from './types';

/** Products the game knows about — reject anything else (anti-tampering). */
export const ALLOWED_PRODUCTS = new Set([
  'com.rescueanimalmatch.hammer',
  'com.rescueanimalmatch.shuffle',
  'com.rescueanimalmatch.moves5',
  'com.rescueanimalmatch.season_pass',
  'com.rescueanimalmatch.donation_pack',
]);

const PACKAGE_NAME = process.env.PLAY_PACKAGE_NAME ?? 'com.rescueanimalmatch.game';

export interface ReceiptVerifier {
  /** Returns purchaseState (0= Purchased, 1=Canceled, 2=Pending) or null if invalid. */
  verify(productId: string, purchaseToken: string): Promise<number | null>;
}

/** Real verifier using the Android Publisher API with service-account ADC. */
class GooglePlayReceiptVerifier implements ReceiptVerifier {
  async verify(productId: string, purchaseToken: string): Promise<number | null> {
    const auth = await google.auth.getClient({
      scopes: ['https://www.googleapis.com/auth/androidpublisher'],
    });
    const publisher = google.androidpublisher({ version: 'v3', auth });
    try {
      const res = await publisher.purchases.products.get({
        packageName: PACKAGE_NAME,
        productId,
        token: purchaseToken,
      });
      return typeof res.data.purchaseState === 'number' ? res.data.purchaseState : null;
    } catch (err) {
      logger.warn('play_receipt_lookup_failed', { error: err });
      return null; // 400/404 => invalid token
    }
  }
}

/** Injectable for tests/emulator: USE_MOCK_VERIFIER=1 accepts tokens "valid:*". */
class MockReceiptVerifier implements ReceiptVerifier {
  async verify(_productId: string, purchaseToken: string): Promise<number | null> {
    return purchaseToken.startsWith('valid:') ? 0 : null;
  }
}

export function getVerifier(): ReceiptVerifier {
  return process.env.USE_MOCK_VERIFIER === '1'
    ? new MockReceiptVerifier()
    : new GooglePlayReceiptVerifier();
}

export const validatePurchase = onCall(async (req) => {
  const auth = req.auth;
  if (!auth) throw new HttpsError('unauthenticated', 'login_required');

  const body = (req.data ?? {}) as Partial<ValidatePurchaseRequest>;
  const { productId, purchaseToken } = body;
  if (typeof productId !== 'string' || typeof purchaseToken !== 'string') {
    throw new HttpsError('invalid-argument', 'productId_and_purchaseToken_required');
  }
  if (!ALLOWED_PRODUCTS.has(productId)) {
    throw new HttpsError('invalid-argument', 'unknown_product');
  }

  const state = await getVerifier().verify(productId, purchaseToken);
  if (state !== 0) {
    logger.warn('purchase_rejected', { uid: auth.uid, productId, state });
    const fail: ValidatePurchaseResponse = { valid: false, productId, error: 'receipt_invalid' };
    return fail;
  }

  // Record in users/{uid}.purchases so IAPManager.RestorePurchases works
  // across reinstalls, and bump purchases_count for analytics properties.
  const userRef = admin.firestore().doc(`users/${auth.uid}`);
  await admin.firestore().runTransaction(async (tx) => {
    const snap = await tx.get(userRef);
    const data = snap.exists ? (snap.data() ?? {}) : {};
    const purchases: string[] = Array.isArray(data.purchases) ? data.purchases : [];
    if (!purchases.includes(productId)) purchases.push(productId);
    tx.set(
      userRef,
      {
        ...data,
        purchases,
        purchasesCount: (typeof data.purchasesCount === 'number' ? data.purchasesCount : 0) + 1,
      },
      { merge: true },
    );
  });

  logger.info('purchase_validated', { uid: auth.uid, productId });
  const ok: ValidatePurchaseResponse = { valid: true, productId };
  return ok;
});
