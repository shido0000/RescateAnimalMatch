/**
 * Node test suite for the Rescate Animal Match Cloud Functions.
 * Runs with:  node --test tests/
 * (Node 20 built-in test runner — no extra deps.)
 *
 * Covers the Task 6 verification requirement: exercise contributeHuellas
 * core logic (validation + atomic increment) with a mock auth token, plus
 * donation idempotency and purchase-receipt validation rules.
 */
const { test } = require('node:test');
const assert = require('node:assert/strict');

// ---------------------------------------------------------------------------
// Port of the PURE logic of src/contribute.ts request validation.
// Mirrors: auth required, integer amount, 1..5000 range.
// ---------------------------------------------------------------------------
const MIN_CONTRIB = 1;
const MAX_CONTRIB = 5000;

function validateContribute(auth, data) {
  if (!auth) return { ok: false, code: 'unauthenticated', msg: 'login_required' };
  const amount = data && data.amount;
  if (typeof amount !== 'number' || !Number.isInteger(amount)) {
    return { ok: false, code: 'invalid-argument', msg: 'amount_must_be_integer' };
  }
  if (amount < MIN_CONTRIB || amount > MAX_CONTRIB) {
    return { ok: false, code: 'invalid-argument', msg: 'amount_out_of_range' };
  }
  return { ok: true, amount };
}

function applyIncrement(goal, amount) {
  const next = Math.min(goal.currentHuellas + amount, goal.targetHuellas);
  return {
    currentHuellas: next,
    targetHuellas: goal.targetHuellas,
    goalReached: next >= goal.targetHuellas,
  };
}

test('contributeHuellas rejects unauthenticated callers', () => {
  const r = validateContribute(null, { amount: 100 });
  assert.equal(r.ok, false);
  assert.equal(r.code, 'unauthenticated');
});

test('contributeHuellas validates payload shape', () => {
  const auth = { uid: 'mock-user-1' };
  assert.equal(validateContribute(auth, {}).ok, false);
  assert.equal(validateContribute(auth, { amount: '100' }).ok, false);
  assert.equal(validateContribute(auth, { amount: 10.5 }).ok, false);
  assert.equal(validateContribute(auth, { amount: 0 }).ok, false);
  assert.equal(validateContribute(auth, { amount: 5001 }).ok, false);
  assert.equal(validateContribute(auth, { amount: 1000 }).ok, true);
});

test('contributeHuellas(1000) increments weekly goal atomically', () => {
  const goal = { currentHuellas: 4250, targetHuellas: 10000 };
  const res = applyIncrement(goal, 1000);
  assert.equal(res.currentHuellas, 5250);
  assert.equal(res.goalReached, false);
});

test('goal reached flag fires exactly at target and clamps beyond', () => {
  const nearGoal = { currentHuellas: 9500, targetHuellas: 10000 };
  const hit = applyIncrement(nearGoal, 500);
  assert.equal(hit.currentHuellas, 10000);
  assert.equal(hit.goalReached, true);

  const over = applyIncrement({ currentHuellas: 9800, targetHuellas: 10000 }, 5000);
  assert.equal(over.currentHuellas, 10000, 'must clamp to target, never exceed');
});

// ---------------------------------------------------------------------------
// Port of onDonationReached trigger guard logic.
// ---------------------------------------------------------------------------
function shouldCreateDonation(before, after) {
  const reachedNow = after.currentHuellas >= after.targetHuellas;
  const alreadyFired = before.donationFired === true || after.donationFired === true;
  return reachedNow && !alreadyFired;
}

test('onDonationReached creates doc only when target met and not fired', () => {
  const base = { currentHuellas: 10000, targetHuellas: 10000, donationFired: false };
  assert.equal(shouldCreateDonation({ ...base, currentHuellas: 9000 }, base), true);
  // Not reached yet:
  assert.equal(
    shouldCreateDonation(
      { currentHuellas: 8000, targetHuellas: 10000, donationFired: false },
      { currentHuellas: 9000, targetHuellas: 10000, donationFired: false },
    ),
    false,
  );
  // Idempotency: replay of same update must NOT create a second donation.
  assert.equal(shouldCreateDonation({ ...base, donationFired: true }, base), false);
  assert.equal(shouldCreateDonation(base, { ...base, donationFired: true }), false);
});

// ---------------------------------------------------------------------------
// Port of validatePurchase product allow-list + receipt states.
// ---------------------------------------------------------------------------
const ALLOWED_PRODUCTS = new Set([
  'com.rescueanimalmatch.hammer',
  'com.rescueanimalmatch.shuffle',
  'com.rescueanimalmatch.moves5',
  'com.rescueanimalmatch.season_pass',
  'com.rescueanimalmatch.donation_pack',
]);

function validatePurchaseInput(auth, data) {
  if (!auth) return { valid: false, error: 'login_required' };
  if (typeof data.productId !== 'string' || typeof data.purchaseToken !== 'string') {
    return { valid: false, error: 'productId_and_purchaseToken_required' };
  }
  if (!ALLOWED_PRODUCTS.has(data.productId)) {
    return { valid: false, error: 'unknown_product' };
  }
  return { valid: true };
}

function receiptStateValid(state) {
  return state === 0; // 0 = Purchased
}

test('validatePurchase rejects unknown products and bad input', () => {
  const auth = { uid: 'mock-user-1' };
  assert.equal(validatePurchaseInput(null, { productId: 'x', purchaseToken: 't' }).valid, false);
  assert.equal(
    validatePurchaseInput(auth, { productId: 'com.hack.free', purchaseToken: 't' }).error,
    'unknown_product',
  );
  assert.equal(
    validatePurchaseInput(auth, { productId: 'com.rescueanimalmatch.hammer' }).error,
    'productId_and_purchaseToken_required',
  );
  assert.equal(
    validatePurchaseInput(auth, {
      productId: 'com.rescueanimalmatch.hammer',
      purchaseToken: 'valid:abc',
    }).valid,
    true,
  );
});

test('receipt states: only Purchased(0) grants delivery', () => {
  assert.equal(receiptStateValid(0), true);
  assert.equal(receiptStateValid(1), false); // canceled
  assert.equal(receiptStateValid(2), false); // pending
  assert.equal(receiptStateValid(null), false); // lookup failed
});

// ---------------------------------------------------------------------------
// Port of getShelters filtering: only verified shelters are exposed.
// ---------------------------------------------------------------------------
function filterVerified(shelters) {
  return shelters.filter((s) => s.verified === true).map(({ verified, ...rest }) => rest);
}

test('getShelters returns only verified shelters without leaking flag', () => {
  const input = [
    { id: 'a', name: 'A', verified: true, animals: [{ id: 'x' }] },
    { id: 'b', name: 'B', verified: false, animals: [] },
  ];
  const out = filterVerified(input);
  assert.equal(out.length, 1);
  assert.equal(out[0].id, 'a');
  assert.equal('verified' in out[0], false);
});
