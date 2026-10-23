import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

test('each frontend owns its entry, scripts, config and audience screen', () => {
  for (const [app, screen, port] of [['rider', 'Rider', 5173], ['driver', 'Driver', 5174]]) {
    const manifest = JSON.parse(read(`../apps/${app}/package.json`));
    assert.equal(manifest.name, `@swiftride/${app}`);
    assert.ok(manifest.scripts.dev && manifest.scripts.build && manifest.scripts.preview);
    assert.match(read(`../apps/${app}/index.html`), new RegExp(`SwiftRide ${screen}`));
    assert.match(read(`../apps/${app}/src/App.tsx`), new RegExp(`import ${screen} from './${screen}'`));
    assert.match(read(`../apps/${app}/vite.config.ts`), new RegExp(`port: ${port}, strictPort: true`));
  }
});

test('shared shell does not import either audience screen or choose an app from URL', () => {
  const shell = read('../shared/TripShell.tsx');
  assert.doesNotMatch(shell, /import (Rider|Driver) from/);
  assert.doesNotMatch(shell, /location\.pathname|roleFromPath/);
});

test('production bundles are separate when CHECK_BUILDS is enabled', { skip: !process.env.CHECK_BUILDS }, () => {
  const bundle = app => readdirSync(new URL(`../apps/${app}/dist/assets/`, import.meta.url))
    .filter(file => file.endsWith('.js'))
    .map(file => read(`../apps/${app}/dist/assets/${file}`)).join('\n');
  const rider = bundle('rider');
  const driver = bundle('driver');
  assert.ok(rider.includes('Bạn muốn đi đâu?'));
  assert.ok(!driver.includes('Bạn muốn đi đâu?'));
  assert.ok(driver.includes('Sẵn sàng cho những hành trình mới?'));
  assert.ok(!rider.includes('Sẵn sàng cho những hành trình mới?'));
});
