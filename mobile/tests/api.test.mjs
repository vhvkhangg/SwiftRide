import test from 'node:test';
import assert from 'node:assert/strict';
import { coordinates, estimateRoute, isTerminal, pointsOf, priceText, readClaims, riderCanCancel, statusName } from '../shared/api/trip.ts';

test('Trip status numbers map in one place; unknown values never look final', () => {
  assert.equal(statusName(0), 'Requested');
  assert.equal(statusName(1), 'PriceConfirmed');
  assert.equal(statusName(7), 'Paid');
  assert.equal(statusName(9), 'Failed');
  assert.equal(statusName('PaymentPending'), 'PaymentPending');
  for (const value of [10, -1, 'Done', null, undefined]) {
    assert.equal(statusName(value), 'Unknown');
    assert.equal(isTerminal(statusName(value)), false);
  }
  assert.equal(riderCanCancel('EnRoute'), true);
  assert.equal(riderCanCancel('PickedUp'), false);
});

test('Route estimate is deterministic and positive for every sample pair', () => {
  for (const from of Object.keys(coordinates)) for (const to of Object.keys(coordinates)) {
    if (from === to) continue;
    const route = estimateRoute(coordinates[from], coordinates[to]);
    assert.ok(route.distance > 0 && route.distance < 10, `${from}->${to} ${route.distance}`);
    assert.ok(Number.isInteger(route.estimatedTime) && route.estimatedTime >= 1);
  }
  assert.deepEqual(estimateRoute(coordinates.university, coordinates.market), estimateRoute(coordinates.university, coordinates.market));
});

test('Prices are shown as returned, never rescaled', () => {
  assert.equal(priceText(44.88), '44,88 ₫');
  assert.equal(priceText(null), '—');
});

test('Claims reader handles Keycloak role arrays and rejects malformed tokens', () => {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const token = `${encode({ alg: 'RS256' })}.${encode({ sub: 'abc', exp: 1, email: 'rider.demo@example.com', name: 'Rider Đẹp', role: ['Rider', 'default-roles'] })}.sig`;
  assert.deepEqual(readClaims(token), { sub: 'abc', email: 'rider.demo@example.com', name: 'Rider Đẹp', roles: ['Rider', 'default-roles'], exp: 1 });
  assert.equal(readClaims('not-a-token'), null);
  assert.equal(readClaims(`${encode({})}.${encode({ exp: 1 })}.x`), null);
});

test('trip points come from the server; missing or invalid coordinates give null, never a guessed position', () => {
  assert.deepEqual(pointsOf({ pickupLatitude: 10.76, pickupLongitude: 106.68, dropoffLatitude: 10.77, dropoffLongitude: 106.7 }),
    { pickup: { latitude: 10.76, longitude: 106.68 }, destination: { latitude: 10.77, longitude: 106.7 } });
  assert.deepEqual(pointsOf({ pickupLatitude: 10.76, pickupLongitude: 106.68 }), { pickup: { latitude: 10.76, longitude: 106.68 }, destination: null });
  assert.deepEqual(pointsOf({ pickupLatitude: Number.NaN, pickupLongitude: 1, dropoffLatitude: null, dropoffLongitude: 106 }), { pickup: null, destination: null });
});
