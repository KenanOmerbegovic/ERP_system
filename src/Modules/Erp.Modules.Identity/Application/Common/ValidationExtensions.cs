using Erp.SharedKernel.Results;
using FluentValidation;
using FluentValidation.Results;

namespace Erp.Modules.Identity.Application.Common;

internal static class ValidationExtensions
{
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;

    /// <summary>Error codes per property; clients turn codes into UI text.</summary>
    public static ValidationError ToValidationError(this ValidationResult result) =>
        new(result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal));

    /// <summary>A failure found after input validation, reported like one.</summary>
    public static ValidationError PropertyError(string property, string code) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [property] = [code] });

    public static IRuleBuilderOptions<T, string> MustBeValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Domain.FieldLengths.Email).EmailAddress();
}
