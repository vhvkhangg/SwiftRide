using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using SwiftRide.MatchingService.Application.Consumers;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Application.Interfaces;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Interfaces;
using Xunit;

namespace SwiftRide.TripService.Tests;

public class RabbitMqFlowTests
{
    // Chạy chủ động với RabbitMQ thật trong vhost test riêng, không chạm queue nghiệp vụ.
    [Fact(Explicit = true)]
    public async Task CreateTrip_ReachesMatchingQueueAndRetriesTransientFailure()
    {
        var builder = Host.CreateApplicationBuilder();
        var virtualHost = builder.Configuration["RabbitMq:VirtualHost"];
        if (virtualHost == null || !virtualHost.StartsWith("swiftride-nv6-test-"))
        {
            throw new InvalidOperationException("Cần RabbitMq__VirtualHost bắt đầu bằng swiftride-nv6-test-.");
        }

        var received = new TaskCompletionSource<MatchRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<IDriverRepository>();
        var attempts = 0;
        repository.Setup(x => x.FindNearbyAvailableAsync(
                It.IsAny<double>(), It.IsAny<double>(), 5, 10, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("Lỗi DB tạm thời để kiểm tra retry.");
                }
                return Task.FromResult<IReadOnlyList<(DriverLocation Driver, double DistanceKm)>>(
                    Array.Empty<(DriverLocation, double)>());
            });
        var matchingService = new SwiftRide.MatchingService.Application.Services.MatchingService(repository.Object);
        var matching = new Mock<IMatchingService>();
        matching.Setup(x => x.MatchAsync(It.IsAny<MatchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (MatchRequest request, CancellationToken ct) =>
            {
                var result = await matchingService.MatchAsync(request, ct);
                received.TrySetResult(request);
                return result;
            });
        builder.Services.AddSingleton(matching.Object);
        builder.Services.AddSwiftRideMessaging(builder.Configuration, "matching", consumers =>
        {
            consumers.AddConsumer<TripRequestedConsumer>()
                .Endpoint(endpoint => endpoint.Name = MessagingEndpoints.MatchingTripRequested);
        });

        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;
        await host.StartAsync(ct);
        try
        {
            using var scope = host.Services.CreateScope();
            var writer = new Mock<ITripWriter>();
            writer.Setup(x => x.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var service = new TripApplicationService(writer.Object, new Mock<ITripReader>().Object,
                scope.ServiceProvider.GetRequiredService<MessagePublisher>());
            var response = await service.CreateAsync(new CreateTripRequest(
                Guid.NewGuid(), "Bến Thành", "Sân bay", 100000, 10.7769, 106.7009), ct);

            var request = await received.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            Assert.Equal(response.Id.ToString(), request.TripId);
            Assert.Equal(10.7769, request.PickupLatitude);
            Assert.Equal(106.7009, request.PickupLongitude);
            Assert.Equal(3, attempts);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
