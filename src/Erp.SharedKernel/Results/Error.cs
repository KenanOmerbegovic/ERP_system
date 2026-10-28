namespace Erp.SharedKernel.Results;

/// <summary>
/// An expected failure. <see cref="Code"/> is stable and machine-readable (clients map it to
/// UI text); <see cref="Type"/> decides how the failure is reported, e.g. the HTTP status.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
