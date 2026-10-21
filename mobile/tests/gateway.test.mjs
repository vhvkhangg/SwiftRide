import test from 'node:test';
import assert from 'node:assert/strict';
import { readConfig } from '../shared/config.ts';
import { GatewayError, requestGateway } from '../shared/gateway.ts';

test('demo is default; API configuration fails closed', () => {
  assert.equal(readConfig().mode, 'demo');
  assert.ok(readConfig('typo').error);
  for (const url of ['', 'invalid', 'file:///tmp', 'https://user:pass@example.com', 'https://example.com?token=secret', 'https://example.com#secret']) assert.ok(readConfig('api', url).error);
  assert.deepEqual(readConfig('api', 'http://localhost:5000/'), { mode: 'api', gatewayUrl: 'http://localhost:5000', error: '' });
});

test('Gateway preserves request body and headers without inventing endpoints', async context => {
  const calls = [];
  context.mock.method(globalThis, 'fetch', async (...args) => { calls.push(args); return Response.json({ status: 'pending' }); });
  const options = { method: 'POST', headers: { Authorization: 'Bearer test', 'Content-Type': 'application/json' }, body: '{"example":true}' };
  assert.deepEqual(await requestGateway('https://example.com/api/', '/test', options), { status: 'pending' });
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], 'https://example.com/api/test');
  assert.equal(calls[0][1].body, options.body);
  assert.deepEqual(calls[0][1].headers, options.headers);
});

test('Gateway rejects unsafe paths and invalid URLs before sending credentials', async context => {
  const fetch = context.mock.method(globalThis, 'fetch', async () => Response.json({}));
  for (const [base, path] of [['bad', '/test'], ['https://example.com', '//evil.test'], ['https://example.com', '/\\evil.test'], ['https://user:pass@example.com', '/test'], ['file:///tmp', '/test']]) await assert.rejects(requestGateway(base, path), GatewayError);
  assert.equal(fetch.mock.callCount(), 0);
});

test('HTTP failures, empty and invalid responses are explicit; no automatic retry', async context => {
  for (const status of [401, 403, 409, 500]) {
    const fetch = context.mock.method(globalThis, 'fetch', async () => new Response('private server details', { status }));
    await assert.rejects(requestGateway('https://example.com', '/test'), error => error instanceof GatewayError && error.status === status && !error.message.includes('private'));
    assert.equal(fetch.mock.callCount(), 1);
    fetch.mock.restore();
  }
  context.mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }));
  assert.equal(await requestGateway('https://example.com', '/test'), null);
  context.mock.method(globalThis, 'fetch', async () => new Response('not JSON'));
  await assert.rejects(requestGateway('https://example.com', '/test'), /JSON/);
});

test('network failure and timeout never retry potentially committed writes', async context => {
  const offline = context.mock.method(globalThis, 'fetch', async () => { throw new TypeError('offline'); });
  await assert.rejects(requestGateway('https://example.com', '/test', { method: 'POST' }), /Không kết nối/);
  assert.equal(offline.mock.callCount(), 1);
  offline.mock.restore();
  const pending = context.mock.method(globalThis, 'fetch', (_url, { signal }) => new Promise((_resolve, reject) => {
    if (signal.aborted) reject(new Error('aborted'));
    else signal.addEventListener('abort', () => reject(new Error('aborted')), { once: true });
  }));
  await assert.rejects(requestGateway('https://example.com', '/test', { method: 'POST' }, 5), /Chưa xác định kết quả/);
  const controller = new AbortController();
  controller.abort();
  await assert.rejects(requestGateway('https://example.com', '/test', { signal: controller.signal }), /bị hủy/);
  assert.equal(pending.mock.callCount(), 2);
});
