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
    vm.runInNewContext(script, { pm, atob, btoa, Date: { now: () => now } });
    return { state, failures, next, name: item.name };
}
test('Collection has unique names, Gateway or Keycloak URLs, no persisted tokens and valid script syntax', () => {
    assert.equal(requests.length, 78);
    assert.equal(new Set(requests.map(item => item.name)).size, requests.length);
    assert.deepEqual(collection.variable, []);
    for (const item of requests) {
        assert.ok(item.request.url.startsWith('{{baseUrl}}/') || item.request.url.startsWith('{{keycloakUrl}}/realms/{{realm}}/'));
        assert.ok(!item.request.url.startsWith('{{baseUrl}}/auth/'), 'Gateway no longer issues tokens');
        assert.notEqual(item.request.url, '{{baseUrl}}/api/payments');
        for (const entry of item.event) new vm.Script(entry.script.exec.join('\n'));
        if (item.request.body?.mode === 'raw') JSON.parse(item.request.body.raw);
        if (item.request.body?.mode === 'urlencoded') assert.ok(item.request.body.urlencoded.every(field => field.key && field.value));
    }
    assert.ok(!environment.values.some(entry => /token|tripId|paymentId/i.test(entry.key)));
    assert.equal(config.clientId, 'swiftride-postman');
    const tokenRequests = requests.filter(item => item.request.url.endsWith('/protocol/openid-connect/token'));
    for (const item of tokenRequests) {
        const fields = Object.fromEntries(item.request.body.urlencoded.map(field => [field.key, field.value]));
        assert.ok(['password', 'refresh_token'].includes(fields.grant_type));
        assert.ok(['{{clientId}}', 'swiftride-mobile'].includes(fields.client_id));
    }
});
test('Readiness resets runtime state and creates a fresh run ID', () => {
    const result = execute('00 ', {}, { event: 'prerequest', values: { riderToken: 'old', tripId: 'old' } });
    assert.equal(result.state.has('riderToken'), false);
    assert.equal(result.state.has('tripId'), false);
    assert.ok(result.state.get('runId'));
});
test('In-app sign-up goes through the Gateway and must create a Rider for this run', () => {
    const item = requests.find(request => request.name.startsWith('03 '));
    assert.equal(item.request.url, '{{baseUrl}}/api/auth/register');
    assert.equal(item.request.auth.type, 'noauth');
    const body = JSON.parse(item.request.body.raw);
    assert.ok(!('role' in body) && !('realmRoles' in body));
    const ok = execute('03 ', { email: 'postman.run@example.com', role: 'Rider' }, { code: 201, values: { runId: 'run' } });
    assert.deepEqual(ok.failures, []);
    assert.equal(execute('03 ', { email: 'postman.run@example.com', role: 'Driver' }, { code: 201, values: { runId: 'run' } }).failures.length, 1);
    const taken = execute('03 ', {}, { code: 409, values: { runId: 'run' } });
    assert.equal(taken.next, null);
});
test('Main Trip is paid by Card end to end; the method is part of the idempotent payload', () => {
    const body = prefix => JSON.parse(requests.find(request => request.name.startsWith(prefix)).request.body.raw);
    assert.equal(body('24 ').paymentMethod, 'Card');
    assert.equal(body('25 ').paymentMethod, 'Card');
    assert.equal(body('27 ').paymentMethod, 'Wallet');
    assert.equal(body('27 ').idempotencyKey, body('24 ').idempotencyKey);
    assert.equal(body('28 ').paymentMethod, 'Cash');
    assert.notEqual(body('28 ').idempotencyKey, body('24 ').idempotencyKey);
    assert.equal(body('63 ').paymentMethod, undefined, 'cancel scenario keeps the default (Wallet)');
    const trip = { riderId: 'rider', status: 'Requested', fareEstimate: 44, id: 'trip' };
    assert.deepEqual(execute('24 ', { ...trip, paymentMethod: 'Card' }, { code: 201, values: { riderId: 'rider' } }).failures, []);
    assert.equal(execute('24 ', { ...trip, paymentMethod: 'Wallet' }, { code: 201, values: { riderId: 'rider' } }).failures.length, 1);
    assert.deepEqual(execute('27 ', { error: 'IdempotencyKey đã được sử dụng với payload khác.' }, { code: 409 }).failures, []);
    assert.equal(execute('27 ', {}, { code: 201 }).next, null);
    assert.deepEqual(execute('28 ', { error: 'Phương thức thanh toán chỉ nhận Wallet hoặc Card.' }, { code: 400 }).failures, []);
    const payment = { tripId: 'trip', status: 'Succeeded', amount: 44, idempotencyKey: 'trip-trip-payment-v1', id: 'pay' };
    const values = { tripId: 'trip', fareEstimate: 44, paymentId: 'pay' };
    assert.deepEqual(execute('43 ', { ...payment, paymentMethod: 'Card' }, { values }).failures, []);
    assert.equal(execute('43 ', { ...payment, paymentMethod: 'Wallet' }, { values }).failures.length, 1);
    assert.deepEqual(execute('44 ', { id: 'pay', paymentMethod: 'Card' }, { values }).failures, []);
    assert.deepEqual(execute('63 ', { id: 'c', paymentMethod: 'Wallet' }, { code: 201 }).failures, []);
});
test('Login captures tokens only on successful response', () => {
    const valid = execute('04 ', { access_token: 'token', refresh_token: 'refresh', token_type: 'Bearer' });
    assert.equal(valid.state.get('riderToken'), 'token');
    assert.deepEqual(valid.failures, []);
    const invalid = execute('04 ', {}, { code: 401 });
    assert.equal(invalid.state.has('riderToken'), false);
    assert.equal(invalid.next, null);
    assert.equal(invalid.failures.length, 1);
});
test('Invitation polling repeats, then clears deadline only for matching Trip', () => {
    const pending = execute('33 ', [{ id: 'other' }], { values: { tripId: 'trip', invitationDeadline: 2000 } });
    assert.equal(pending.next, pending.name);
    const ready = execute('33 ', [{ id: 'trip' }], { values: { tripId: 'trip', invitationDeadline: 2000 } });
    assert.equal(ready.next, undefined);
    assert.equal(ready.state.has('invitationDeadline'), false);
    assert.deepEqual(ready.failures, []);
});
test('Invitation timeout fails and stops instead of reporting success', () => {
    const result = execute('33 ', [], { values: { tripId: 'trip', invitationDeadline: 999 } });
    assert.equal(result.next, null);
    assert.equal(result.failures.length, 1);
});
test('Poll pre-request keeps original deadline across retries', () => {
    const first = execute('42 ', {}, { event: 'prerequest' });
    assert.equal(first.state.get('paymentDeadline'), 46000);
    const retry = execute('42 ', {}, { event: 'prerequest', now: 2000, values: { paymentDeadline: 46000 } });
    assert.equal(retry.state.get('paymentDeadline'), 46000);
});
test('Payment poll accepts numeric and string Paid states', () => {
    for (const status of [7, 'Paid']) {
        const result = execute('42 ', { status }, { values: { paymentDeadline: 2000 } });
        assert.deepEqual(result.failures, []);
        assert.equal(result.state.has('paymentDeadline'), false);
    }
});
test('Payment pending retries; terminal failure, timeout and HTTP error stop', () => {
    const pending = execute('42 ', { status: 6 }, { values: { paymentDeadline: 2000 } });
    assert.equal(pending.next, pending.name);
    for (const options of [
        { body: { status: 'Failed' }, values: { paymentDeadline: 2000 } },
        { body: { status: 'Cancelled' }, values: { paymentDeadline: 2000 } },
        { body: { status: 6 }, values: { paymentDeadline: 999 } },
        { body: {}, code: 500 }
    ]) {
        const result = execute('42 ', options.body, options);
        assert.equal(result.next, null);
        assert.equal(result.failures.length, 1);
    }
});
test('Wrong payment amount or key stops flow', () => {
    const result = execute('43 ', { tripId: 'trip', status: 'Succeeded', amount: 999 }, { values: { tripId: 'trip', fareEstimate: 50000 } });
    assert.equal(result.next, null);
    assert.equal(result.failures.length, 1);
});
function jwt(payload) {
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.c2lnbmF0dXJl`;
}
const decodePart = part => JSON.parse(Buffer.from(part, 'base64url').toString());
test('Forged tokens keep the real signature or drop it, never re-sign', () => {
    const riderToken = jwt({ sub: 'rider', role: 'Rider', exp: 9999999999 });
    const signature = riderToken.split('.')[2];
    const tampered = execute('13 ', {}, { event: 'prerequest', values: { riderToken } }).state.get('tamperedToken').split('.');
    assert.equal(decodePart(tampered[1]).role, 'Admin');
    assert.equal(tampered[2], signature);
    const unsigned = execute('14 ', {}, { event: 'prerequest', values: { riderToken } }).state.get('unsignedToken').split('.');
    assert.equal(decodePart(unsigned[0]).alg, 'none');
    assert.equal(unsigned[2], '');
    const expired = execute('15 ', {}, { event: 'prerequest', values: { riderToken }, now: 2_000_000_000_000 }).state.get('expiredToken').split('.');
    assert.equal(decodePart(expired[1]).role, 'Rider');
    assert.ok(decodePart(expired[1]).exp < 2_000_000_000);
    assert.equal(expired[2], signature);
});
test('Admin login stores Admin session only', () => {
    const result = execute('08 ', { access_token: 'admin', refresh_token: 'admin-refresh', token_type: 'Bearer' });
    assert.equal(result.state.get('adminToken'), 'admin');
    assert.equal(result.state.get('adminRefresh'), 'admin-refresh');
    assert.equal(result.state.has('driverToken'), false);
});
test('Admin refund must return Refunded payment with Trip fare', () => {
    const values = { paymentId: 'pay', tripId: 'trip', fareEstimate: '50000' };
    assert.deepEqual(execute('48 ', { paymentId: 'pay', tripId: 'trip', status: 'Refunded', amount: 50000 }, { values }).failures, []);
    const wrong = execute('48 ', { paymentId: 'pay', tripId: 'trip', status: 'Succeeded', amount: 50000 }, { values });
    assert.equal(wrong.next, null);
    assert.equal(wrong.failures.length, 1);
    assert.equal(execute('49 ', {}, { code: 200 }).next, null);
});
test('Reject scenario polls each Driver for the reject Trip with separate deadlines', () => {
    const first = execute('54 ', [{ id: 'reject' }], { values: { tripId: 'main', rejectTripId: 'reject', rejectInvitationDeadline: 2000 } });
    assert.equal(first.next, undefined);
    assert.equal(first.state.has('rejectInvitationDeadline'), false);
    const waiting = execute('58 ', [{ id: 'main' }], { values: { tripId: 'main', rejectTripId: 'reject', reassignInvitationDeadline: 2000 } });
    assert.equal(waiting.next, waiting.name);
    const timedOut = execute('58 ', [], { values: { rejectTripId: 'reject', reassignInvitationDeadline: 999 } });
    assert.equal(timedOut.next, null);
    assert.equal(timedOut.failures.length, 1);
});
test('Reassigned Trip must still be unassigned and waiting', () => {
    assert.deepEqual(execute('59 ', { status: 'PriceConfirmed', driverId: null, declineCount: 1, declineThreshold: 2 }).failures, []);
    const accepted = execute('59 ', { status: 'DriverAccepted', driverId: 'driver' });
    assert.equal(accepted.next, null);
    assert.equal(accepted.failures.length, 1);
});
test('Identity comes from Keycloak userinfo; Rider token must not carry Driver/Admin roles', () => {
    const rider = execute('10 ', { sub: 'rider-sub', role: ['Rider'] });
    assert.deepEqual(rider.failures, []);
    assert.equal(rider.state.get('riderId'), 'rider-sub');
    const escalated = execute('10 ', { sub: 'rider-sub', role: ['Rider', 'Admin'] });
    assert.equal(escalated.next, null);
    assert.equal(escalated.failures.length, 1);
    assert.equal(execute('11 ', { sub: 'driver-sub', role: ['Driver'] }).state.get('driverId'), 'driver-sub');
});
test('Revoked Keycloak refresh token must fail with invalid_grant', () => {
    assert.deepEqual(execute('72 ', { error: 'invalid_grant' }, { code: 400 }).failures, []);
    assert.equal(execute('72 ', { access_token: 'x' }, { code: 200 }).next, null);
});
test('Cancelled Trip: invalid action must leave status and updatedAt unchanged', () => {
    const values = { cancelUpdatedAt: '2026-11-16T10:00:00Z' };
    assert.deepEqual(execute('65 ', { status: 'Cancelled', updatedAt: '2026-11-16T10:00:00Z' }).state.get('cancelUpdatedAt'), '2026-11-16T10:00:00Z');
    assert.deepEqual(execute('68 ', { status: 8, updatedAt: '2026-11-16T10:00:00Z', driverId: null }, { values }).failures, []);
    const changed = execute('68 ', { status: 8, updatedAt: '2026-11-16T10:05:00Z', driverId: null }, { values });
    assert.equal(changed.next, null);
    assert.equal(changed.failures.length, 1);
    assert.equal(execute('66 ', {}, { code: 200 }).next, null);
});
