using Erp.SharedKernel.Monetary;

namespace Erp.SharedKernel.Tests.Monetary;

public class CurrencyTests
{
    [Theory]
    [InlineData("BAM")]
    [InlineData("bam")]
    [InlineData(" BAM ")]
    public void FromCode_finds_known_currency(string code)
    {
        var result = Currency.FromCode(code);

        Assert.True(result.IsSuccess);
        Assert.Same(Currency.BAM, result.Value);
        Assert.Equal(2, result.Value.MinorUnits);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void FromCode_fails_for_unknown_currency(string? code)
    {
        var result = Currency.FromCode(code);

        Assert.True(result.IsFailure);
        Assert.Equal("Money.UnknownCurrency", result.Error.Code);
    }
}
