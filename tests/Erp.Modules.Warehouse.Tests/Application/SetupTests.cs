using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Tests.Application;

/// <summary>Warehouses, number series and Warehouse's copy of Core's articles.</summary>
[Collection(WarehouseCollection.Name)]
public sealed class SetupTests(WarehouseDatabase database)
{
    private readonly WarehouseScenario _scenario = new(database);

    [Fact]
    public async Task FR_WH_01_warehouses_are_created_updated_and_listed()
    {
        var id = await _scenario.WarehouseAsync("MP2", "Magacin 2");
        await _scenario.WarehouseAsync("MP1", "Magacin 1");

        var update = await _scenario.RunAsync(h => h.UpdateWarehouse.HandleAsync(
            new UpdateWarehouse(_scenario.CompanyId, id, " MP2 ", "Skladište Tuzla", IsActive: false), CancellationToken.None));
        var list = await _scenario.RunAsync(h => h.ListWarehouses.HandleAsync(new ListWarehouses(_scenario.CompanyId), CancellationToken.None));

        ResultAssert.Succeeds(update);
        Assert.Equal([("MP1", "Magacin 1", true), ("MP2", "Skladište Tuzla", false)], list.Select(w => (w.Code, w.Name, w.IsActive)));
    }

    [Fact]
    public async Task FR_WH_01_warehouse_code_is_unique_per_company_and_validated()
    {
        await _scenario.WarehouseAsync("MP1");
        var other = new WarehouseScenario(database);

        var duplicate = await _scenario.RunAsync(h => h.CreateWarehouse.HandleAsync(new CreateWarehouse(_scenario.CompanyId, "MP1", "X", true), CancellationToken.None));
        var blank = await _scenario.RunAsync(h => h.CreateWarehouse.HandleAsync(new CreateWarehouse(_scenario.CompanyId, "", "X", true), CancellationToken.None));
        var otherCompany = await other.RunAsync(h => h.CreateWarehouse.HandleAsync(new CreateWarehouse(other.CompanyId, "MP1", "X", true), CancellationToken.None));
        var missing = await _scenario.RunAsync(h => h.UpdateWarehouse.HandleAsync(new UpdateWarehouse(_scenario.CompanyId, Guid.NewGuid(), "MP3", "X", true), CancellationToken.None));

        ResultAssert.Fails(duplicate, WarehouseErrors.DuplicateWarehouseCode);
        ResultAssert.FailsValidation(blank, "Code", "NotEmptyValidator");
        ResultAssert.Succeeds(otherCompany);
        ResultAssert.Fails(missing, WarehouseErrors.WarehouseNotFound);
    }

    [Fact]
    public async Task FR_WH_01_number_series_are_unique_per_type_and_start_date()
    {
        var validFrom = new DateOnly(2026, 1, 1);
        var create = (string prefix, int padding) => _scenario.RunAsync(h => h.CreateNumberSeries.HandleAsync(
            new CreateNumberSeries(_scenario.CompanyId, StockDocumentType.Receipt, validFrom, prefix, padding), CancellationToken.None));

        ResultAssert.Succeeds(await create("PRM-", 5));
        ResultAssert.Fails(await create("P-", 5), WarehouseErrors.DuplicateNumberSeries);
        ResultAssert.FailsValidation(await create("P-", 0), "Padding", "InclusiveBetweenValidator");
        var list = await _scenario.RunAsync(h => h.ListNumberSeries.HandleAsync(new ListNumberSeries(_scenario.CompanyId), CancellationToken.None));
        Assert.Equal((StockDocumentKind.Receipt, "PRM-", 5), (Assert.Single(list).DocumentType, list[0].Prefix, list[0].Padding));
    }

    [Fact]
    public async Task Article_events_keep_warehouse_copy_in_sync_and_are_idempotent()
    {
        var id = Guid.CreateVersion7();
        var created = new ArticleCreated(Guid.CreateVersion7(), _scenario.Clock.UtcNow, _scenario.CompanyId.Value, id, "A1", "Laptop",
            ArticleKind.Goods, ArticleTracking.Serial, Guid.NewGuid(), true);
        await _scenario.RunAsync(async h => { await h.ArticleCreated.HandleAsync(created, CancellationToken.None); return 0; });
        await _scenario.RunAsync(async h => { await h.ArticleCreated.HandleAsync(created, CancellationToken.None); return 0; });

        await _scenario.RunAsync(async h =>
        {
            await h.ArticleUpdated.HandleAsync(
                new ArticleUpdated(Guid.CreateVersion7(), _scenario.Clock.UtcNow, created.CompanyId, id, "A1", "Laptop 15\"",
                    ArticleKind.Goods, ArticleTracking.Serial, created.UnitOfMeasureId, false),
                CancellationToken.None);
            return 0;
        });

        var article = await _scenario.QueryAsync(db => db.Articles.SingleAsync(a => a.Id == id));
        Assert.Equal(("Laptop 15\"", TrackingType.Serial, false), (article.Name, article.Tracking, article.IsActive));
        Assert.False(article.IsStockable);
    }

    [Fact]
    public async Task Article_update_creates_an_article_whose_creation_was_missed()
    {
        var id = Guid.CreateVersion7();

        await _scenario.RunAsync(async h =>
        {
            await h.ArticleUpdated.HandleAsync(
                new ArticleUpdated(Guid.CreateVersion7(), _scenario.Clock.UtcNow, _scenario.CompanyId.Value, id, "U1", "Montaža",
                    ArticleKind.Service, ArticleTracking.None, Guid.NewGuid(), true),
                CancellationToken.None);
            return 0;
        });

        var article = await _scenario.QueryAsync(db => db.Articles.SingleAsync(a => a.Id == id));
        Assert.False(article.IsGoods);
    }
}
