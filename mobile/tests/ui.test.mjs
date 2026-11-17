import test, { after, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runInThisContext } from 'node:vm';
import ts from 'typescript';
import { JSDOM } from 'jsdom';
import React, { act } from 'react';

const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'http://localhost' });
for (const name of ['window', 'document', 'navigator', 'HTMLElement', 'Element', 'Node', 'ShadowRoot', 'MutationObserver', 'getComputedStyle']) {
  Object.defineProperty(globalThis, name, { value: dom.window[name], configurable: true });
}
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
const { createRoot } = await import('react-dom/client');
const require = createRequire(import.meta.url);
const native = require('react-native-web');
const cache = new Map();
const Svg = ({ accessible, ...props }) => React.createElement('svg', props);
function load(path) {
  if (cache.has(path)) return cache.get(path).exports;
  const module = { exports: {} };
  cache.set(path, module);
  const code = ts.transpileModule(readFileSync(path, 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true } }).outputText;
  const localRequire = name => {
    if (name === 'react-native') return native;
    if (name === 'react-native-safe-area-context') return { useSafeAreaInsets: () => ({ top: 0, bottom: 34, left: 0, right: 0 }) };
    if (name === 'react-native-svg') return { __esModule: true, default: Svg, Circle: 'circle', Path: 'path', Rect: 'rect', Text: 'text' };
    if (name === 'expo-linear-gradient') return { LinearGradient: native.View };
    if (name.startsWith('.')) {
      const base = resolve(dirname(path), name);
      return load(existsSync(`${base}.tsx`) ? `${base}.tsx` : `${base}.ts`);
    }
    return require(name);
  };
  runInThisContext(`(function(require, module, exports) {${code}\n})`, { filename: path })(localRequire, module, module.exports);
  return module.exports;
}
const shared = fileURLToPath(new URL('../shared/', import.meta.url));
const { Button } = load(resolve(shared, 'ui.tsx'));
const geo = load(resolve(shared, 'map/geo.ts'));
let root;
let container;
async function mount(element) {
  container = document.createElement('div');
  document.body.append(container);
  root = createRoot(container);
  await act(async () => root.render(element));
}
const button = label => container.querySelector(`[aria-label="${label}"]`);
function deferred() {
  let resolvePromise;
  let rejectPromise;
  const promise = new Promise((resolve, reject) => { resolvePromise = resolve; rejectPromise = reject; });
  return { promise, resolve: resolvePromise, reject: rejectPromise };
}
afterEach(async () => {
  if (root) await act(async () => root.unmount());
  container?.remove();
  root = null;
});
after(() => dom.window.close());

test('Button blocks duplicate presses, exposes loading, and unlocks after completion', async context => {
  let now = 1000;
  context.mock.method(Date, 'now', () => now);
  const pending = deferred();
  let calls = 0;
  await mount(React.createElement(Button, { title: 'Submit', onPress: () => { calls++; return pending.promise; } }));
  await act(async () => { button('Submit').click(); button('Submit').click(); });
  assert.equal(calls, 1);
  assert.equal(button('Submit').getAttribute('aria-busy'), 'true');
  assert.equal(button('Submit').getAttribute('aria-disabled'), 'true');
  now += 1000;
  await act(async () => button('Submit').click());
  assert.equal(calls, 1);
  await act(async () => pending.resolve());
  assert.notEqual(button('Submit').getAttribute('aria-disabled'), 'true');
  await act(async () => button('Submit').click());
  assert.equal(calls, 2);
});

test('Button debounce protects synchronous actions; disabled and external busy block clicks', async context => {
  context.mock.method(Date, 'now', () => 1000);
  let calls = 0;
  const onPress = () => { calls++; };
  await mount(React.createElement(Button, { title: 'Submit', onPress }));
  await act(async () => { button('Submit').click(); button('Submit').click(); });
  assert.equal(calls, 1);
  for (const state of [{ disabled: true }, { busy: true }]) {
    await act(async () => root.render(React.createElement(Button, { title: 'Submit', onPress, ...state })));
    await act(async () => button('Submit').click());
    assert.equal(calls, 1);
    assert.equal(button('Submit').getAttribute('aria-disabled'), 'true');
  }
});

test('rejected action shows an alert, preserves form input, and permits retry', async context => {
  let now = 1000;
  context.mock.method(Date, 'now', () => now);
  const pending = deferred();
  let calls = 0;
  await mount(React.createElement('div', null,
    React.createElement('input', { defaultValue: 'keep this draft' }),
    React.createElement(Button, { title: 'Submit', onPress: () => { if (++calls === 1) return pending.promise; } })));
  await act(async () => button('Submit').click());
  await act(async () => pending.reject(new Error('offline')));
  assert.ok(container.querySelector('[role="alert"]'));
  assert.equal(container.querySelector('input').value, 'keep this draft');
  assert.notEqual(button('Submit').getAttribute('aria-disabled'), 'true');
  now += 1000;
  await act(async () => button('Submit').click());
  assert.equal(calls, 2);
  assert.equal(container.querySelector('[role="alert"]'), null);
});

test('OSM address labels are short: place, street and district only', () => {
  assert.equal(geo.shortAddress({ name: 'Chợ Bến Thành', address: { road: 'Lê Lợi', suburb: 'Phường Bến Thành', city: 'Thành phố Hồ Chí Minh' } }),
    'Chợ Bến Thành, Lê Lợi, Phường Bến Thành');
  assert.equal(geo.shortAddress({ address: { house_number: '273', road: 'An Dương Vương', city_district: 'Quận 5' } }), '273 An Dương Vương, Quận 5');
  assert.equal(geo.shortAddress({ display_name: 'x'.repeat(300) }).length, 200);
});

test('road route comes from OSRM in km/minutes; any failure falls back to the straight-line estimate', async context => {
  const from = { latitude: 10.7596, longitude: 106.6822 };
  const to = { latitude: 10.7725, longitude: 106.698 };
  context.mock.method(globalThis, 'fetch', async url => {
    assert.match(String(url), /router\.project-osrm\.org\/route\/v1\/driving\/106\.6822,10\.7596;106\.698,10\.7725/);
    return Response.json({ routes: [{ distance: 3456, duration: 545, geometry: { coordinates: [[106.6822, 10.7596], [106.698, 10.7725]] } }] });
  });
  assert.deepEqual(await geo.findRoute(from, to), { distance: 3.5, estimatedTime: 9, path: [[10.7596, 106.6822], [10.7725, 106.698]], source: 'osrm' });
  context.mock.method(globalThis, 'fetch', async () => new Response('busy', { status: 503 }));
  const fallback = await geo.findRoute(from, to);
  assert.equal(fallback.source, 'estimate');
  assert.equal(fallback.path, null);
  assert.ok(fallback.distance > 0 && fallback.estimatedTime >= 1);
});

test('reverse geocoding falls back to coordinates; search keeps only valid points', async context => {
  context.mock.method(globalThis, 'fetch', async () => { throw new Error('offline'); });
  assert.equal(await geo.reverseGeocode({ latitude: 10.123456, longitude: 106.654321 }), '10.12346, 106.65432');
  context.mock.method(globalThis, 'fetch', async url => {
    assert.match(String(url), /countrycodes=vn/);
    return Response.json([{ lat: '10.77', lon: '106.70', name: 'Nhà hát Thành phố', address: { road: 'Đồng Khởi' } }, { lat: 'x', lon: '1' }]);
  });
  assert.deepEqual(await geo.searchPlaces('nhà hát'), [{ latitude: 10.77, longitude: 106.7, address: 'Nhà hát Thành phố, Đồng Khởi' }]);
  assert.deepEqual(await geo.searchPlaces('   '), []);
});
