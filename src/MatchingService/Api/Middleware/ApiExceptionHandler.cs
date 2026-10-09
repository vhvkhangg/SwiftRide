using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SwiftRide.MatchingService.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ArgumentException or ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ"),
            _ => (StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không mong muốn")
        };

        if (status >= 500)
            logger.LogError(exception, "Lỗi chưa được xử lý khi đang xử lý đường dẫn {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Yêu cầu không hợp lệ cho đường dẫn {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status400BadRequest
                ? exception.Message
                : "Không thể hoàn thành yêu cầu.",
            Instance = httpContext.Request.Path
        }, cancellationToken);
        return true;
    }
}