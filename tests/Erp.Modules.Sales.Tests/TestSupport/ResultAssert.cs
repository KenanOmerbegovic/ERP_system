using Erp.SharedKernel.Results;

namespace Erp.Modules.Sales.Tests.TestSupport;

internal static class ResultAssert
{
    public static void Succeeds(Result result) =>
        Assert.True(result.IsSuccess, result.IsFailure ? $"Expected success, got {Describe(result.Error)}" : null);

    public static void Fails(Result result, Error expected)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.Equal(expected, result.Error);
    }

    public static void FailsWithCode(Result result, string code)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.True(code == result.Error.Code, $"Expected {code}, got {Describe(result.Error)}");
    }

    public static void FailsValidation(Result result, string property, string code)
    {
        Assert.True(result.IsFailure, "Expected a validation failure.");
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.True(validation.Errors.TryGetValue(property, out var codes), $"No error for {property}; errors: {Describe(validation)}");
        Assert.Contains(code, codes);
    }

    private static string Describe(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Errors.Select(field => $"{field.Key}: {string.Join(", ", field.Value)}"))
        : $"{error.Code} ({error.Message})";
}
