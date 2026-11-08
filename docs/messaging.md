# Messaging And Recovery Runbook

Run commands from the repository root. Requires .NET 10 SDK, Python 3.10+ and Docker Compose 2.24.4+ (`!override`). Local Compose credentials are public development values, not production secrets.

## Active Flow And Queues

Trip creation obtains an internal HTTP quote and persists `Requested`; it does not start matching. Confirm-price atomically saves `PriceConfirmed` and stages `FindDriverForTripV1` in the PostgreSQL bus outbox. Trip owns invitations and state transitions. Dropoff atomically saves `PaymentPending`, stages `ProcessPaymentV1` and releases the driver reservation. A publish is not evidence of driver acceptance or settled payment.

| Queue | Consumer / delivery |
| --- | --- |
| `swiftride-matching-find-driver-for-trip` | Matching, command via Send |
| `swiftride-matching-release-driver` | Matching, command via Send |
| `swiftride-trip-driver-found-for-trip` | Trip, DriverFoundForTripV1 |
| `swiftride-trip-no-driver-found-for-trip` | Trip, NoDriverFoundForTripV1 |
| `swiftride-payment-process-payment` | Payment, command via Send |
| `swiftride-trip-payment-succeeded` | Trip, PaymentSucceededV1 |
| `swiftride-trip-payment-failed` | Trip, PaymentFailedV1 |
| `swiftride-trip-payment-fault` | Trip, Fault of ProcessPaymentV1 |

Messaging.Setup creates durable topology without consuming. Services register the eight business consumers. Legacy TripRequested/DriverMatched/DriverMatchFailed contracts are not the active workflow; existing legacy queues are not automatically deleted.

Trip and Payment use PostgreSQL inbox/outbox transactions. Payment has a unique business key and unique settled Trip constraint; Trip creation has a unique nullable idempotency key. The full create payload must remain unchanged when retrying a key. Mongo has unique DriverId and unique non-null `(ReservationTripId, ReservationAttempt)` indexes. Index creation fails rather than deleting conflicting legacy data; inspect duplicates and reconcile ownership before upgrading.

Reservations are not unlocked by GPS heartbeats or wall-clock expiry. Repeated matching returns the existing owner. Reject/timeout/cancel/dropoff send a durable owner-scoped release command. The recovery worker checks overdue reservations against authoritative Trip state every ten seconds: active trips retain ownership; obsolete attempts and terminal/missing trips release it. A failed Trip HTTP lookup never unlocks a driver. Startup index creation also applies when upgrading an existing Mongo volume; database name comes from the MatchDb URI / `MATCH_DB_NAME`.

## Start, Migrate And Seed

```powershell
docker compose up -d --build
docker compose ps -a
docker compose logs --tail 100 messaging-setup trip-service matching-service payment-service
```

Compose enables startup EF migrations for Trip/Payment, seeds Mongo pricing and enables development Auth/demo accounts. Gateway Auth still uses development-only EnsureCreated; it does not provide a production Auth schema-upgrade migration. Configure secrets from a protected environment before non-local use. Service `/health/ready` checks bus readiness, not full business correctness.

Before upgrading existing volumes, back up both PostgreSQL databases and Mongo using the configured database/user names. Example for default local Trip DB:

```powershell
docker compose exec -T trip-postgres pg_dump -U swiftride -d swiftride_trip -Fc -f /tmp/task5-trip.dump
if ($LASTEXITCODE -ne 0) { throw 'Backup failed; stop upgrade.' }
docker compose exec -T trip-postgres pg_restore --list /tmp/task5-trip.dump
if ($LASTEXITCODE -ne 0) { throw 'Backup unreadable; stop upgrade.' }
New-Item -ItemType Directory -Force artifacts | Out-Null
docker compose cp trip-postgres:/tmp/task5-trip.dump artifacts/task5-trip.dump
if ($LASTEXITCODE -ne 0) { throw 'Backup copy failed; stop upgrade.' }
```

Repeat for Payment and take a Mongo backup; verify restore in an isolated environment. Then deploy the new images with `docker compose up -d --build`. Never use `down -v`, reset Docker, or remove development volumes to make migrations pass. Migration `TripIdempotency` adds nullable columns and a unique key without rewriting existing trips. If a manually modified database already has those columns or duplicate non-null keys, stop and reconcile its schema/history from backups; do not delete data or blindly mark the migration applied.

## Observe And Replay

```powershell
docker compose exec -T rabbitmq rabbitmqctl list_queues -p swiftride name messages_ready messages_unacknowledged consumers durable
docker compose exec -T rabbitmq rabbitmqctl list_bindings -p swiftride source_name destination_name routing_key
docker compose logs --tail 200 trip-service matching-service payment-service
```

Consumer JSON log scopes include TripId, CorrelationId, MessageId, Attempt, IdempotencyKey and RetryAttempt; non-applicable fields may be null. Publisher logs identify staged matching/payment commands. Do not enable EF SensitiveDataLogging, log payloads/accounts/tokens/connection strings, or enable request URL logging with SignalR `access_token` query strings.

Retries use 1/3/5 second intervals. Exhausted messages remain in `<queue>_error`; unhandled contracts remain in `<queue>_skipped`. A technical payment Fault records a safe reason while keeping Trip `PaymentPending`; only a validated financial decline marks it `Failed`. Repair the cause before replay. Do not manufacture PaymentFailedV1 fields to represent a technical fault.

1. Identify TripId, MessageId and business key; inspect error headers, original message and database state. Stop on contradictory terminal state, wrong amount or wrong key.
2. Repair the dependency/schema/consumer. Read the original using `ack_requeue_true`; preserve its full envelope, MessageId, properties, headers and content type.
3. Publish the original command, not its Fault, to its original queue. Keep the error copy until verifying the business result.
4. Verify one payment and two ledger rows for success, or one failed payment and no ledger for decline. Only then acknowledge the exact retained error delivery. Never purge the whole queue or blindly replay every error.

RabbitMQ Management UI supports inspection; its API permits read/requeue and publish. Use HTTPS outside loopback, secure credentials, reject truncated payloads, and verify `routed=true`. Replaying an already-consumed MessageId can be ignored by the inbox; never change IDs/keys to bypass a business-state conflict.

PowerShell 7 example for one inspected payment command, local management endpoint only. Read/requeue leaves the original error copy intact:

```powershell
$credential = Get-Credential
$management = 'http://127.0.0.1:15672/api'
$vhost = [uri]::EscapeDataString('swiftride')
$queue = 'swiftride-payment-process-payment'
$expectedId = [guid](Read-Host 'Inspected original MessageId')
$tripId = [guid](Read-Host 'Inspected TripId')
$get = @{ count=1; ackmode='ack_requeue_true'; encoding='auto'; truncate=1000000 } | ConvertTo-Json
$items = @(Invoke-RestMethod "$management/queues/$vhost/${queue}_error/get" -Method Post -Body $get -ContentType application/json -Authentication Basic -Credential $credential -AllowUnencryptedAuthentication)
if ($items.Count -ne 1) { throw 'Expected one error message; stop.' }
$item = $items[0]
if ($item.payload_bytes -gt 1000000 -or $item.payload_encoding -ne 'string') { throw 'Truncated/unsupported payload; stop.' }
$envelope = $item.payload | ConvertFrom-Json
if ([guid]$envelope.messageId -ne $expectedId -or [guid]$envelope.message.tripId -ne $tripId -or $envelope.message.idempotencyKey -ne "trip-$($tripId.ToString('N'))-payment-v1") { throw 'Wrong ID/business key; stop.' }
if ($envelope.messageType -notcontains 'urn:message:SwiftRide.Contracts.Payments:ProcessPaymentV1') { throw 'Wrong contract; stop.' }
$body = @{ routing_key=$queue; payload=$item.payload; payload_encoding='string'; properties=$item.properties } | ConvertTo-Json -Depth 100
$result = Invoke-RestMethod "$management/exchanges/$vhost/amq.default/publish" -Method Post -Body $body -ContentType application/json -Authentication Basic -Credential $credential -AllowUnencryptedAuthentication
if (-not $result.routed) { throw 'Not routed; original remains in error queue.' }
```

Outside loopback, replace the endpoint with HTTPS and remove `-AllowUnencryptedAuthentication`. The script deliberately does not acknowledge/delete the retained error; verify settlement and use a consumer/tool with the exact delivery tag before removing that copy.

## Reproducible Verification

```powershell
dotnet restore SwiftRide.sln -p:NuGetAuditMode=all -warnaserror:NU1901,NU1902,NU1903,NU1904
dotnet build SwiftRide.sln -c Release --no-restore
dotnet test --solution SwiftRide.sln -c Release --no-build -- --filter-not-trait Category=Integration
python -m unittest discover -s tests/System -p 'test_*.py'
python tests/System/messaging.py --dotnet dotnet
```

The system command uses a fresh `swiftride-task5-*` project, unique random credentials, separate volumes/vhost/databases, random loopback ports, and ignores the development `.env`. It invokes PostgreSQL/Mongo integration tests with isolated `_test` connections; missing dependencies fail in CI rather than being silently skipped. PostgreSQL tests migrate old schemas containing data, and rollback tests fail after a real SaveChanges. Mongo tests cover concurrent ownership, duplicate attempts and heartbeat safety.

System scenarios cover Paid, persisted decline delivery, reject/timeout/cancel, duplicate create/payment, late results, reconnect invitations, broker/database/service outages, commit/result gaps and error/replay. The decline scenario seeds a persisted failed payment, then uses the real consumer/broker/Trip path; it is not a real bank/PSP decline. Payment decision behavior is separately covered by application/database tests.

Report: `artifacts/task5/system-current/report.json`. Failure additionally saves redacted service logs; exit status is nonzero. Containers/network stop by default, test volumes remain. `--keep-running` retains the test stack; credentials are intentionally not saved, so normally rerun with a fresh project. `--no-build` is only for unchanged images. Low disk: use `--output E:\SwiftRide-task5\report` and redirect .NET output with `--artifacts-path`; do not move or delete development database volumes.

CI in `.github/workflows/ci.yml` separates whole-solution unit/build from real-stack integration/recovery and uploads its JSON report even on failure. Local evidence is in [task5-results.md](task5-results.md); local PASS is not proof that GitHub CI has run.

For this workstation, the portable SDK and heavy outputs were moved to E. Reproduce without filling D:

```powershell
$root = 'E:\SwiftRide-task5-10733f5'
$dotnet = "$root\sdk-complete\dotnet.exe"
$env:DOTNET_CLI_HOME = "$root\cli"
$env:NUGET_PACKAGES = "$root\packages"
& $dotnet restore SwiftRide.sln -p:ArtifactsPath="$root\build" -p:NuGetAuditMode=all -warnaserror:NU1901,NU1902,NU1903,NU1904
& $dotnet build SwiftRide.sln -c Release --no-restore --artifacts-path "$root\build"
& $dotnet test --solution SwiftRide.sln -c Release --no-build -p:ArtifactsPath="$root\build" -- --filter-not-trait Category=Integration
python tests/System/messaging.py --output "$root\reports\repeat" --dotnet $dotnet --artifacts-path "$root\build"
```
