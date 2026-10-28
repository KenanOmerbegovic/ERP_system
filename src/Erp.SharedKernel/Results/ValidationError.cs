namespace Erp.SharedKernel.Results;

/// <summary>Input validation failed; <see cref="Errors"/> maps each property to its error codes.</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation);
