/**
 * Rescate Animal Match — Cloud Functions entry point.
 *
 * Deploy:  npm run deploy   (builds TS then firebase deploy --only functions)
 * Emulate: firebase emulators:start --only functions,firestore,auth
 *
 * Secrets policy: no credentials are hardcoded. Google Play verification uses
 * the default service account of the project (ADC). If a dedicated Play
 * service account is required, set it via:
 *   firebase functions:secrets:set PLAY_SERVICE_ACCOUNT_JSON
 */
import { initializeApp } from 'firebase-admin/app';
import { contributeHuellas } from './contribute';
import { onDonationReached } from './donations';
import { validatePurchase } from './purchases';
import { getShelters } from './shelters';

initializeApp();

export { contributeHuellas, onDonationReached, validatePurchase, getShelters };
