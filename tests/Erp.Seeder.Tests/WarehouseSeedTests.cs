using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Seeder;
using Erp.Seeder.Data;

namespace Erp.Seeder.Tests;

/// <summary>The generated warehouses and opening stock obey the Warehouse module's rules before they reach it.</summary>
public sealed class WarehouseSeedTests
{
    [Theory]
    [InlineData("Small")]
    [InlineData("Full")]
    public void FR_GEN_01_opening_stock_fills_every_warehouse_with_active_goods(string size)
    {
        var profile = SeedProfile.For(Enum.Parse<SeedSize>(size));

        var data = SeedDataGenerator.Generate(profile);

        Assert.Equal(profile.Warehouses, data.Warehouses.Count);
        Assert.Equal(data.Warehouses.Count, data.Warehouses.Select(w => w.Code).Distinct().Count());
        Assert.Equal(data.Warehouses.Select(w => w.Code), data.OpeningReceipts.Select(r => r.WarehouseCode).Distinct());
        var articles = data.Articles.ToDictionary(a => a.Code);
        var lines = data.OpeningReceipts.SelectMany(r => r.Lines).ToList();
        Assert.All(lines, line => Assert.True(articles[line.ArticleCode] is { Type: ArticleType.Goods, IsActive: true }, line.ArticleCode));
        Assert.All(data.OpeningReceipts, receipt => Assert.InRange(receipt.Lines.Count, 1, SeedDataGenerator.MaxReceiptLines));
        Assert.All(data.OpeningReceipts, receipt => Assert.Equal(SeedDataGenerator.OpeningDate, receipt.DocumentDate));
        Assert.All(lines, line => Assert.True(line.Quantity > 0m && line.UnitCost >= 0m, line.ArticleCode));
    }

    [Fact]
    public void FR_WH_02_tracked_articles_get_lots_and_serial_numbers()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Full));
        var articles = data.Articles.ToDictionary(a => a.Code);
        var lines = data.OpeningReceipts.SelectMany(r => r.Lines).ToList();

        Assert.Contains(lines, line => articles[line.ArticleCode].Tracking == TrackingType.Lot);
        Assert.Contains(lines, line => articles[line.ArticleCode].Tracking == TrackingType.Serial);
        Assert.All(lines, line =>
        {
            var tracking = articles[line.ArticleCode].Tracking;
            Assert.Equal(tracking == TrackingType.Lot, line.LotNumber is not null);
            Assert.Equal(tracking == TrackingType.Serial ? line.Quantity : 0m, line.SerialNumbers.Count);
        });
        var serials = lines.SelectMany(line => line.SerialNumbers.Select(serial => (line.ArticleCode, serial))).ToList();
        Assert.Equal(serials.Count, serials.Distinct().Count());
        Assert.DoesNotContain(data.Articles, article => article.Type == ArticleType.Service && article.Tracking != TrackingType.None);
    }

    [Fact]
    public void FR_WH_01_every_document_type_has_a_number_series_from_the_opening_date()
    {
        var data = SeedDataGenerator.Generate(SeedProfile.For(SeedSize.Small));

        Assert.Equal(Enum.GetValues<StockDocumentType>(), data.NumberSeries.Select(s => s.DocumentType).Order());
        Assert.All(data.NumberSeries, series => Assert.Equal(SeedDataGenerator.OpeningDate, series.ValidFrom));
    }
}
