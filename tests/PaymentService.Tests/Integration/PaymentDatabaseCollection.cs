using Xunit;

namespace SwiftRide.PaymentService.Tests.Integration;

// Both classes migrate the same database. Serialize their setup while retaining
// the explicit Task.WhenAll races inside individual concurrency tests.
[CollectionDefinition("Payment database", DisableParallelization = true)]
public sealed class PaymentDatabaseCollection;
