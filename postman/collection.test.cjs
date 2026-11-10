const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const collection = JSON.parse(fs.readFileSync(path.join(__dirname, 'SwiftRide.postman_collection.json')));
const environment = JSON.parse(fs.readFileSync(path.join(__dirname, 'SwiftRide.local.postman_environment.json')));
const requests = collection.item.flatMap(folder => folder.item);
const config = Object.fromEntries(environment.values.map(entry => [entry.key, entry.value]));
function execute(prefix, body, { code = 200, values = {}, now = 1000, event = 'test' } = {}) {
    const item = requests.find(request => request.name.startsWith(prefix));
    const state = new Map(Object.entries(values));
    const failures = [];
    let next;
    function expect(value, negate = false) {
        const chain = {};
        const check = condition => assert.ok(negate ? !condition : condition);
        for (const key of ['to', 'be', 'and', 'at']) Object.defineProperty(chain, key, { get: () => chain });
        Object.defineProperty(chain, 'not', { get: () => expect(value, !negate) });
        Object.defineProperty(chain, 'empty', { get: () => { check(value.length === 0); return chain; } });
        chain.eql = expected => { check(JSON.stringify(value) === JSON.stringify(expected)); return chain; };
        chain.include = expected => { check(value.includes(expected)); return chain; };
        chain.a = type => { check(typeof value === type); return chain; };
        chain.above = minimum => { check(value > minimum); return chain; };
        chain.least = minimum => { check(value >= minimum); return chain; };
        return chain;
    }
    const pm = {
        environment: { get: key => config[key] },
        variables: { replaceIn: () => '00000000-0000-0000-0000-000000000001' },
        collectionVariables: { get: key => state.get(key), set: (key, value) => state.set(key, value), unset: key => state.delete(key) },
        info: { requestName: item.name },
        execution: { setNextRequest: value => { next = value; } },
        response: { code, responseTime: 1, json: () => body, headers: { get: () => 'no-store' } },
        test: (name, callback) => { try { callback(); } catch (error) { failures.push(error.message); } },
        expect
    };
    const script = item.event.find(entry => entry.listen === event).script.exec.join('\n');
    vm.runInNewContext(script, { pm, Date: { now: () => now } });
    return { state, failures, next, name: item.name };
}
test('Collection has unique names, Gateway-only URLs, no persisted tokens and valid script syntax', () => {
    assert.equal(requests.length, 52);
    assert.equal(new Set(requests.map(item => item.name)).size, requests.length);
    assert.deepEqual(collection.variable, []);
    for (const item of requests) {
        assert.ok(item.request.url.startsWith('{{baseUrl}}/'));
        assert.notEqual(item.request.url, '{{baseUrl}}/api/payments');
        for (const entry of item.event) new vm.Script(entry.script.exec.join('\n'));
        if (item.request.body) JSON.parse(item.request.body.raw);
    }
    assert.ok(!environment.values.some(entry => /token|tripId|paymentId/i.test(entry.key)));
});
test('Readiness resets runtime state and creates a fresh run ID', () => {
    const result = execute('00 ', {}, { event: 'prerequest', values: { riderToken: 'old', tripId: 'old' } });
    assert.equal(result.state.has('riderToken'), false);
    assert.equal(result.state.has('tripId'), false);
    assert.ok(result.state.get('runId'));
});
test('Login captures tokens only on successful response', () => {
    const valid = execute('04 ', { accessToken: 'token', refreshToken: 'refresh' });
    assert.equal(valid.state.get('riderToken'), 'token');
    assert.deepEqual(valid.failures, []);
    const invalid = execute('04 ', {}, { code: 401 });
    assert.equal(invalid.state.has('riderToken'), false);
    assert.equal(invalid.next, null);
    assert.equal(invalid.failures.length, 1);
});
test('Invitation polling repeats, then clears deadline only for matching Trip', () => {
    const pending = execute('26 ', [{ id: 'other' }], { values: { tripId: 'trip', invitationDeadline: 2000 } });
    assert.equal(pending.next, pending.name);
    const ready = execute('26 ', [{ id: 'trip' }], { values: { tripId: 'trip', invitationDeadline: 2000 } });
    assert.equal(ready.next, undefined);
    assert.equal(ready.state.has('invitationDeadline'), false);
    assert.deepEqual(ready.failures, []);
});
test('Invitation timeout fails and stops instead of reporting success', () => {
    const result = execute('26 ', [], { values: { tripId: 'trip', invitationDeadline: 999 } });
    assert.equal(result.next, null);
    assert.equal(result.failures.length, 1);
});
test('Poll pre-request keeps original deadline across retries', () => {
    const first = execute('35 ', {}, { event: 'prerequest' });
    assert.equal(first.state.get('paymentDeadline'), 46000);
    const retry = execute('35 ', {}, { event: 'prerequest', now: 2000, values: { paymentDeadline: 46000 } });
    assert.equal(retry.state.get('paymentDeadline'), 46000);
});
test('Payment poll accepts numeric and string Paid states', () => {
    for (const status of [7, 'Paid']) {
        const result = execute('35 ', { status }, { values: { paymentDeadline: 2000 } });
        assert.deepEqual(result.failures, []);
        assert.equal(result.state.has('paymentDeadline'), false);
    }
});
test('Payment pending retries; terminal failure, timeout and HTTP error stop', () => {
    const pending = execute('35 ', { status: 6 }, { values: { paymentDeadline: 2000 } });
    assert.equal(pending.next, pending.name);
    for (const options of [
        { body: { status: 'Failed' }, values: { paymentDeadline: 2000 } },
        { body: { status: 'Cancelled' }, values: { paymentDeadline: 2000 } },
        { body: { status: 6 }, values: { paymentDeadline: 999 } },
        { body: {}, code: 500 }
    ]) {
        const result = execute('35 ', options.body, options);
        assert.equal(result.next, null);
        assert.equal(result.failures.length, 1);
    }
});
test('Wrong payment amount or key stops flow', () => {
    const result = execute('36 ', { tripId: 'trip', status: 'Succeeded', amount: 999 }, { values: { tripId: 'trip', fareEstimate: 50000 } });
    assert.equal(result.next, null);
    assert.equal(result.failures.length, 1);
});
