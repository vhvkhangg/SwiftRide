using MassTransit;
using Moq;
using SwiftRide.Contracts.Trips;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Interfaces;
using Xunit;

namespace SwiftRide.TripService.Tests;

public class TripMessagingTests
{
    private readonly Mock<ITripWriter> _writer = new();
    private readonly Mock<IPublishEndpoint> _bus = new();
    private readonly Mock<ITripReader> _reader = new();

    private TripApplicationService CreateService()
    {
        var publisher = new MessagePublisher(_bus.Object, new Mock<ISendEndpointProvider>().Object);
        return new TripApplicationService(_writer.Object, _reader.Object, publisher);
    }

    private static CreateTripRequest ValidRequest()
    {
        return new CreateTripRequest(Guid.NewGuid(), "Bến Thành", "Sân bay", 100000,
            10.7769, 106.7009);
    }

    [Fact]
    public async Task Create_SavesTripBeforePublishingItsData()
    {
        Trip? saved = null;
        TripRequestedV1? published = null;
        _writer.Setup(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .Callback<Trip, CancellationToken>((trip, ct) => saved = trip)
            .Returns(Task.CompletedTask);
        _bus.Setup(x => x.Publish(It.IsAny<TripRequestedV1>(), It.IsAny<CancellationToken>()))
            .Callback<TripRequestedV1, CancellationToken>((message, ct) =>
            {
                Assert.NotNull(saved);
                Assert.Equal(saved.Id, message.TripId);
                published = message;
            })
            .Returns(Task.CompletedTask);

        var request = ValidRequest();
        var result = await CreateService().CreateAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(published);
        Assert.Equal(result.Id, published.TripId);
        Assert.Equal(result.Id, published.CorrelationId);
        Assert.Equal(request.RiderId, published.RiderId);
        Assert.Equal(request.PickupAddress, published.PickupAddress);
        Assert.Equal(request.PickupLatitude, published.PickupLatitude);
        Assert.Equal(request.PickupLongitude, published.PickupLongitude);
        Assert.Equal(TimeSpan.Zero, published.OccurredAtUtc.Offset);
        _bus.Verify(x => x.Publish(It.IsAny<TripRequestedV1>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, 106.0)]
    [InlineData(10.0, null)]
    [InlineData(91.0, 106.0)]
    [InlineData(10.0, 181.0)]
    [InlineData(double.NaN, 106.0)]
    public async Task Create_RejectsInvalidCoordinatesBeforeSaving(double? latitude, double? longitude)
    {
        var request = ValidRequest() with { PickupLatitude = latitude, PickupLongitude = longitude };
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateAsync(request, TestContext.Current.CancellationToken));
        _writer.Verify(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
        _bus.Verify(x => x.Publish(It.IsAny<TripRequestedV1>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_DoesNotPublishWhenSavingFails()
    {
        _writer.Setup(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateAsync(ValidRequest(), TestContext.Current.CancellationToken));
        _bus.Verify(x => x.Publish(It.IsAny<TripRequestedV1>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_PreservesSavedTripIdWhenPublishingFails()
    {
        Trip? saved = null;
        _writer.Setup(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .Callback<Trip, CancellationToken>((trip, ct) => saved = trip)
            .Returns(Task.CompletedTask);
        _bus.Setup(x => x.Publish(It.IsAny<TripRequestedV1>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ unavailable"));

        var error = await Assert.ThrowsAsync<TripPublishException>(() => CreateService().CreateAsync(ValidRequest(), TestContext.Current.CancellationToken));
        Assert.NotNull(saved);
        Assert.Equal(saved.Id, error.TripId);
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
