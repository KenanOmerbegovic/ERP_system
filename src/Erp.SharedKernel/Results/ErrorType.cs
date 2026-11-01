namespace Erp.SharedKernel.Results;

public enum ErrorType
{
    /// <summary>A business rule rejected the operation.</summary>
    Failure,

    /// <summary>The input is invalid.</summary>
    Validation,

    /// <summary>The addressed resource does not exist.</summary>
    NotFound,

    /// <summary>The operation conflicts with existing data, e.g. a duplicate code.</summary>
    Conflict,

    /// <summary>The caller could not be authenticated, e.g. wrong credentials or an invalid token.</summary>
    Unauthorized,
}
