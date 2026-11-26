using System.Globalization;
using System.Text;
using Bogus;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Domain.StockDocuments;

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

    /// <summary>Every seeded article is taxed at the standard rate.</summary>
    public const string StandardVatCode = "PDV17";

    /// <summary>Lines per opening receipt; larger warehouses get several.</summary>
    public const int MaxReceiptLines = 200;

    private const int MaxAttempts = 1000;

    /// <summary>Opening stock is received on this date, so the data does not depend on the day the seeder runs.</summary>
    public static readonly DateOnly OpeningDate = new(2026, 1, 1);

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
        var warehouses = Warehouses(random, profile.Warehouses);
        var receipts = OpeningReceipts(random, warehouses, articles);
        var orders = PurchaseOrders(random, partners, articles, profile.PurchaseOrders);
        return new SeedData(
            company, administrator, roles, units, partners, articles, warehouses, NumberSeries(), receipts, VatRates(), PurchasingNumberSeries(), orders);
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

    /// <summary>Fixed roles built from Core's and Warehouse's permissions; each gets one generated user.</summary>
    private static List<RoleSeed> Roles(Randomizer random, string domain, HashSet<string> emails) =>
    [
        new("Knjigovođa",
            [
                CorePermissions.CompaniesRead, CorePermissions.BusinessPartnersRead, CorePermissions.BusinessPartnersWrite,
                CorePermissions.UnitsOfMeasureRead, CorePermissions.ArticlesRead, CorePermissions.VatRatesRead,
                WarehousePermissions.WarehousesRead, WarehousePermissions.StockDocumentsRead, WarehousePermissions.StockRead,
                PurchasingPermissions.OrdersRead, PurchasingPermissions.ReceiptsRead, PurchasingPermissions.InvoicesRead,
                PurchasingPermissions.InvoicesWrite, PurchasingPermissions.InvoicesConfirm,
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
                WarehousePermissions.WarehousesRead, WarehousePermissions.WarehousesWrite, WarehousePermissions.StockDocumentsRead,
                WarehousePermissions.StockDocumentsWrite, WarehousePermissions.StockDocumentsConfirm, WarehousePermissions.StockRead,
                PurchasingPermissions.OrdersRead, PurchasingPermissions.ReceiptsRead, PurchasingPermissions.ReceiptsWrite,
                PurchasingPermissions.ReceiptsConfirm,
            ],
            Person(random, domain, emails)),
        new("Nabavljač",
            [
                CorePermissions.CompaniesRead, CorePermissions.BusinessPartnersRead, CorePermissions.BusinessPartnersWrite,
                CorePermissions.UnitsOfMeasureRead, CorePermissions.ArticlesRead, CorePermissions.VatRatesRead,
                WarehousePermissions.WarehousesRead, WarehousePermissions.StockRead,
                PurchasingPermissions.OrdersRead, PurchasingPermissions.OrdersWrite, PurchasingPermissions.OrdersConfirm,
                PurchasingPermissions.ReceiptsRead, PurchasingPermissions.ReceiptsWrite, PurchasingPermissions.ReceiptsConfirm,
                PurchasingPermissions.InvoicesRead, PurchasingPermissions.InvoicesWrite,
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

    /// <summary>
    /// About one article in ten is a service; goods names combine line, brand and variant, so they are unique.
    /// Food is LOT tracked and power tools serial tracked, as their catalog line says.
    /// </summary>
    private static List<ArticleSeed> Articles(Randomizer random, int count)
    {
        var serviceCount = Math.Min(count / 10, BiHData.Services.Length);
        var goods = BiHData.Goods
            .SelectMany(line => line.Variants.SelectMany(variant => BiHData.Brands.Select(brand =>
                (Name: $"{line.Name} {brand} {variant}", line.UnitCode, Type: ArticleType.Goods, line.Tracking))))
            .ToList();
        if (goods.Count < count - serviceCount)
        {
            throw new InvalidOperationException($"The goods catalog has {goods.Count} articles; {count - serviceCount} were asked for.");
        }

        var services = BiHData.Services.Select(service => (service.Name, service.UnitCode, Type: ArticleType.Service, Tracking: TrackingType.None));
        var chosen = random.Shuffle(goods).Take(count - serviceCount)
            .Concat(random.Shuffle(services).Take(serviceCount));
        return random.Shuffle(chosen)
            .Select((article, index) => new ArticleSeed($"A{index + 1:D5}", article.Name, article.Type, article.Tracking, article.UnitCode, random.Bool(0.97f), StandardVatCode))
            .ToList();
    }

    private static List<WarehouseSeed> Warehouses(Randomizer random, int count) =>
        random.Shuffle(BiHData.Cities).Take(count)
            .Select((city, index) => new WarehouseSeed($"MP{index + 1:D2}", index == 0 ? $"Centralno skladište {city.Name}" : $"Skladište {city.Name}"))
            .ToList();

    /// <summary>One series per document type from the opening date: PRM-2026-00001, IZD-…, MSK-….</summary>
    private static List<NumberSeriesSeed> NumberSeries() =>
    [
        new(StockDocumentType.Receipt, OpeningDate, "PRM-", 5),
        new(StockDocumentType.Issue, OpeningDate, "IZD-", 5),
        new(StockDocumentType.Transfer, OpeningDate, "MSK-", 5),
    ];

    /// <summary>
    /// Opening stock: the first warehouse holds every active goods article, the others about half of them. LOT tracked
    /// articles come in one or two LOTs; serial tracked ones get serial numbers unique per article.
    /// </summary>
    private static List<ReceiptSeed> OpeningReceipts(Randomizer random, IReadOnlyList<WarehouseSeed> warehouses, IReadOnlyList<ArticleSeed> articles)
    {
        var stockable = articles.Where(article => article is { Type: ArticleType.Goods, IsActive: true }).ToList();
        var serialCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        var receipts = new List<ReceiptSeed>();
        for (var index = 0; index < warehouses.Count; index++)
        {
            var lines = new List<ReceiptLineSeed>();
            foreach (var article in stockable)
            {
                if (index > 0 && !random.Bool(0.5f))
                {
                    continue;
                }

                var unitCost = article.Tracking == TrackingType.Serial
                    ? Math.Round(random.Decimal(80m, 400m), 2)
                    : Math.Round(random.Decimal(0.5m, 120m), 2);
                switch (article.Tracking)
                {
                    case TrackingType.Lot:
                        var lotCount = random.Int(1, 2);
                        for (var lot = 1; lot <= lotCount; lot++)
                        {
                            var expiry = OpeningDate.AddDays(random.Int(120, 540));
                            var lotNumber = $"L{index + 1}{lot}-{random.Int(100, 999)}";
                            lines.Add(new ReceiptLineSeed(article.Code, Quantity(random, article.UnitCode), unitCost, lotNumber, expiry, []));
                        }

                        break;
                    case TrackingType.Serial:
                        var pieces = random.Int(1, 4);
                        var first = serialCounters.GetValueOrDefault(article.Code);
                        serialCounters[article.Code] = first + pieces;
                        var serials = Enumerable.Range(first + 1, pieces).Select(n => $"SN-{article.Code}-{n:D4}").ToList();
                        lines.Add(new ReceiptLineSeed(article.Code, pieces, unitCost, null, null, serials));
                        break;
                    default:
                        lines.Add(new ReceiptLineSeed(article.Code, Quantity(random, article.UnitCode), unitCost, null, null, []));
                        break;
                }
            }

            receipts.AddRange(lines.Chunk(MaxReceiptLines).Select(chunk => new ReceiptSeed(warehouses[index].Code, OpeningDate, chunk)));
        }

        return receipts;
    }

    /// <summary>Whole pieces and packs; measured goods with up to two decimals.</summary>
    private static decimal Quantity(Randomizer random, string unitCode) => unitCode switch
    {
        "kom" or "pak" => random.Int(5, 200),
        "t" or "m3" => Math.Round(random.Decimal(1m, 20m), 2),
        _ => Math.Round(random.Decimal(10m, 500m), 1),
    };

    /// <summary>The BiH standard rate and exempt supplies, valid long before any seeded document.</summary>
    private static List<VatRateSeed> VatRates() =>
    [
        new(StandardVatCode, "Opća stopa PDV-a", 0.17m, new DateOnly(2006, 1, 1)),
        new("OSL", "Oslobođeno PDV-a", 0m, new DateOnly(2006, 1, 1)),
    ];

    private static List<PurchasingNumberSeriesSeed> PurchasingNumberSeries() =>
    [
        new(PurchasingDocumentType.PurchaseOrder, OpeningDate, "NAR-", 5),
        new(PurchasingDocumentType.GoodsReceipt, OpeningDate, "PRI-", 5),
        new(PurchasingDocumentType.SupplierInvoice, OpeningDate, "ULF-", 5),
    ];

    /// <summary>
    /// Orders to active suppliers for active goods that are not serial tracked, one every week from the opening date.
    /// In turn: fully received and invoiced, partly received, not received yet; the first invoice charges 5 % more on
    /// its first line, so there is a price difference for Finance.
    /// </summary>
    private static List<PurchaseOrderSeed> PurchaseOrders(Randomizer random, IReadOnlyList<PartnerSeed> partners, IReadOnlyList<ArticleSeed> articles, int count)
    {
        var suppliers = partners.Where(p => p is { IsSupplier: true, IsActive: true }).ToList();
        var goods = articles.Where(a => a is { Type: ArticleType.Goods, IsActive: true } && a.Tracking != TrackingType.Serial).ToList();
        var orders = new List<PurchaseOrderSeed>();
        for (var index = 0; index < count && suppliers.Count > 0 && goods.Count > 0; index++)
        {
            var lines = random.Shuffle(goods).Take(random.Int(2, 4))
                .Select((article, line) => new PurchaseOrderLineSeed(
                    article.Code,
                    article.UnitCode is "kom" or "pak" ? random.Int(10, 100) : Math.Round(random.Decimal(10m, 200m), 1),
                    Math.Round(random.Decimal(0.5m, 100m), 2),
                    article.Tracking == TrackingType.Lot ? $"N{index + 1:D3}-{line + 1}" : null))
                .ToList();
            var share = (index % 3) switch { 0 => 1m, 1 => 0.5m, _ => 0m };
            orders.Add(new PurchaseOrderSeed(
                random.ArrayElement(suppliers.ToArray()).Code,
                OpeningDate.AddDays(14 + (7 * index)),
                lines,
                share,
                share == 1m ? $"F-{OpeningDate.Year}-{index + 1:D4}" : null,
                index == 0 ? 1.05m : 1m));
        }

        return orders;
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
