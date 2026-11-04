using System.Globalization;
using System.Text;
using Bogus;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Domain.Articles;

namespace Erp.Seeder.Data;

/// <summary>
/// Generates the mock data from a fixed seed, so every run creates the same company, people and
/// master data. The company and users are generated first, so they are the same for every size.
/// </summary>
internal static class SeedDataGenerator
{
    public const int Seed = 20261103;

    /// <summary>Every generated email address uses this reserved TLD.</summary>
    public const string ReservedTopLevelDomain = ".test";

    private const int MaxAttempts = 1000;

    public static SeedData Generate(SeedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var random = new Randomizer(Seed);
        var taxIds = new HashSet<string>(StringComparer.Ordinal);

        var company = Company(random, taxIds);
        var domain = EmailDomain(company.Name);
        var emails = new HashSet<string>(StringComparer.Ordinal);
        var administrator = Person(random, domain, emails);
        var roles = Roles(random, domain, emails);

        var units = BiHData.Units.Select(unit => new UnitSeed(unit.Code, unit.Name)).ToList();
        var partners = Partners(random, profile.BusinessPartners, taxIds);
        var articles = Articles(random, profile.Articles);
        return new SeedData(company, administrator, roles, units, partners, articles);
    }

    /// <summary>BiH JIB: 13 digits, legal entities start with 4. The PDV number is the JIB without that leading 4.</summary>
    public static string VatNumberFor(string taxId) => taxId[1..];

    private static CompanySeed Company(Randomizer random, HashSet<string> taxIds)
    {
        var city = random.ArrayElement(BiHData.Cities);
        var taxId = TaxId(random, taxIds);
        return new CompanySeed(
            $"{random.ArrayElement(BiHData.Rivers)} {random.ArrayElement(BiHData.Activities)} d.o.o.",
            taxId,
            VatNumberFor(taxId),
            Address(random),
            city.PostalCode,
            city.Name);
    }

    /// <summary>Fixed roles built from Core's permissions; each gets one generated user.</summary>
    private static List<RoleSeed> Roles(Randomizer random, string domain, HashSet<string> emails) =>
    [
        new("Knjigovođa",
            [
                CorePermissions.CompaniesRead, CorePermissions.BusinessPartnersRead, CorePermissions.BusinessPartnersWrite,
                CorePermissions.UnitsOfMeasureRead, CorePermissions.ArticlesRead,
            ],
            Person(random, domain, emails)),
        new("Komercijalista",
            [
                CorePermissions.CompaniesRead, CorePermissions.BusinessPartnersRead, CorePermissions.BusinessPartnersWrite,
                CorePermissions.UnitsOfMeasureRead, CorePermissions.ArticlesRead, CorePermissions.ArticlesWrite,
            ],
            Person(random, domain, emails)),
        new("Skladištar",
            [
                CorePermissions.CompaniesRead, CorePermissions.BusinessPartnersRead, CorePermissions.UnitsOfMeasureRead,
                CorePermissions.UnitsOfMeasureWrite, CorePermissions.ArticlesRead, CorePermissions.ArticlesWrite,
            ],
            Person(random, domain, emails)),
    ];

    private static UserSeed Person(Randomizer random, string domain, HashSet<string> emails)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var first = random.ArrayElement(BiHData.FirstNames);
            var last = random.ArrayElement(BiHData.LastNames);
            var email = $"{Ascii(first)}.{Ascii(last)}@{domain}";
            if (emails.Add(email))
            {
                return new UserSeed(email, $"{first} {last}");
            }
        }

        throw new InvalidOperationException("Ran out of unique person names.");
    }

    private static List<PartnerSeed> Partners(Randomizer random, int count, HashSet<string> taxIds)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var partners = new List<PartnerSeed>(count);
        for (var number = 1; number <= count; number++)
        {
            var city = random.ArrayElement(BiHData.Cities);
            var taxId = TaxId(random, taxIds);
            var role = random.Int(0, 99);
            partners.Add(new PartnerSeed(
                Code: $"P{number:D5}",
                Name: Unique(names, () => PartnerName(random), "partner names"),
                TaxId: taxId,
                VatNumber: random.Bool(0.7f) ? VatNumberFor(taxId) : null,
                Address: Address(random),
                PostalCode: city.PostalCode,
                City: city.Name,
                IsCustomer: role < 75,
                IsSupplier: role >= 60,
                IsActive: random.Bool(0.95f)));
        }

        return partners;
    }

    private static string PartnerName(Randomizer random) => random.Int(0, 99) switch
    {
        < 55 => $"{random.ArrayElement(BiHData.Rivers)} {random.ArrayElement(BiHData.Activities)} d.o.o.",
        < 75 => $"{random.ArrayElement(BiHData.LastNames)} {random.ArrayElement(BiHData.Activities)} d.o.o.",
        < 85 => $"{random.ArrayElement(BiHData.Rivers)} d.d.",
        _ => $"{random.ArrayElement(BiHData.Crafts)} \"{random.ArrayElement(BiHData.LastNames)}\"",
    };

    /// <summary>About one article in ten is a service; goods names combine line, brand and variant, so they are unique.</summary>
    private static List<ArticleSeed> Articles(Randomizer random, int count)
    {
        var serviceCount = Math.Min(count / 10, BiHData.Services.Length);
        var goods = BiHData.Goods
            .SelectMany(line => line.Variants.SelectMany(variant => BiHData.Brands.Select(brand =>
                (Name: $"{line.Name} {brand} {variant}", line.UnitCode, Type: ArticleType.Goods))))
            .ToList();
        if (goods.Count < count - serviceCount)
        {
            throw new InvalidOperationException($"The goods catalog has {goods.Count} articles; {count - serviceCount} were asked for.");
        }

        var services = BiHData.Services.Select(service => (service.Name, service.UnitCode, Type: ArticleType.Service));
        var chosen = random.Shuffle(goods).Take(count - serviceCount)
            .Concat(random.Shuffle(services).Take(serviceCount));
        return random.Shuffle(chosen)
            .Select((article, index) => new ArticleSeed($"A{index + 1:D5}", article.Name, article.Type, article.UnitCode, random.Bool(0.97f)))
            .ToList();
    }

    private static string TaxId(Randomizer random, HashSet<string> taxIds) =>
        Unique(taxIds, () => "4" + random.ReplaceNumbers("############"), "tax ids");

    private static string Address(Randomizer random) =>
        $"{random.ArrayElement(BiHData.Streets)} {random.Int(1, 150).ToString(CultureInfo.InvariantCulture)}";

    private static string Unique(HashSet<string> taken, Func<string> next, string what)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = next();
            if (taken.Add(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException($"Ran out of unique {what}.");
    }

    /// <summary>
    /// "Bosna Promet d.o.o." → "bosnapromet.test". The reserved .test TLD (RFC 2606) never resolves,
    /// so no generated address can reach a real company or person.
    /// </summary>
    private static string EmailDomain(string companyName) =>
        Ascii(companyName.Replace(" d.o.o.", string.Empty, StringComparison.Ordinal)).Replace("-", string.Empty, StringComparison.Ordinal) + ReservedTopLevelDomain;

    /// <summary>Lowercase ASCII letters only, with Bosnian letters transliterated (đ → dj).</summary>
    private static string Ascii(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var letter in value.ToLowerInvariant())
        {
            builder.Append(letter switch
            {
                'č' or 'ć' => "c",
                'đ' => "dj",
                'š' => "s",
                'ž' => "z",
                _ when char.IsAsciiLetterOrDigit(letter) || letter == '-' => letter.ToString(),
                _ => string.Empty,
            });
        }

        return builder.ToString();
    }
}
