using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Services;

namespace SwiftRide.PaymentService.Api.Endpoints
{
    public static class PaymentEndpoints
    {
        public static IEndpointRouteBuilder MapPaymentEndpoints(
            this IEndpointRouteBuilder endpoints
        )
        {
            var group = endpoints.MapGroup("/payments");

            group.MapPost("/", CreatePaymentAsync);
            group.MapGet("/{id:guid}", GetPaymentAsync);
            group.MapPost("/{id:guid}/refund", RefundPaymentAsync);

            return endpoints;
        }

        private static async Task<IResult> CreatePaymentAsync(
            CreatePaymentRequest request,
            IPaymentApplicationService paymentService,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var payment = await paymentService.CreatePaymentAsync(
                    request,
                    cancellationToken
                );

                return Results.Created(
                    $"/payments/{payment.Id}",
                    payment
                );
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new
                {
                    error = exception.Message
                });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new
                {
                    error = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new
                {
                    error = exception.Message
                });
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    detail: "Không thể hoàn tất giao tiếp với TripService. "
              + "Nếu đã tạo Payment, hãy retry bằng cùng IdempotencyKey.",
              statusCode: StatusCodes.Status502BadGateway
                );
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    detail: "TripService không phản hồi kịp thời. "
              + "Hãy retry bằng cùng IdempotencyKey.",
        statusCode: StatusCodes.Status504GatewayTimeout);
                ;
            }
        }

        private static async Task<IResult> GetPaymentAsync(
            Guid id,
            IPaymentApplicationService paymentService,
            CancellationToken cancellationToken
        )
        {
            var payment =
                await paymentService.GetPaymentByIdAsync(
                    id,
                    cancellationToken);

            return payment is null
                ? Results.NotFound()
                : Results.Ok(payment);
        }

        private static async Task<IResult> RefundPaymentAsync(
            Guid id,
            IPaymentApplicationService paymentService,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var result =
                    await paymentService.RefundPaymentAsync(
                        id,
                        cancellationToken);

                return Results.Ok(result);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new
                {
                    error = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new
                {
                    error = exception.Message
                });
            }
        }
    }
}