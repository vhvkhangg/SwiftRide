# Task 2 - Trip Service Verification

## Delivered

- Trip creation obtains server-controlled quote from Matching/Pricing; client fare is never trusted.
- Idempotency compares the complete business request: rider, addresses, coordinates, distance, estimated time, ride type and promo code.
- Rider confirms price before matching starts. Matching attempts are bounded by TripMatching:MaxAttempts.
- Matching results create driver invitations; invitation acceptance is separate from driver discovery.
- Reject and timeout release the reservation, increment attempt and request another candidate; exhaustion marks the trip failed.
- Driver accept is protected by authorization, active invitation checks, transaction scope and optimistic Trip rowversion.
- Active-trip recovery exists for Rider and Driver; trip detail is ownership-filtered; internal state/payment endpoints require service policies.
- Database uniqueness allows at most one invitation per (TripId, Attempt) even when matching results race concurrently.

## Verification

- Release solution build: 0 warnings, 0 errors.
- Trip Service unit/local tests: 29 passed, 0 failed.
- Full solution non-integration tests: 156 passed, 0 failed.
- Task 5 real-stack suite: 24/24 checks passed after applying the new Trip migration.

## Limits

- Vehicle profile data is not modeled yet, so trip detail exposes DriverId but no vehicle metadata.
- Real map/geocoding remains outside this task; Matching/Pricing receives validated coordinates and server-controlled quote inputs.
