using System.Reflection;
using Microsoft.AspNetCore.Http;
using Moq;
using SwiftRide.PaymentService.Api.Endpoints;
using SwiftRide.PaymentService.Application.Services;
using Xunit;

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
}
