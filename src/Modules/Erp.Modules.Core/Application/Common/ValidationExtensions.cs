using Erp.SharedKernel.Results;
using FluentValidation.Results;

namespace Erp.Modules.Core.Application.Common;

internal static class ValidationExtensions
{
    /// <summary>Error codes per property; clients turn codes into UI text.</summary>
    public static ValidationError ToValidationError(this ValidationResult result) =>
        new(result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal));
}
