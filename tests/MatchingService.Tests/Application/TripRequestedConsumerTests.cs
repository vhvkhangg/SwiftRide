using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SwiftRide.Contracts.Trips;
using SwiftRide.MatchingService.Application.Consumers;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Application.Interfaces;

namespace SwiftRide.MatchingService.Tests.Application;

public class TripRequestedConsumerTests
{
    [Fact]
    public async Task Consume_CallsMatchingWithMessageCoordinates()
    {
        var message = new TripRequestedV1
        {
            TripId = Guid.NewGuid(), PickupLatitude = 10.7769, PickupLongitude = 106.7009
        };
        var context = new Mock<ConsumeContext<TripRequestedV1>>();
        context.SetupGet(x => x.Message).Returns(message);
        var matching = new Mock<IMatchingService>();
        matching.Setup(x => x.MatchAsync(It.IsAny<MatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MatchResponse(message.TripId.ToString(), Array.Empty<MatchedDriver>()));
        var consumer = new TripRequestedConsumer(matching.Object, NullLogger<TripRequestedConsumer>.Instance);

        await consumer.Consume(context.Object);

        matching.Verify(x => x.MatchAsync(
            new MatchRequest(message.TripId.ToString(), 10.7769, 106.7009, 5),
            context.Object.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData(null, 106.0)]
    [InlineData(10.0, null)]
    [InlineData(91.0, 106.0)]
    [InlineData(10.0, double.PositiveInfinity)]
    public async Task Consume_RejectsInvalidCoordinates(double? latitude, double? longitude)
    {
        var context = new Mock<ConsumeContext<TripRequestedV1>>();
        context.SetupGet(x => x.Message).Returns(new TripRequestedV1
        {
            TripId = Guid.NewGuid(), PickupLatitude = latitude, PickupLongitude = longitude
        });
        var matching = new Mock<IMatchingService>();
        var consumer = new TripRequestedConsumer(matching.Object, NullLogger<TripRequestedConsumer>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => consumer.Consume(context.Object));
        matching.Verify(x => x.MatchAsync(It.IsAny<MatchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
