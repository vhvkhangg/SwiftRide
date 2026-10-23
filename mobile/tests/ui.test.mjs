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
import { initialTrip, snapshot, storageKey } from '../shared/domain.ts';

const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'http://localhost' });
for (const name of ['window', 'document', 'navigator', 'HTMLElement', 'Element', 'Node', 'ShadowRoot', 'MutationObserver', 'getComputedStyle']) {
  Object.defineProperty(globalThis, name, { value: dom.window[name], configurable: true });
}
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
const { createRoot } = await import('react-dom/client');
const require = createRequire(import.meta.url);
const native = require('react-native-web');
const storage = { getItem: async () => null, setItem: async () => {} };
const cache = new Map();
const Svg = ({ accessible, ...props }) => React.createElement('svg', props);
function load(path) {
  if (cache.has(path)) return cache.get(path).exports;
  const module = { exports: {} };
  cache.set(path, module);
  const code = ts.transpileModule(readFileSync(path, 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true } }).outputText;
  const localRequire = name => {
    if (name === 'react-native') return native;
    if (name === '@react-native-async-storage/async-storage') return storage;
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
const { useTrip } = load(resolve(shared, 'useTrip.ts'));
const { default: Login } = load(resolve(shared, 'Login.tsx'));
const { default: TripActions } = load(resolve(shared, 'TripActions.tsx'));
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
  storage.getItem = async () => null;
  storage.setItem = async () => {};
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

test('invalid login preserves email and password; password visibility works', async () => {
  let logins = 0;
  await mount(React.createElement(Login, { role: 'rider', onLogin: () => logins++ }));
  await act(async () => button('Điền tài khoản demo').click());
  const password = button('Mật khẩu');
  await act(async () => {
    Object.getOwnPropertyDescriptor(dom.window.HTMLInputElement.prototype, 'value').set.call(password, 'wrong-password');
    password.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
  });
  await act(async () => button('Đăng nhập').click());
  assert.equal(logins, 0);
  assert.equal(button('Tài khoản').value, 'rider@swiftride.demo');
  assert.equal(password.value, 'wrong-password');
  assert.ok(container.querySelector('[role="alert"]'));
  await act(async () => button('Hiện mật khẩu').click());
  assert.equal(password.type, 'text');
  await act(async () => button('Ẩn mật khẩu').click());
  assert.equal(password.type, 'password');
});

test('failed restore never overwrites saved trip; retry restores it', async () => {
  const saved = { ...initialTrip, destination: 'market', status: 'searching' };
  let reads = 0;
  const writes = [];
  storage.getItem = async () => { if (++reads === 1) throw new Error('unavailable'); return snapshot(saved, 'rider'); };
  storage.setItem = async (...args) => { writes.push(args); };
  let state;
  function Probe() { state = useTrip('rider'); return null; }
  await mount(React.createElement(Probe));
  assert.equal(state.ready, true);
  assert.equal(state.canSave, false);
  assert.equal(writes.length, 0);
  await act(async () => state.retryLoad());
  assert.deepEqual(state.trip, saved);
  assert.equal(state.canSave, true);
  assert.equal(writes.length, 1);
  assert.equal(writes[0][0], storageKey('rider'));
});

test('save failure preserves current trip; retry exposes loading and saves same snapshot', async () => {
  const saved = { ...initialTrip, destination: 'market', status: 'quoted' };
  const pending = deferred();
  const writes = [];
  storage.getItem = async () => snapshot(saved, 'rider');
  storage.setItem = async (...args) => { writes.push(args); if (writes.length === 1) throw new Error('quota'); await pending.promise; };
  let state;
  function Probe() { state = useTrip('rider'); return React.createElement(Button, { title: 'Retry', busy: state.saving, onPress: state.retrySave }); }
  await mount(React.createElement(Probe));
  assert.equal(state.saveFailed, true);
  assert.deepEqual(state.trip, saved);
  await act(async () => { button('Retry').click(); button('Retry').click(); });
  assert.equal(state.saving, true);
  assert.equal(button('Retry').getAttribute('aria-busy'), 'true');
  assert.equal(writes.length, 2);
  assert.deepEqual(writes[0], writes[1]);
  await act(async () => pending.resolve());
  assert.equal(state.saveFailed, false);
  assert.equal(state.saving, false);
  assert.deepEqual(state.trip, saved);
});

test('storage writes remain ordered and finish with latest trip', async () => {
  const pending = deferred();
  const writes = [];
  storage.setItem = async (...args) => { writes.push(args); if (writes.length === 1) await pending.promise; };
  let state;
  function Probe() { state = useTrip('rider'); return null; }
  await mount(React.createElement(Probe));
  await act(async () => state.dispatch({ type: 'place', field: 'destination', id: 'market' }));
  await act(async () => state.dispatch({ type: 'quote' }));
  assert.equal(writes.length, 1);
  assert.equal(state.saving, true);
  await act(async () => pending.resolve());
  assert.equal(writes.length, 3);
  assert.equal(writes.at(-1)[1], snapshot(state.trip, 'rider'));
  assert.equal(state.saving, false);
});

test('trip actions follow role and payment state; dropoff requires confirmation', async () => {
  const actions = [];
  let confirmation;
  const props = { dispatch: action => actions.push(action), confirm: (...args) => { confirmation = args; } };
  await mount(null);
  for (const [role, status, label] of [
    ['rider', 'quoted', 'Xác nhận đặt xe'], ['driver', 'searching', 'Nhận chuyến'],
    ['driver', 'accepted', 'Bắt đầu đi đón'], ['driver', 'enroute', 'Đã đón khách'],
    ['driver', 'pickedup', 'Đã trả khách'], ['rider', 'droppedoff', 'Thanh toán mô phỏng'],
  ]) {
    await act(async () => root.render(React.createElement(TripActions, { ...props, role, trip: { ...initialTrip, destination: 'market', status } })));
    assert.ok(button(label), `${role}/${status}`);
  }
  await act(async () => root.render(React.createElement(TripActions, { ...props, role: 'driver', trip: { ...initialTrip, status: 'pickedup' } })));
  await act(async () => button('Đã trả khách').click());
  assert.equal(actions.length, 0);
  assert.equal(confirmation[0], 'Đã trả khách?');
  confirmation[2]();
  assert.deepEqual(actions, [{ type: 'dropoff' }]);
  for (const trip of [{ ...initialTrip }, { ...initialTrip, status: 'payment_pending', paymentError: true, paymentAttempts: 3 }]) {
    await act(async () => root.render(React.createElement(TripActions, { ...props, role: 'rider', trip })));
    assert.equal(container.querySelector('[role="button"]').getAttribute('aria-disabled'), 'true');
  }
  await act(async () => root.render(React.createElement(TripActions, { ...props, role: 'driver', trip: { ...initialTrip, status: 'droppedoff' } })));
  assert.equal(container.querySelector('[role="button"]'), null);
});

test('unmount during restore prevents late writes', async () => {
  const pending = deferred();
  let writes = 0;
  storage.getItem = () => pending.promise;
  storage.setItem = async () => { writes++; };
  function Probe() { useTrip('rider'); return null; }
  await mount(React.createElement(Probe));
  await act(async () => root.unmount());
  root = null;
  await act(async () => pending.resolve(snapshot(initialTrip, 'rider')));
  assert.equal(writes, 0);
});

test('late failed save does not replace successful latest snapshot with an error', async () => {
  const pending = deferred();
  const writes = [];
  storage.setItem = async (...args) => { writes.push(args); if (writes.length === 1) await pending.promise; };
  let state;
  function Probe() { state = useTrip('rider'); return null; }
  await mount(React.createElement(Probe));
  await act(async () => state.dispatch({ type: 'place', field: 'destination', id: 'park' }));
  await act(async () => pending.reject(new Error('old write failed')));
  assert.equal(writes.length, 2);
  assert.equal(writes[1][1], snapshot(state.trip, 'rider'));
  assert.equal(state.saveFailed, false);
  assert.equal(state.saving, false);
});
