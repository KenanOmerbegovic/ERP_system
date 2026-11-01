using Erp.Modules.Core.Contracts.Common;
using Erp.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace Erp.Modules.Identity.Endpoints;

/// <summary>
/// Maps results to HTTP. Failures become problem details carrying the error <c>code</c>.
/// Same mapping as Core's; modules don't share Endpoints code.
/// </summary>
internal static class HttpResults
{
    public static IResult Created(Result<Guid> result) =>
        result.IsSuccess ? Results.Created((string?)null, new CreatedResponse(result.Value)) : Problem(result.Error);

    public static IResult NoContent(Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult Ok<T>(Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    public static IResult Problem(Error error) => error switch
    {
        ValidationError validation => Results.ValidationProblem(
            validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value),
            title: validation.Message,
            extensions: CodeExtension(validation)),
        _ => Results.Problem(
            title: error.Message,
            statusCode: StatusCodeFor(error.Type),
            extensions: CodeExtension(error)),
    };

    private static Dictionary<string, object?> CodeExtension(Error error) => new() { ["code"] = error.Code };

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
