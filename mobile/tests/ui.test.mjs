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
const { Button, CityMap } = load(resolve(shared, 'ui.tsx'));
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

test('CityMap labels pickup and destination for screen readers', async () => {
  await mount(React.createElement(CityMap, { pickup: 'university', destination: 'market' }));
  assert.match(container.querySelector('[role="img"]').getAttribute('aria-label'), /Đại học Sài Gòn.*Chợ Bến Thành/);
  await act(async () => root.render(React.createElement(CityMap, { pickup: 'park' })));
  assert.doesNotMatch(container.querySelector('[role="img"]').getAttribute('aria-label'), /điểm đến/);
});
