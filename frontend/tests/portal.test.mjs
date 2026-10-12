import test from 'node:test';
import assert from 'node:assert/strict';
import { initialState } from '../shared/lib/demo.ts';
import { portalStorageKey, readPortalSnapshot, isDemoAccount } from '../shared/lib/portal.ts';
import { screens as riderScreens } from '../apps/rider/src/screens.ts';
import { screens as driverScreens } from '../apps/driver/src/screens.ts';

test('demo credentials only work in their own portal', () => {
  for (const role of ['rider', 'driver']) {
    const other = role === 'rider' ? 'driver' : 'rider';
    assert.equal(isDemoAccount(role, ` ${role.toUpperCase()}@SWIFTRIDE.DEMO `, 'demo1234'), true);
    assert.equal(isDemoAccount(role, `${other}@swiftride.demo`, 'demo1234'), false);
    assert.equal(isDemoAccount(role, `${role}@swiftride.demo`, 'wrong'), false);
  }
});

test('portal storage is separate and does not reuse the old combined demo key', () => {
  assert.notEqual(portalStorageKey('rider'), portalStorageKey('driver'));
  assert.notEqual(portalStorageKey('rider'), 'swiftride-ui-v1');
  assert.notEqual(portalStorageKey('driver'), 'swiftride-ui-v1');
  const values = new Map();
  const rider = { ...initialState, role: 'rider', status: 'searching', destination: 'market' };
  const driver = { ...initialState, role: 'driver', online: false };
  values.set(portalStorageKey('rider'), JSON.stringify(rider));
  values.set(portalStorageKey('driver'), JSON.stringify(driver));
  assert.deepEqual(readPortalSnapshot(values.get(portalStorageKey('rider')), 'rider'), rider);
  assert.deepEqual(readPortalSnapshot(values.get(portalStorageKey('driver')), 'driver'), driver);
  values.set(portalStorageKey('driver'), JSON.stringify({ ...driver, role: null }));
  assert.deepEqual(readPortalSnapshot(values.get(portalStorageKey('rider')), 'rider'), rider);
});

test('a snapshot from the other portal cannot restore its session', () => {
  const driver = JSON.stringify({ ...initialState, role: 'driver' });
  assert.deepEqual(readPortalSnapshot(driver, 'rider'), initialState);
  assert.deepEqual(readPortalSnapshot('broken', 'driver'), initialState);
});

test('each portal has only its own preview screens', () => {
  assert.deepEqual(riderScreens.map(screen => screen.code), ['R01', 'R02', 'R03', 'R04', 'R05', 'R06']);
  assert.deepEqual(driverScreens.map(screen => screen.code), ['D01', 'D02', 'D03', 'D04', 'D05']);
});
