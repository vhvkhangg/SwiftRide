import test from 'node:test';
import assert from 'node:assert/strict';
import { demoReducer, initialState, readSnapshot, paymentKey } from '../shared/lib/demo.ts';

const draft = () => ({ ...initialState, role: 'rider', destination: 'market' });
const run = (state, ...types) => types.reduce((current, type) => demoReducer(current, { type }), state);

test('complete trip follows explicit transitions', () => {
  const result = run(draft(), 'quote', 'book', 'accept', 'depart', 'pickup', 'dropoff', 'pay', 'settle');
  assert.equal(result.status, 'paid');
});
test('cannot quote missing or identical destinations', () => {
  assert.equal(run(initialState, 'quote').status, 'idle');
  assert.equal(run({ ...draft(), destination: 'university' }, 'quote').status, 'idle');
});
test('cannot jump from booking to completed trip', () => {
  assert.equal(run(draft(), 'settle', 'dropoff', 'pickup').status, 'idle');
});
test('cancellation only allowed before pickup', () => {
  assert.equal(run({ ...draft(), status: 'enroute' }, 'cancel').status, 'cancelled');
  assert.equal(run({ ...draft(), status: 'pickedup' }, 'cancel').status, 'pickedup');
});
test('three rejected offers end matching', () => {
  const result = run(draft(), 'quote', 'book', 'reject', 'reject', 'reject');
  assert.equal(result.status, 'failed');
  assert.equal(result.attempts, 3);
});
test('retry preserves payment identity and never settles an unresolved error', () => {
  const pending = { ...draft(), status: 'payment_pending' };
  const failed = run(pending, 'payment-error', 'settle');
  assert.equal(failed.status, 'payment_pending');
  assert.equal(paymentKey(run(failed, 'retry')), paymentKey(pending));
  assert.equal(run(failed, 'retry', 'settle').status, 'paid');
});
test('settlement cannot be duplicated', () => {
  const paid = { ...draft(), status: 'paid' };
  assert.equal(run(paid, 'settle'), paid);
});
test('offline drivers cannot generate offers', () => {
  assert.equal(run({ ...draft(), online: false }, 'offer').status, 'idle');
});
test('snapshot rejects malformed and inconsistent local storage', () => {
  for (const raw of ['broken', 'null', '{}', JSON.stringify({ ...draft(), status: 'unknown' }), JSON.stringify({ ...draft(), status: 'paid', destination: '' })]) {
    assert.deepEqual(readSnapshot(raw), initialState);
  }
  assert.deepEqual(readSnapshot(JSON.stringify(draft())), draft());
});
test('logout preserves local trip; new trip receives a new payment key', () => {
  const before = { ...draft(), status: 'enroute' };
  const after = demoReducer(before, { type: 'role', role: null });
  assert.equal(after.status, 'enroute');
  assert.notEqual(paymentKey(run(after, 'reset')), paymentKey(before));
});
