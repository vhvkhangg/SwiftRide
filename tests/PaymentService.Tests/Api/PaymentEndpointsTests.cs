using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Moq;
using SwiftRide.PaymentService.Api.Endpoints;
using SwiftRide.PaymentService.Application.Services;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using SwiftRide.PaymentService.Application.Integrations;

namespace SwiftRide.PaymentService.Tests.Api;

public sealed class PaymentEndpointsTests
{
    [Fact]
    public async Task GetPaymentByTrip_EmptyGuid_Returns400()
    {
        var method = typeof(PaymentEndpoints).GetMethod(
            "GetPaymentByTripAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var service = new Mock<IPaymentApplicationService>();
        service.Setup(x => x.GetPaymentByTripIdAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("TripId không được để trống."));

        var call = method.Invoke(null, new object[]
        {
            Guid.Empty, service.Object, TestContext.Current.CancellationToken
        });
        var task = Assert.IsAssignableFrom<Task<IResult>>(call);
        var result = await task;
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }


    [Fact]
    public async Task PaymentRoutes_ShouldNotExposeDirectCreationEndpoint()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddAuthorization();

        builder.Services.AddSingleton<IPaymentApplicationService>(
            Mock.Of<IPaymentApplicationService>());

        builder.Services.AddSingleton<ITripServiceClient>(
            Mock.Of<ITripServiceClient>());

        await using var app = builder.Build();

        app.MapPaymentEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.Contains(routes, route =>
            route.RoutePattern.RawText?.TrimEnd('/') ==
            "/payments/{id:guid}/refund");

        Assert.DoesNotContain(routes, route =>
            route.RoutePattern.RawText?.TrimEnd('/') == "/payments" &&
            route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Contains("POST") == true);

        Assert.DoesNotContain(routes, route =>
            route.RoutePattern.RawText?.TrimEnd('/') ==
            "/payments/{id:guid}/reconcile");
    }

}
