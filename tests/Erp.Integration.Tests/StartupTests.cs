using Erp.Integration.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Erp.Integration.Tests;

/// <summary>The API refuses to start with a signing key anyone could know.</summary>
[Collection(ApiCollection.Name)]
public sealed class StartupTests(ApiFactory factory)
{
    [Theory]
    [InlineData("")]
    [InlineData("shorter-than-thirty-two-bytes")]
    [InlineData("dev-only-signing-key-change-outside-development-0123456789")]
    public void The_api_does_not_start_with_a_missing_short_or_placeholder_signing_key(string key)
    {
        using var server = factory.WithSettings(("Identity:Jwt:SigningKey", key));

        var exception = Assert.ThrowsAny<Exception>(() => server.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(exception.GetBaseException(), exactMatch: false);
        Assert.Contains(validation.Failures, failure => failure.Contains("SigningKey", StringComparison.Ordinal));
        Assert.DoesNotContain(validation.Failures, failure => key.Length > 0 && failure.Contains(key, StringComparison.Ordinal));
    }
}
