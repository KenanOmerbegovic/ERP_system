using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Seeder;
using Erp.Seeder.Data;

namespace Erp.Seeder.Tests;

/// <summary>The generated sales scenario obeys the Sales module's rules before it reaches it.</summary>
public sealed class SalesSeedTests
{
    [Theory]
    [InlineData("Small")]
    [InlineData("Full")]
    public void FR_SAL_03_sales_go_to_active_customers_for_untracked_goods_the_first_warehouse_can_cover(string size)
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(Enum.Parse<SeedSize>(size)));
        var customers = data.Partners.Where(p => p is { IsCustomer: true, IsActive: true }).Select(p => p.Code).ToHashSet();
        var articles = data.Articles.ToDictionary(a => a.Code);
        var firstWarehouse = data.Warehouses[0].Code;
        var openingStock = data.OpeningReceipts.Where(r => r.WarehouseCode == firstWarehouse).SelectMany(r => r.Lines)
            .GroupBy(l => l.ArticleCode).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var goodsLines = data.SalesOrders.SelectMany(o => o.Lines).Concat(data.Offers.SelectMany(o => o.Lines)).Where(l => !l.IsService).ToList();

        Assert.All(data.SalesOrders.Select(o => o.CustomerCode).Concat(data.Offers.Select(o => o.CustomerCode)), code => Assert.Contains(code, customers));
        Assert.All(goodsLines, line =>
        {
            var article = articles[line.ArticleCode];
            Assert.True(article is { Type: ArticleType.Goods, IsActive: true, Tracking: TrackingType.None }, line.ArticleCode);
            Assert.True(line.Quantity <= openingStock[line.ArticleCode], line.ArticleCode);
        });
        Assert.Equal(goodsLines.Count, goodsLines.Select(l => l.ArticleCode).Distinct().Count());
        Assert.All(data.SalesOrders.SelectMany(o => o.Lines).Where(l => l.IsService), line => Assert.Equal(ArticleType.Service, articles[line.ArticleCode].Type));
        Assert.All(data.SalesOrders, order => Assert.True(order.OfferDate > SeedDataGenerator.OpeningDate && order.CreditNoteDate < new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void FR_SAL_05_every_scenario_is_seeded_including_corrections()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));

        Assert.Contains(data.SalesOrders, o => o is { FromOffer: true, DeliveredShare: 1m, Correction: SalesCorrection.Return });
        Assert.Contains(data.SalesOrders, o => o is { FromOffer: false, DeliveredShare: 0.5m, Correction: SalesCorrection.ValueReduction });
        Assert.Contains(data.SalesOrders, o => o is { FromOffer: true, DeliveredShare: 0m, Invoiced: false });
        Assert.Contains(data.SalesOrders, o => o.Correction == SalesCorrection.InvoiceStorno);
        Assert.Contains(data.SalesOrders, o => o.Lines.Any(l => l.IsService));
        Assert.All(data.SalesOrders.Where(o => o.DeliveredShare > 0m), o => Assert.Contains(o.Lines, l => SalesOrderSeed.Delivered(l, o.DeliveredShare) > 0m));
        Assert.Equal([OfferOutcome.Sent, OfferOutcome.Rejected], data.Offers.Select(o => o.Outcome));
        Assert.Equal(Enum.GetValues<SalesDocumentType>(), data.SalesNumberSeries.Select(s => s.DocumentType).Order());
    }
}
