# Task 5 - Local Verification Results

Base commit: `10733f5fc08dc26ceb06903258ff9e80f7da6c99` (31 October 2026). Results apply to the uncommitted Task 5 changes in this workspace, not to the original HEAD alone.

Full-stack run: **31 October 2026, 23:37:51-23:44:48, Asia/Saigon**. Isolated project: `swiftride-task5-e84cf73705`.

## Verification

| Check | Result |
| --- | --- |
| Whole-solution dependency restore/audit; NU1901-NU1904 treated as errors | PASS |
| Release build of SwiftRide.sln | PASS; 0 warnings, 0 errors |
| Unit/local HTTP tests | PASS; 152 passed, 0 failed, 0 skipped |
| Python system-helper checks | PASS; 2 tests |
| PostgreSQL Trip integration | PASS; 4 tests, 0 skipped |
| PostgreSQL Payment integration | PASS; 12 tests, 0 skipped |
| MongoDB reservation integration | PASS; 2 tests, 0 skipped |
| System report | PASS; 20 checks: 3 database-test groups and 17 topology/E2E/recovery scenarios |

SDK: Windows .NET 10.0.403; production Dockerfiles use SDK 10.0.401. SDK, dependency cache, .NET output and primary reports are under `E:\SwiftRide-task5-10733f5` because drive D lacked build space. Small copies of build/unit logs and the JSON system report are under `artifacts/task5/system-current/`. Runbook and portable commands: [messaging.md](messaging.md).

## Work Items And Evidence

| Item | Delivered and verified |
| --- | --- |
| Reservation lifecycle | Durable owner-scoped ReleaseDriverV1 on reject/timeout/cancel/dropoff; reconciliation against authoritative Trip state; expiry/heartbeat cannot unlock an active driver |
| Candidate contention | Lost reservation race tries another candidate; exhaustion of bounded contention retries throws instead of falsely reporting no drivers; unit tests pass |
| Duplicate matching attempt | Mongo unique DriverId and partial unique TripId/Attempt indexes; real concurrent tests verify one owner and one driver; repeated reads survive repository reconstruction |
| Executable system suite | Production Dockerfiles, actual consumer registrations/topology and actual RabbitMQ/PostgreSQL/MongoDB; no repository replacement in the system path |
| Four commit/delivery gaps | Confirm/matching, reservation/result/invitation, dropoff/payment and saved-payment/result all recover across broker disconnect or service kill |
| Offline notifications | SignalR reconnect replays active PostgreSQL invitations; Gateway forwards DELETE for long-poll cleanup |
| Real rollback and upgrade | Trip failure injected after SaveChanges rolls back state/outbox; isolated old Trip/Payment schemas containing rows migrate twice without data loss; nullable Trip idempotency columns and unique index added by a new EF migration |
| CI | Whole-solution unit/build job and separate real-stack integration/recovery job; JSON report uploaded even on failure; not pushed or executed on GitHub in this run |
| Logs and dependencies | Scoped TripId/CorrelationId/MessageId/Attempt/key/retry identifiers; logging test excludes account/payload; unused SQLite references removed without suppressing audit warnings |
| Documentation | Active topology/flow, startup, migration/backup, seed, queue inspection and safe same-envelope replay documented; historical NV6 reports explicitly marked historical |

## System Scenarios

All entries below have `status=passed` in the JSON report:

- `topology`: eight durable business queues, one registered consumer each, expected event bindings.
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

Gateway Auth still uses development-only EnsureCreated; production Auth schema evolution is not supplied by this patch. Local results do not replace business acceptance of Tasks 1-4 or an actual GitHub CI execution. `_skipped` retention is provided by MassTransit configuration; this run does not add a dedicated wrong-contract system scenario.

The first system attempt exposed a test-helper bug parsing plain-text readiness as JSON; it was fixed and the entire suite rerun successfully. That failed run remains separate from the final PASS report.

Docker initially failed on stale runtime sockets. Only socket directories were renamed to preserved backups before restarting Desktop; Docker was not reset. Final test containers/network were stopped, test volumes retained, development volumes untouched. Random test credentials were not written to reports and the temporary empty Compose env file was removed.
