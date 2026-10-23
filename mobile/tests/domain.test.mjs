import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { initialTrip, paymentKey, readSnapshot, snapshot, storageKey, tripReducer, validLogin } from '../shared/domain.ts';

const run = (trip, ...actions) => actions.reduce((state, type) => tripReducer(state, { type }), trip);
const draft = () => ({ ...initialTrip, destination: 'market' });

test('rider completes the full trip and payment without skipping states', () => {
  const paid = run(draft(), 'quote', 'book', 'accept', 'depart', 'pickup', 'dropoff', 'pay', 'settle');
  assert.equal(paid.status, 'paid');
  assert.equal(paid.paymentAttempts, 1);
  assert.equal(run(draft(), 'pickup', 'dropoff', 'settle').status, 'idle');
});

test('quote validates destination and active trips cannot change location', () => {
  assert.equal(run(initialTrip, 'quote'), initialTrip);
  assert.equal(run({ ...draft(), destination: 'university' }, 'quote').status, 'idle');
  assert.equal(tripReducer(initialTrip, { type: 'place', field: 'destination', id: 'invalid' }), initialTrip);
  const active = run(draft(), 'quote', 'book');
  assert.equal(tripReducer(active, { type: 'place', field: 'pickup', id: 'park' }), active);
});

test('driver receives and completes own offer; offline drivers cannot receive', () => {
  assert.equal(run({ ...initialTrip, online: false }, 'offer').status, 'idle');
  const accepted = run(initialTrip, 'offer', 'accept');
  assert.equal(tripReducer(accepted, { type: 'online', value: false }), accepted);
  assert.equal(run(accepted, 'depart', 'pickup', 'dropoff', 'pay', 'settle').status, 'paid');
});

test('cancellation only works before pickup and rejection is bounded', () => {
  assert.equal(run({ ...draft(), status: 'enroute' }, 'cancel').status, 'cancelled');
  assert.equal(run({ ...draft(), status: 'pickedup' }, 'cancel').status, 'pickedup');
  const failed = run(initialTrip, 'offer', 'reject', 'reject', 'reject', 'reject');
  assert.equal(failed.status, 'failed');
  assert.equal(failed.attempts, 3);
});

test('payment retries preserve identity, have a limit and cannot double-settle', () => {
  const pending = run({ ...draft(), status: 'droppedoff' }, 'pay');
  const failed = run(pending, 'payment-error');
  assert.equal(run(failed, 'settle'), failed);
  const retry = run(failed, 'retry');
  assert.equal(paymentKey(retry), paymentKey(pending));
  assert.equal(retry.paymentAttempts, 2);
  const exhausted = run(retry, 'payment-error', 'retry', 'payment-error', 'retry', 'settle');
  assert.equal(exhausted.paymentAttempts, 3);
  assert.equal(exhausted.status, 'payment_pending');
  assert.equal(exhausted.paymentError, true);
  const paid = run(retry, 'settle');
  assert.equal(run(paid, 'settle'), paid);
  assert.notEqual(paymentKey(run(paid, 'reset')), paymentKey(paid));
});

test('snapshots validate structure, version, audience and business invariants', () => {
  const paid = run({ ...draft(), status: 'droppedoff' }, 'pay', 'settle');
  assert.deepEqual(readSnapshot(snapshot(paid, 'rider'), 'rider'), paid);
  assert.equal(readSnapshot(snapshot(paid, 'rider'), 'driver'), null);
  for (const raw of ['null', '{}', 'broken', JSON.stringify({ version: 2, role: 'rider', trip: paid })]) {
    assert.equal(readSnapshot(raw, 'rider'), null);
  }
  for (const invalid of [
    { status: 'invalid' }, { pickup: 'unknown' }, { destination: '' }, { destination: 'university' },
    { paymentAttempts: 0 }, { paymentAttempts: 4 }, { paymentError: true }, { online: 'true' },
    { attempts: -1 }, { tripNumber: Number.MAX_SAFE_INTEGER },
  ]) assert.equal(readSnapshot(snapshot({ ...paid, ...invalid }, 'rider'), 'rider'), null);
});

test('each app has its own login and storage; resetting driver cannot change rider', () => {
  assert.notEqual(storageKey('rider'), storageKey('driver'));
  assert.equal(validLogin(' RIDER@SWIFTRIDE.DEMO ', 'demo1234', 'rider'), true);
  assert.equal(validLogin('driver@swiftride.demo', 'demo1234', 'rider'), false);
  assert.equal(validLogin('driver@swiftride.demo', 'wrong', 'driver'), false);
  const rider = run(draft(), 'quote', 'book');
  run(initialTrip, 'offer', 'accept', 'reset');
  assert.equal(rider.status, 'searching');
});

test('separate mobile identities and entrypoints; no web view or sibling screen import', () => {
  const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const rider = JSON.parse(read('../apps/rider/app.json')).expo;
  const driver = JSON.parse(read('../apps/driver/app.json')).expo;
  assert.notEqual(rider.android.package, driver.android.package);
  assert.notEqual(rider.ios.bundleIdentifier, driver.ios.bundleIdentifier);
  assert.notEqual(rider.scheme, driver.scheme);
  for (const [app, other] of [['rider', 'Driver'], ['driver', 'Rider']]) {
    assert.match(read(`../apps/${app}/index.ts`), /registerRootComponent/);
    assert.doesNotMatch(read(`../apps/${app}/src/App.tsx`), new RegExp(`import ${other} from`));
    assert.equal(JSON.parse(read(`../apps/${app}/package.json`)).dependencies['react-native-webview'], undefined);
    const config = createRequire(import.meta.url)(`../apps/${app}/metro.config.js`);
    assert.ok(config.watchFolders.includes(fileURLToPath(new URL('../shared', import.meta.url))));
  }
  assert.doesNotMatch(read('../shared/MobileApp.tsx'), /from.*apps\/(rider|driver)|WebView|localStorage/);
});
