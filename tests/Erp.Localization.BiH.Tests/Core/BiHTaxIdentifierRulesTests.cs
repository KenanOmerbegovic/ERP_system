using Erp.Localization.BiH.Core;

namespace Erp.Localization.BiH.Tests.Core;

public class BiHTaxIdentifierRulesTests
{
    private readonly BiHTaxIdentifierRules _rules = new();

    [Fact]
    public void Applies_to_Bosnia_and_Herzegovina()
    {
        Assert.Equal("BA", _rules.CountryCode);
    }

    [Theory]
    [InlineData("4200000000001")]
    [InlineData("0101990170001")]
    public void Tax_id_is_thirteen_digits(string value)
    {
        Assert.True(_rules.IsValidTaxId(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("420000000000")]
    [InlineData("42000000000012")]
    [InlineData("420000000000A")]
    [InlineData("4200 00000000")]
    [InlineData("٤٢٠٠٠٠٠٠٠٠٠٠١")]
    public void Tax_id_rejects_anything_else(string value)
    {
        Assert.False(_rules.IsValidTaxId(value));
    }

    [Theory]
    [InlineData("200000000001")]
    public void Vat_number_is_twelve_digits(string value)
    {
        Assert.True(_rules.IsValidVatNumber(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("4200000000001")]
    [InlineData("20000000001")]
    [InlineData("20000000000X")]
    public void Vat_number_rejects_anything_else(string value)
    {
        Assert.False(_rules.IsValidVatNumber(value));
    }
}
