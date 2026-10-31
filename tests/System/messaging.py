"""Isolated real-stack recovery tests. Never reuse development databases or delete volumes."""
import argparse
import base64
import concurrent.futures
import datetime as dt
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
PAYMENT = "swiftride-payment-process-payment"
FOUND = "swiftride-trip-driver-found-for-trip"


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def wait(operation, predicate=bool, seconds=120):
    deadline = time.monotonic() + seconds
    last = None
    while time.monotonic() < deadline:
        try:
            last = operation()
            if predicate(last):
                return last
        except (urllib.error.URLError, TimeoutError, ConnectionError, RuntimeError):
            pass
        time.sleep(0.5)
    raise TimeoutError(f"Condition not met in {seconds}s; last result: {last!r}")


def http(method, url, body=None, token=None, expected=200, headers=None):
    headers = dict(headers or {})
    if token:
        headers["Authorization"] = "Bearer " + token
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            check(response.status == expected, f"Unexpected HTTP status {response.status}")
            payload = response.read()
            if not payload:
                return None
            return json.loads(payload) if "json" in response.headers.get("Content-Type", "") else payload.decode()
    except urllib.error.HTTPError as error:
        raise RuntimeError(f"HTTP {method} {urllib.parse.urlsplit(url).path}: {error.code}") from None


class Stack:
    def __init__(self, project):
        self.project = project
        self.env = dict(os.environ)
        self.env.update(RABBITMQ_USER="task5", RABBITMQ_VHOST="task5", TRIP_DB_NAME="swiftride_trip_test",
                        PAYMENT_DB_NAME="swiftride_payment_test", MATCH_DB_NAME="swiftride_matching_test",
                        TRIP_DB_USER="swiftride", PAYMENT_DB_USER="swiftride")
        self.secrets = []
        for key in ("RABBITMQ_PASSWORD", "TRIP_DB_PASSWORD", "PAYMENT_DB_PASSWORD", "AUTH_DB_PASSWORD",
                    "JWT_SECRET_KEY", "SERVICE_AUTH_SECRET_KEY", "DEV_ACCOUNT_PASSWORD"):
            self.env[key] = secrets.token_hex(24) + "Aa1!"
            self.secrets.append(self.env[key])
        with tempfile.NamedTemporaryFile(mode="w", suffix=".env", delete=False) as empty:
            self.env_file = Path(empty.name)

    def run(self, command, timeout=120, env=None):
        result = subprocess.run(command, cwd=ROOT, env=env or self.env, capture_output=True,
                                text=True, encoding="utf-8", errors="replace", timeout=timeout)
        output = result.stdout + result.stderr
        for secret in self.secrets:
            output = output.replace(secret, "[redacted]")
        if result.returncode:
            raise RuntimeError(f"Command failed ({result.returncode}): {output[-5000:]}")
        return result.stdout.strip()

    def compose(self, *args, timeout=120):
        return self.run(["docker", "compose", "--env-file", str(self.env_file), "-p", self.project,
                         "-f", "docker-compose.yml", "-f", "tests/System/compose.yml", *args], timeout)

    def port(self, service, port):
        return self.compose("port", service, str(port)).rsplit(":", 1)[1]

    def url(self, service, port=8080):
        return f"http://127.0.0.1:{self.port(service, port)}"

    @property
    def gateway(self):
        return self.url("api-gateway")

    def sql(self, service, query):
        database = self.env["TRIP_DB_NAME" if service == "trip-postgres" else "PAYMENT_DB_NAME"]
        return self.compose("exec", "-T", service, "psql", "-U", "swiftride", "-d", database,
                            "-v", "ON_ERROR_STOP=1", "-Atc", query)

    def mongo(self, script):
        return self.compose("exec", "-T", "matching-mongodb", "mongosh", "--quiet",
                            self.env["MATCH_DB_NAME"], "--eval", script)

    def rabbit(self, method, path, body=None):
        auth = base64.b64encode(f"task5:{self.env['RABBITMQ_PASSWORD']}".encode()).decode()
        return http(method, self.url("rabbitmq", 15672) + "/api/" + path, body,
                    headers={"Authorization": "Basic " + auth})

    def queue(self, name):
        return self.rabbit("GET", "queues/task5/" + name)

    def publish(self, queue, payload, properties):
        result = self.rabbit("POST", "exchanges/task5/amq.default/publish",
                             dict(routing_key=queue, payload=payload, payload_encoding="string", properties=properties))
        check(result["routed"], f"Message not routed: {queue}")

    def send(self, queue, contract, message, message_id=None):
        envelope = dict(messageId=message_id or str(uuid.uuid4()), correlationId=message["correlationId"],
                        messageType=["urn:message:SwiftRide.Contracts." + contract], message=message,
                        sentTime=dt.datetime.now(dt.timezone.utc).isoformat(), headers={})
        self.publish(queue, json.dumps(envelope),
                     {"content_type": "application/vnd.masstransit+json", "delivery_mode": 2})
        return envelope["messageId"]

    def status(self, trip_id):
        return self.sql("trip-postgres", f'''SELECT "Status" FROM "Trips" WHERE "Id"='{trip_id}';''')

    def create(self, key=None):
        return http("POST", self.gateway + "/api/trips/", dict(pickupAddress="Task5 pickup",
                    dropoffAddress="Task5 dropoff", pickupLatitude=10.77, pickupLongitude=106.69,
                    distance=5, estimatedTime=10, idempotencyKey=key), self.rider, 201)

    def action(self, trip_id, action, driver=False):
        expected = 202 if action in ("confirm-price", "driver-reject", "dropoff") else 204
        return http("POST", self.gateway + f"/api/trips/{trip_id}/{action}", {},
                    self.driver if driver else self.rider, expected)

    def available(self):
        http("POST", self.gateway + "/api/drivers/location",
             dict(latitude=10.77, longitude=106.69, isAvailable=True), self.driver)
        wait(lambda: self.mongo(f'print(db.drivers.countDocuments({{DriverId:"{self.driver_id}",IsAvailable:true}}))'), lambda value: value == "1")

    def invitation(self, trip_id):
        return next((item for item in http("GET", self.gateway + "/api/trips/driver/invitations", token=self.driver)
                     if item["id"] == trip_id), None)

    def picked_up(self):
        self.available()
        trip = self.create()
        self.action(trip["id"], "confirm-price")
        wait(lambda: self.invitation(trip["id"]))
        for action in ("driver-accept", "enroute", "pickup"):
            self.action(trip["id"], action, True)
        return trip

    def payment_command(self, trip):
        return dict(tripId=trip["id"], correlationId=trip["id"], amount=trip["fareEstimate"], currency="VND",
                    idempotencyKey="trip-" + trip["id"].replace("-", "") + "-payment-v1",
                    payerAccount="rider-" + self.rider_id, payeeAccount="driver-" + self.driver_id,
                    occurredAtUtc=dt.datetime.now(dt.timezone.utc).isoformat())

    def payment_count(self, trip_id):
        return int(self.sql("payment-postgres", f"SELECT count(*) FROM payments WHERE trip_id='{trip_id}';"))

    def settled(self, trip_id):
        wait(lambda: self.status(trip_id), lambda value: value == "Paid")
        check(self.payment_count(trip_id) == 1, "Duplicate payment")
        count = self.sql("payment-postgres", f"SELECT count(*) FROM ledger_entries l JOIN payments p ON p.id=l.payment_id WHERE p.trip_id='{trip_id}';")
        check(count == "2", f"Expected two ledger entries: {count}")

    def reconnect(self, trip_id):
        base = self.gateway + "/api/hubs/trips"
        token = http("POST", base + "/negotiate?negotiateVersion=1", {}, self.driver)["connectionToken"]
        url = base + "?id=" + urllib.parse.quote(token)
        def raw(method, data=None):
            request = urllib.request.Request(url, data=data, method=method,
                                             headers={"Authorization": "Bearer " + self.driver})
            with urllib.request.urlopen(request, timeout=15) as response:
                return response.read().decode()
        try:
            raw("GET")
            raw("POST", b'{"protocol":"json","version":1}\x1e')
            received = wait(lambda: raw("GET"), lambda value: trip_id in value)
            check("driverInvitation" in received, "Reconnect did not replay invitation")
        finally:
            raw("DELETE")


def scenarios(stack, record):
    wait(lambda: http("GET", stack.gateway + "/health"))
    for service in ("trip-service", "matching-service", "payment-service"):
        wait(lambda service=service: http("GET", stack.url(service) + "/health/ready"))
    queues = ["swiftride-matching-find-driver-for-trip", "swiftride-matching-release-driver", FOUND,
              "swiftride-trip-no-driver-found-for-trip", PAYMENT, "swiftride-trip-payment-succeeded",
              "swiftride-trip-payment-failed", "swiftride-trip-payment-fault"]
    for name in queues:
        queue = wait(lambda name=name: stack.queue(name), lambda value: value["consumers"] == 1)
        check(queue["durable"] and not queue["auto_delete"], f"Non-durable business queue: {name}")
    bindings = stack.rabbit("GET", "bindings/task5")
    for event in ("driver-found-for-trip", "no-driver-found-for-trip", "payment-succeeded", "payment-failed", "payment-fault"):
        check(any(binding["source"] == f"swiftride-{event}-v1" and binding["destination"] == f"swiftride-trip-{event}"
                  for binding in bindings), f"Missing binding: {event}")
    record("topology", "Eight durable business queues, registered consumers and event bindings")
    def login(email):
        token = http("POST", stack.gateway + "/auth/login", dict(email=email, password=stack.env["DEV_ACCOUNT_PASSWORD"]))["accessToken"]
        user = http("GET", stack.gateway + "/auth/me", token=token)
        return token, user["id"]
    stack.rider, stack.rider_id = login("rider.demo@example.com")
    stack.driver, stack.driver_id = login("driver.demo@example.com")
    record("clean-start", "All services ready, migrations and Mongo seed completed")

    key = "task5-" + uuid.uuid4().hex
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        created = list(executor.map(lambda _: stack.create(key), range(2)))
    check(created[0]["id"] == created[1]["id"], "Concurrent create duplicated trip")
    record("duplicate-create", "Concurrent same-key creates reused one trip")

    trip = stack.picked_up()
    stack.mongo(f'db.drivers.updateOne({{DriverId:"{stack.driver_id}"}},{{$set:{{ReservationExpiresAtUtc:new Date(0)}}}})')
    http("POST", stack.gateway + "/api/drivers/location", dict(latitude=10.77, longitude=106.69, isAvailable=True), stack.driver)
    time.sleep(11)
    check(stack.mongo(f'print(db.drivers.countDocuments({{DriverId:"{stack.driver_id}",IsAvailable:false}}))') == "1",
          "Active driver unlocked after offer expiry or heartbeat")
    stack.action(trip["id"], "dropoff", True)
    stack.settled(trip["id"])
    record("end-to-end", trip["id"] + ": Requested through Paid; active reservation survived expiry/heartbeat")

    command = stack.payment_command(trip)
    original = stack.send(PAYMENT, "Payments:ProcessPaymentV1", command)
    stack.send(PAYMENT, "Payments:ProcessPaymentV1", command, original)
    another = stack.send(PAYMENT, "Payments:ProcessPaymentV1", command)
    for message_id in (original, another):
        wait(lambda message_id=message_id: stack.sql("payment-postgres", f'''SELECT count(*) FROM "InboxState" WHERE "MessageId"='{message_id}' AND "Consumed" IS NOT NULL;'''), lambda value: value == "1")
    stack.settled(trip["id"])
    record("duplicate-payment", "Same/new MessageId and same business key; one payment/two ledger rows")

    for action in ("cancel", "driver-reject", "timeout"):
        stack.available()
        pending = stack.create()
        stack.action(pending["id"], "confirm-price")
        wait(lambda: stack.invitation(pending["id"]))
        stack.reconnect(pending["id"])
        if action != "timeout":
            stack.action(pending["id"], action, action == "driver-reject")
        target = "Cancelled" if action == "cancel" else "Failed"
        wait(lambda: stack.status(pending["id"]), lambda value: value == target)
        late = dict(tripId=pending["id"], correlationId=pending["id"], attempt=1,
                    driverId=stack.driver_id, occurredAtUtc=dt.datetime.now(dt.timezone.utc).isoformat())
        late_id = stack.send(FOUND, "Matching:DriverFoundForTripV1", late)
        wait(lambda: stack.sql("trip-postgres", f'''SELECT count(*) FROM "InboxState" WHERE "MessageId"='{late_id}' AND "Consumed" IS NOT NULL;'''), lambda value: value == "1")
        check(stack.status(pending["id"]) == target, "Late matching result changed terminal state")
        stack.available()
        record(action + "-late-event", pending["id"] + ": " + target + "; driver released, reconnect replay verified")

    pending = stack.create()
    stack.compose("stop", "rabbitmq")
    stack.action(pending["id"], "confirm-price")
    check(stack.status(pending["id"]) == "PriceConfirmed", "Confirm DB commit lost")
    check(int(stack.sql("trip-postgres", 'SELECT count(*) FROM "OutboxMessage";')) > 0, "Matching outbox missing")
    stack.compose("kill", "trip-service")
    stack.compose("start", "rabbitmq")
    stack.compose("start", "trip-service")
    wait(lambda: stack.invitation(pending["id"]))
    stack.reconnect(pending["id"])
    stack.action(pending["id"], "cancel")
    record("confirm-disconnect-restart", "Committed matching command survived broker/service restart")

    stack.available()
    pending = stack.create()
    stack.compose("stop", "matching-service")
    stack.action(pending["id"], "confirm-price")
    wait(lambda: stack.queue("swiftride-matching-find-driver-for-trip"), lambda value: value.get("messages_ready", 0) > 0)
    try:
        stack.compose("exec", "-T", "rabbitmq", "rabbitmqctl", "set_vm_memory_high_watermark", "absolute", "1")
        stack.compose("start", "matching-service")
        wait(lambda: stack.mongo(f'print(db.drivers.countDocuments({{ReservationTripId:UUID("{pending["id"]}")}}))'), lambda value: value == "1")
        stack.compose("kill", "matching-service")
    finally:
        stack.compose("exec", "-T", "rabbitmq", "rabbitmqctl", "set_vm_memory_high_watermark", "0.6")
        stack.compose("start", "matching-service")
    wait(lambda: stack.invitation(pending["id"]))
    stack.reconnect(pending["id"])
    check(stack.mongo(f'print(db.drivers.countDocuments({{ReservationTripId:UUID("{pending["id"]}")}}))') == "1", "Reservation duplicated on replay")
    stack.action(pending["id"], "cancel")
    record("reservation-result-disconnect", "Reservation reused after consumer kill; offline invitation recovered")

    trip = stack.picked_up()
    stack.compose("stop", "rabbitmq")
    stack.action(trip["id"], "dropoff", True)
    check(stack.status(trip["id"]) == "PaymentPending", "Dropoff commit missing")
    stack.compose("kill", "trip-service")
    stack.compose("start", "rabbitmq")
    stack.compose("start", "trip-service")
    stack.settled(trip["id"])
    record("dropoff-disconnect-restart", "Committed payment command recovered once")

    trip = stack.picked_up()
    stack.compose("stop", "payment-service")
    stack.action(trip["id"], "dropoff", True)
    wait(lambda: stack.queue(PAYMENT), lambda value: value.get("messages_ready", 0) > 0)
    try:
        stack.compose("exec", "-T", "rabbitmq", "rabbitmqctl", "set_vm_memory_high_watermark", "absolute", "1")
        stack.compose("start", "payment-service")
        wait(lambda: stack.payment_count(trip["id"]))
        check(stack.status(trip["id"]) == "PaymentPending", "Result delivered during publish block")
        stack.compose("kill", "payment-service")
    finally:
        stack.compose("exec", "-T", "rabbitmq", "rabbitmqctl", "set_vm_memory_high_watermark", "0.6")
        stack.compose("start", "payment-service")
    stack.settled(trip["id"])
    record("payment-commit-result-disconnect", "Payment/result survived consumer kill; no duplicate ledger")

    trip = stack.picked_up()
    stack.compose("stop", "payment-postgres")
    stack.action(trip["id"], "dropoff", True)
    wait(lambda: stack.queue(PAYMENT), lambda value: value.get("messages", 0) > 0)
    stack.compose("start", "payment-postgres")
    stack.settled(trip["id"])
    stack.compose("stop", "trip-postgres")
    time.sleep(12)
    stack.compose("start", "trip-postgres")
    wait(lambda: http("GET", stack.url("trip-service") + "/health/ready"))
    check(stack.status(trip["id"]) == "Paid", "Postgres restart lost state")
    record("postgres-recovery", "Both PostgreSQL services restarted; timeout worker survived")

    stack.available()
    pending = stack.create()
    stack.compose("stop", "matching-mongodb")
    stack.action(pending["id"], "confirm-price")
    wait(lambda: stack.queue("swiftride-matching-find-driver-for-trip"), lambda value: value.get("messages", 0) > 0)
    stack.compose("start", "matching-mongodb")
    wait(lambda: stack.invitation(pending["id"]))
    stack.action(pending["id"], "cancel")
    record("mongo-recovery", "Mongo connection and queued matching command recovered")

    trip = stack.picked_up()
    command = stack.payment_command(trip)
    stack.sql("payment-postgres", f"""INSERT INTO payments
        (id,trip_id,amount,status,idempotency_key,payer_account,payee_account,is_trip_synced,version,created_at,updated_at,failure_code,failure_reason)
        VALUES ('{uuid.uuid4()}','{trip['id']}',{command['amount']},'Failed','{command['idempotencyKey']}',
        '{command['payerAccount']}','{command['payeeAccount']}',false,0,now(),now(),'PAYMENT_DECLINED','Task5 declined');""")
    stack.action(trip["id"], "dropoff", True)
    wait(lambda: stack.status(trip["id"]), lambda value: value == "Failed")
    stack.send(PAYMENT, "Payments:ProcessPaymentV1", command)
    wait(lambda: stack.queue(PAYMENT), lambda value: value.get("messages", 0) == 0)
    check(stack.payment_count(trip["id"]) == 1, "Duplicate failed payment")
    check(stack.sql("payment-postgres", f"SELECT count(*) FROM ledger_entries l JOIN payments p ON p.id=l.payment_id WHERE p.trip_id='{trip['id']}';") == "0", "Decline produced ledger")
    record("payment-declined", "Persisted decline propagated through real consumer/RabbitMQ/Trip; no ledger")

    trip = stack.picked_up()
    stack.sql("trip-postgres", f'''UPDATE "Trips" SET "Status"='PaymentPending' WHERE "Id"='{trip['id']}';''')
    stack.sql("payment-postgres", f"""CREATE SEQUENCE task5_attempts;
        CREATE FUNCTION task5_fail_result() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN IF NEW."CorrelationId" = '{trip['id']}'::uuid THEN
            PERFORM nextval('task5_attempts'); RAISE EXCEPTION 'Task5 injected failure'; END IF; RETURN NEW; END $$;
        CREATE TRIGGER task5_fail_result BEFORE INSERT ON "OutboxMessage" FOR EACH ROW EXECUTE FUNCTION task5_fail_result();""")
    try:
        original = stack.send(PAYMENT, "Payments:ProcessPaymentV1", stack.payment_command(trip))
        wait(lambda: stack.queue(PAYMENT + "_error"), lambda value: value.get("messages", 0) > 0)
        wait(lambda: stack.sql("trip-postgres", f'''SELECT coalesce("PaymentFailureReason", '') FROM "Trips" WHERE "Id"='{trip['id']}';'''))
        check(stack.status(trip["id"]) == "PaymentPending", "Technical failure classified as decline")
        check(stack.payment_count(trip["id"]) == 0, "Faulted consumer committed payment")
        check(stack.sql("payment-postgres", "SELECT last_value FROM task5_attempts;") == "4", "Expected initial attempt and three retries")
        failed = stack.rabbit("POST", "queues/task5/" + PAYMENT + "_error/get",
                             dict(count=1, ackmode="ack_requeue_true", encoding="auto", truncate=1000000))[0]
        check(json.loads(failed["payload"])["messageId"] == original, "Original MessageId lost")
    finally:
        stack.sql("payment-postgres", 'DROP TRIGGER task5_fail_result ON "OutboxMessage"; DROP FUNCTION task5_fail_result(); DROP SEQUENCE task5_attempts;')
    stack.publish(PAYMENT, failed["payload"], failed["properties"])
    stack.settled(trip["id"])
    record("error-fault-replay", "Rollback verified; original retained in _error; same-envelope replay settled once")

    stack.compose("restart", "trip-service", "matching-service", "payment-service", "api-gateway")
    wait(lambda: http("GET", stack.gateway + "/api/trips/" + trip["id"], token=stack.rider))
    stack.settled(trip["id"])
    record("existing-volume-restart", "Startup migrations reran without deleting volumes; rows/login preserved")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/task5/system-current")
    parser.add_argument("--keep-running", action="store_true")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--dotnet")
    parser.add_argument("--artifacts-path")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    stack = Stack("swiftride-task5-" + uuid.uuid4().hex[:10])
    report = dict(project=stack.project, base_commit=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
                  working_tree="modified", started=dt.datetime.now(dt.timezone.utc).isoformat(), checks=[], status="running")
    def save():
        (args.output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    def record(name, detail):
        report["checks"].append(dict(name=name, status="passed", detail=detail))
        save()
        print("PASS " + name, flush=True)
    save()
    started = False
    try:
        stack.run(["docker", "info", "--format", "{{.ServerVersion}}"], timeout=30)
        stack.compose("config", "--quiet")
        started = True
        stack.compose("up", "-d", *([] if args.no_build else ["--build"]), timeout=1800)
        if args.dotnet:
            env = dict(stack.env)
            for name, service in (("TRIP", "trip-postgres"), ("PAYMENT", "payment-postgres")):
                env[f"SWIFTRIDE_{name}_TEST_DB"] = (f"Host=127.0.0.1;Port={stack.port(service,5432)};"
                    f"Database={stack.env[name+'_DB_NAME']};Username=swiftride;Password={stack.env[name+'_DB_PASSWORD']}")
            env["SWIFTRIDE_MATCHING_TEST_DB"] = f"mongodb://127.0.0.1:{stack.port('matching-mongodb',27017)}/swiftride_matching_test"
            for project in ("TripService", "PaymentService", "MatchingService"):
                command = [args.dotnet, "test", "--project", f"tests/{project}.Tests/{project}.Tests.csproj", "-c", "Release", "--no-build"]
                if args.artifacts_path:
                    command += ["-p:ArtifactsPath=" + args.artifacts_path]
                command += ["--", "--filter-trait", "Category=Integration"]
                output = stack.run(command, timeout=300, env=env)
                print(output, flush=True)
                record(project + "-integration", "Real database integration tests passed")
        scenarios(stack, record)
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
        if started:
            try:
                logs = stack.compose("logs", "--no-color", "--tail", "80")
                for secret in stack.secrets:
                    logs = logs.replace(secret, "[redacted]")
                (args.output / "services.log").write_text(logs, encoding="utf-8")
            except (RuntimeError, subprocess.TimeoutExpired):
                pass
        raise
    finally:
        report["finished"] = dt.datetime.now(dt.timezone.utc).isoformat()
        save()
        if started and not args.keep_running:
            try:
                stack.compose("down", timeout=90)
            except (RuntimeError, subprocess.TimeoutExpired):
                print("Cleanup unavailable; retained project: " + stack.project, flush=True)
        stack.env_file.unlink(missing_ok=True)
        print("Report: " + str(args.output / "report.json"), flush=True)


if __name__ == "__main__":
    main()
