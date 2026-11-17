import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { findPlace, placeIdOf, places } from '../shared/domain.ts';
import { coordinates } from '../shared/api/trip.ts';

test('every sample place has coordinates and round-trips through its trip address', () => {
  for (const place of places) {
    assert.ok(coordinates[place.id], place.id);
    assert.equal(placeIdOf(place.name), place.id);
    assert.equal(findPlace(place.id), place);
  }
  assert.equal(placeIdOf('Địa chỉ lạ'), '');
});

test('separate mobile identities and entrypoints; both apps use the real API shell only', () => {
  const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const rider = JSON.parse(read('../apps/rider/app.json')).expo;
  const driver = JSON.parse(read('../apps/driver/app.json')).expo;
  assert.notEqual(rider.android.package, driver.android.package);
  assert.notEqual(rider.ios.bundleIdentifier, driver.ios.bundleIdentifier);
  assert.notEqual(rider.scheme, driver.scheme);
  for (const app of ['rider', 'driver']) {
    assert.match(read(`../apps/${app}/index.ts`), /registerRootComponent/);
    assert.match(read(`../apps/${app}/src/App.tsx`), new RegExp(`<MobileApp role="${app}" />`));
    const deps = JSON.parse(read(`../apps/${app}/package.json`)).dependencies;
    assert.equal(deps['react-native-webview'], undefined);
    assert.equal(deps['@react-native-async-storage/async-storage'], undefined);
    const config = createRequire(import.meta.url)(`../apps/${app}/metro.config.js`);
    assert.ok(config.watchFolders.includes(fileURLToPath(new URL('../shared', import.meta.url))));
  }
  const shell = read('../shared/MobileApp.tsx');
  assert.match(shell, /ApiApp/);
  assert.doesNotMatch(shell, /from.*apps\/(rider|driver)|WebView|localStorage|APP_MODE/);
});
