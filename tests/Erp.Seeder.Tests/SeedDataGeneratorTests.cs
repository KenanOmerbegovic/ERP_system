using System.Text.Json;
using Erp.Localization.BiH.Core;
using Erp.Modules.Core.Domain.Articles;
using Erp.Seeder;
using Erp.Seeder.Data;

namespace Erp.Seeder.Tests;

public sealed class SeedDataGeneratorTests
{
    private static readonly BiHTaxIdentifierRules TaxRules = new();

    [Theory]
    [InlineData("Small")]
    [InlineData("Full")]
    public void Generates_the_requested_amounts(string size)
    {
        var profile = SeedProfile.For(Enum.Parse<SeedSize>(size));

        var data = SeedDataGenerator.Generate(profile);

        Assert.Equal(profile.BusinessPartners, data.Partners.Count);
        Assert.Equal(profile.Articles, data.Articles.Count);
        Assert.Equal(["Knjigovođa", "Komercijalista", "Skladištar"], data.Roles.Select(role => role.Name));
    }

    [Fact]
    public void The_fixed_seed_gives_the_same_data_every_time()
    {
        var first = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));
        var second = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Company_and_users_are_the_same_for_every_size()
    {
        var small = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));
        var full = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));

        Assert.Equal(small.Company, full.Company);
        Assert.Equal(small.Administrator, full.Administrator);
        Assert.Equal(small.Roles.Select(role => role.User), full.Roles.Select(role => role.User));
    }

    [Fact]
    public void Tax_ids_are_unique_valid_BiH_JIBs_with_matching_VAT_numbers()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));
        var taxIds = data.Partners.Select(partner => partner.TaxId).Append(data.Company.TaxId).ToList();

        Assert.Equal(taxIds.Count, taxIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(taxIds, taxId =>
        {
            Assert.True(TaxRules.IsValidTaxId(taxId), taxId);
            Assert.StartsWith("4", taxId, StringComparison.Ordinal);
        });
        Assert.Equal(data.Company.TaxId[1..], data.Company.VatNumber);
        Assert.All(data.Partners.Where(partner => partner.VatNumber is not null), partner =>
        {
            Assert.True(TaxRules.IsValidVatNumber(partner.VatNumber!), partner.VatNumber);
            Assert.Equal(partner.TaxId[1..], partner.VatNumber);
        });
        Assert.Contains(data.Partners, partner => partner.VatNumber is null);
    }

    [Fact]
    public void Partners_are_in_BiH_cities_with_their_postal_codes()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));

        Assert.All(data.Partners, partner => Assert.Contains(new City(partner.City, partner.PostalCode), BiHData.Cities));
        Assert.Contains(new City(data.Company.City, data.Company.PostalCode), BiHData.Cities);
        Assert.Equal(data.Partners.Count, data.Partners.Select(partner => partner.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(data.Partners.Count, data.Partners.Select(partner => partner.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(data.Partners, partner => Assert.True(partner.IsCustomer || partner.IsSupplier, partner.Code));
    }

    [Fact]
    public void Articles_have_unique_codes_and_names_and_known_units()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));
        var units = data.Units.Select(unit => unit.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(data.Articles.Count, data.Articles.Select(article => article.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(data.Articles.Count, data.Articles.Select(article => article.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(data.Articles, article => Assert.Contains(article.UnitCode, units));
        Assert.Contains(data.Articles, article => article.Type == ArticleType.Service);
    }

    [Fact]
    public void Emails_use_only_reserved_domains_that_cannot_belong_to_anyone()
    {
        string[] reserved = [".test", ".example", ".invalid", "@example.com", "@example.org", "@example.net"];
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));

        // Every string the generator produces that looks like an address, wherever it appears.
        var addresses = JsonSerializer.Serialize(data)
            .Split('"')
            .Where(value => value.Contains('@', StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(addresses);
        Assert.All(addresses, address => Assert.Contains(reserved, suffix => address.EndsWith(suffix, StringComparison.Ordinal)));
    }

    [Fact]
    public void Users_have_unique_ascii_emails_at_the_company_domain()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));
        var users = data.Roles.Select(role => role.User).Prepend(data.Administrator).ToList();

        Assert.Equal(users.Count, users.Select(user => user.Email).Distinct(StringComparer.Ordinal).Count());
        var domain = data.Administrator.Email.Split('@')[1];
        Assert.All(users, user =>
        {
            Assert.EndsWith("@" + domain, user.Email, StringComparison.Ordinal);
            Assert.All(user.Email, letter => Assert.True(char.IsAscii(letter), user.Email));
        });
    }
}
