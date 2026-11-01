using Erp.SharedKernel.Results;

namespace Erp.Modules.Identity.Tests.TestSupport;

internal static class ResultAssert
{
    public static void Fails(Result result, Error expected)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.Equal(expected, result.Error);
    }

    /// <summary>Asserts a validation failure with <paramref name="code"/> on <paramref name="property"/>.</summary>
    public static void FailsValidation(Result result, string property, string code)
    {
        Assert.True(result.IsFailure, "Expected a validation failure.");
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.True(
            validation.Errors.TryGetValue(property, out var codes),
            $"No error for {property}; errors: {string.Join(", ", validation.Errors.Keys)}");
        Assert.Contains(code, codes);
    }
}
