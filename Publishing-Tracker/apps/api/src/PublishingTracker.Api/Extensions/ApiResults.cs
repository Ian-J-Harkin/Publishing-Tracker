namespace PublishingTracker.Api.Extensions;

public sealed record ApiErrorResponse(string Message);

public static class ApiResults
{
    public static IResult BadRequest(string message) =>
        Results.BadRequest(new ApiErrorResponse(message));

    public static IResult NotFound(string message) =>
        Results.NotFound(new ApiErrorResponse(message));

    public static IResult Unauthorized(string message) =>
        Results.Json(new ApiErrorResponse(message), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult Forbidden(string message) =>
        Results.Json(new ApiErrorResponse(message), statusCode: StatusCodes.Status403Forbidden);
}
