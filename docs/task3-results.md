# Task 3 - Matching and Pricing Verification

## Delivered

- Driver IDs validated as non-empty GUIDs at location-write and matching boundaries; no fallback GUID generated.
- Matching only considers valid, online, recently updated, available drivers inside requested radius.
- Mongo reservations use atomic driver updates and unique `(TripId, Attempt)` ownership; stale attempts cannot release newer reservations.
- Reservation and `DriverFoundForTripV1` are committed in one Mongo replica-set transaction with a durable Outbox entry.
- `NoDriverFoundForTripV1` is persisted before the matching command is acknowledged; the dispatcher retries with leases and backoff.
- Matching startup rejects a standalone MongoDB configuration because it cannot provide the transaction required by the Outbox.
- Reservation release carries `TripId`, `DriverId` and `Attempt`.
- Offline drivers remain unavailable after reservation release.
- GPS payloads reject non-finite or out-of-range coordinates.
- Pricing validates distance, duration and supported ride types; promo codes normalize server-side and surge rules validate.

## Verification

- Solution build: succeeded, 0 errors. Existing xUnit analyzer warnings remain in Trip Service tests.
- Matching Service tests: 46 passed, 0 failed, 0 skipped with the local MongoDB `rs0` replica set and an isolated `_test` database.
- Docker Compose configuration validated; the local MongoDB container reports `rs0` primary.

## Limits

- Location freshness defaults to 10 minutes and is configurable.
- Trip creation currently accepts `Distance` and `EstimatedTime` from the client and passes them to Matching's internal quote endpoint. Surge and promo are server-controlled, but distance/time are not independently verified; route-based calculation or a short-lived server-issued quote is still needed before treating the fare as tamper-resistant.
- Vehicle profile metadata remains outside Matching.
- `POST /match` is a read-only candidate preview; it also uses pickup coordinates supplied in that request rather than loading them from the persisted trip. Assignment and reservation use the event-driven matching command.
- Matching does not currently register OpenTelemetry traces/metrics; observability is limited to the configured logging and health endpoints.
