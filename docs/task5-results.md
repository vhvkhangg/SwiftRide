# Task 5 - Local Verification Results

Base commit: `bb98d99861b8a4106cbdd6ee0b331a00771d7e2c` (8 November 2026). Results apply to the current uncommitted Task 5 changes, not to the base commit alone. This report supersedes the 31 October result.

Full-stack run: **8 November 2026, Asia/Bangkok**. Isolated project: `swiftride-task5-7a39daf35a`. Exact UTC timestamps are recorded in `artifacts/task5/system-current-final/report.json`.

## Verification

| Check | Result |
| --- | --- |
| Whole-solution dependency restore/audit; NU1901-NU1904 treated as errors | PASS |
| Release build of SwiftRide.sln | PASS; 0 warnings, 0 errors |
| Unit/local HTTP tests | PASS; 153 passed, 0 failed, 0 skipped |
| Python system-helper checks | PASS; 3 tests |
| PostgreSQL Trip integration | PASS; 4 tests, 0 skipped |
| PostgreSQL Payment integration | PASS; 12 tests, 0 skipped |
| MongoDB reservation integration | PASS; 2 tests, 0 skipped |
| System report | PASS; 24 checks: 3 database-test groups and 21 topology/E2E/recovery scenarios |

SDK: Windows .NET 10.0.403; production Dockerfiles use SDK 10.0.401. Build and test output is under `E:\SwiftRide`; system evidence is under `artifacts/task5/system-current-final/`. Runbook and portable commands: [messaging.md](messaging.md).

## Work Items And Evidence

| Item | Delivered and verified |
| --- | --- |
| Reservation lifecycle | Durable owner-scoped ReleaseDriverV1 on reject/timeout/cancel/dropoff; reconciliation against authoritative Trip state; expiry/heartbeat cannot unlock an active driver |
| Candidate contention | Lost reservation race tries another candidate; exhaustion of bounded contention retries throws instead of falsely reporting no drivers; unit tests pass |
| Duplicate matching attempt | Mongo unique DriverId and partial unique TripId/Attempt indexes; real concurrent tests verify one owner and one driver. Fixed owner lookup hiding expired reservations; regression verifies restored owner, wrong attempt rejection and no owner after release |
| Executable system suite | Production Dockerfiles, actual consumer registrations/topology and actual RabbitMQ/PostgreSQL/MongoDB; no repository replacement in the system path |
| Four commit/delivery gaps | Confirm/matching, reservation/result/invitation, dropoff/payment and saved-payment/result all recover across broker disconnect or service kill |
| Offline notifications | SignalR reconnect replays active PostgreSQL invitations; Gateway forwards DELETE for long-poll cleanup |
| Real rollback and upgrade | Trip failure injected after SaveChanges rolls back state/outbox; isolated old Trip/Payment schemas containing rows migrate twice without data loss; nullable Trip idempotency columns and unique index added by a new EF migration |
| CI | Whole-solution unit/build job and separate real-stack integration/recovery job; JSON report uploaded even on failure; not pushed or executed on GitHub in this run |
| Logs and dependencies | Scoped TripId/CorrelationId/MessageId/Attempt/key/retry identifiers; logging test excludes account/payload; unused SQLite references removed without suppressing audit warnings |
| Documentation | Active topology/flow, startup, migration/backup, seed, queue inspection and safe same-envelope replay documented; historical NV6 reports explicitly marked historical |

## System Scenarios

All scenarios below have `status=passed` in the final JSON report:

- `topology`: ten durable business queues, one registered consumer each, expected event bindings; command contracts have no publish subscribers.
- `broker-durable-message`: persistent queued message survives broker restart; all consumers reconnect.
- `skipped-contract`: unsupported contract retains the original MessageId in `_skipped`.
- `invalid-command`: invalid matching command enters `_error` as ArgumentException with no retry or Trip creation.
- `publish-fanout`: real PaymentSucceeded reaches Trip and two independent subscriber queues.
- `clean-start`: fresh stack reaches readiness, migrations and Mongo seed complete.
- `duplicate-create`: simultaneous same-key creates return one Trip.
- `end-to-end`: Requested through Paid; active reservation remains locked despite forced expiry and heartbeat.
- `duplicate-payment`: duplicate MessageId and new MessageId/same business key produce one payment and two ledger rows.
- `cancel-late-event`, `driver-reject-late-event`, `timeout-late-event`: expected terminal state remains unchanged; driver is released; reconnect notification replay works.
- `confirm-disconnect-restart`: committed matching command survives broker/Trip restart.
- `reservation-result-disconnect`: Mongo reservation survives Matching kill during blocked publish; retry reuses ownership and invitation is recovered.
- `dropoff-disconnect-restart`: committed payment command survives broker/Trip restart.
- `payment-commit-result-disconnect`: saved payment/result survives Payment kill while broker blocks publishing; no duplicate ledger.
- `postgres-recovery`: both PostgreSQL services restart without removing volumes; timeout worker remains alive.
- `mongo-recovery`: Matching reconnects to Mongo and completes the queued command.
- `payment-declined`: persisted decline is delivered through the real Payment consumer, broker and Trip; no ledger is created.
- `error-fault-replay`: trigger failure rolls payment/outbox back, initial attempt plus three retries observed, original MessageId retained in `_error`, technical Fault keeps Trip pending, repaired same-envelope replay settles once.
- `existing-volume-restart`: service restart reruns startup migrations while preserving existing rows and authentication.

## Limits And Cleanup

Payment is a ledger simulation, not a real PSP/bank integration. The system decline case seeds a persisted failed payment to exercise actual failure-event delivery; decision-service decline behavior is separately covered by application/database tests. No claim of a real bank decline is made.

Base Compose still uses development-only Auth EnsureCreated for legacy development volumes. Task 1 now provides Auth EF migrations and a migration-based acceptance overlay. Local results do not replace business acceptance of Tasks 1-4 or an actual GitHub CI execution.

The first current run exposed a real expired-owner lookup bug in Mongo; it is fixed without weakening the past-expiry test. New reliability scenarios also exposed helper assumptions about RabbitMQ creation/deletion HTTP statuses and temporarily missing consumer statistics after restart; both are fixed and covered by reruns. Failed attempts remain in separate artifact directories.

The runner stops its isolated containers/network on exit and retains test volumes. Development volumes and unrelated Docker projects are not touched. Random test credentials are redacted from failure reports; the temporary empty Compose env file is removed. No commit or push is part of this verification.
