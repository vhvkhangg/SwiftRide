# Trip messaging status

## Current Trip flow

`POST /trips` now gets a price from Matching/Pricing over authenticated internal HTTP, then saves the trip as `Requested`. It deliberately does not publish `TripRequestedV1`: matching before the rider accepts the quote would violate the ride flow in `RideLite-KienTrucPhanMem.pdf`.

`POST /trips/{id}/confirm-price` persists `PriceConfirmed`. RabbitMQ matching dispatch is intentionally not connected yet, pending approval of the command/event contracts in [TRIPSERVICE-API-REVIEW.md](./TRIPSERVICE-API-REVIEW.md). As a result, confirming a quote does not yet start driver search.

The existing `TripRequestedV1` contract and Matching consumer remain in the repository, but the Trip create path no longer publishes that message. They are not the active end-to-end workflow.

## Messaging to implement after contract approval

- Trip sends a command to Matching only after rider confirmation and whenever another driver search is needed.
- Matching returns one candidate or an exhausted-search result; Trip records an invitation and remains the owner of trip state.
- After dropoff, Trip sends the existing `ProcessPaymentV1` command. Trip consumes payment success/failure results.
- Trip needs an EF Outbox for its outbound commands and an Inbox/idempotency mechanism for consumed results.

Do not treat a RabbitMQ publish as proof that a driver accepted the trip or that payment succeeded. See the review report for the proposed message list, retries, and current gaps.
