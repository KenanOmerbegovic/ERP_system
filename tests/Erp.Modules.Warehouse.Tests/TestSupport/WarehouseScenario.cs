using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Warehouse.Application.Articles;
using Erp.Modules.Warehouse.Application.Commands;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Infrastructure;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Tests.TestSupport;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

/// <summary>Handlers on one context, as one HTTP request gets them.</summary>
internal sealed class WarehouseHandlers(WarehouseDbContext db, IClock clock)
{
    public WarehouseDbContext Db => db;

    public CreateWarehouseHandler CreateWarehouse => new(db, new CreateWarehouseValidator());

    public UpdateWarehouseHandler UpdateWarehouse => new(db, new UpdateWarehouseValidator());

    public ListWarehousesHandler ListWarehouses => new(db);

    public ArticleCreatedHandler ArticleCreated => new(db);

    public ArticleUpdatedHandler ArticleUpdated => new(db);

    public CreateNumberSeriesHandler CreateNumberSeries => new(db, new CreateNumberSeriesValidator());

    public ListNumberSeriesHandler ListNumberSeries => new(db);

    public CreateStockDocumentHandler CreateStockDocument => new(db, new CreateStockDocumentValidator(clock), clock);

    public UpdateStockDocumentHandler UpdateStockDocument => new(db, new StockLocks(db), new UpdateStockDocumentValidator(clock));

    public DeleteStockDocumentHandler DeleteStockDocument => new(db, new StockLocks(db));

    public GetStockDocumentHandler GetStockDocument => new(db);

    public ListStockDocumentsHandler ListStockDocuments => new(db, new ListStockDocumentsValidator());

    public ConfirmStockDocumentHandler ConfirmStockDocument => new(db, new StockLocks(db), clock);

    public StornoStockDocumentHandler StornoStockDocument => new(db, new StockLocks(db), clock);

    public ListStockBalancesHandler ListStockBalances => new(db, new ListStockBalancesValidator());

    public ListSerialNumbersHandler ListSerialNumbers => new(db, new ListSerialNumbersValidator());

    public StockDocumentCommands Commands => new(db, new StockLocks(db), clock, new CreateStockDocumentValidator(clock));
}

/// <summary>
/// A fresh company in the shared database, with helpers that create prerequisite data through the handlers.
/// Every call runs on a new context, so assertions see what was saved.
/// </summary>
internal sealed class WarehouseScenario(WarehouseDatabase database)
{
    public CompanyId CompanyId { get; } = CompanyId.New();

    public FakeClock Clock { get; } = new();

    public DateOnly Today => Clock.Today;

    public async Task<T> RunAsync<T>(Func<WarehouseHandlers, Task<T>> action)
    {
        await using var db = database.CreateContext();
        return await action(new WarehouseHandlers(db, Clock));
    }

    public async Task<T> QueryAsync<T>(Func<WarehouseDbContext, Task<T>> query)
    {
        await using var db = database.CreateContext();
        return await query(db);
    }

    public Task<Guid> WarehouseAsync(string code = "MP1", string name = "Magacin", bool isActive = true) =>
        RunAsync(async h => (await h.CreateWarehouse.HandleAsync(new CreateWarehouse(CompanyId, code, name, isActive), CancellationToken.None)).Value);

    /// <summary>An article as Core announces it.</summary>
    public async Task<Guid> ArticleAsync(
        ArticleTracking tracking = ArticleTracking.None, string? code = null, ArticleKind kind = ArticleKind.Goods, bool isActive = true)
    {
        var id = Guid.CreateVersion7();
        await RunAsync(async h =>
        {
            await h.ArticleCreated.HandleAsync(
                new ArticleCreated(Guid.CreateVersion7(), Clock.UtcNow, CompanyId.Value, id, code ?? "A" + id.ToString("N")[^8..], "Artikal", kind, tracking, Guid.NewGuid(), isActive),
                CancellationToken.None);
            return id;
        });
        return id;
    }

    /// <summary>Number series for every document type, valid from the start of the year.</summary>
    public async Task NumberSeriesAsync()
    {
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var (type, prefix) in new[] { (StockDocumentType.Receipt, "PRM-"), (StockDocumentType.Issue, "IZD-"), (StockDocumentType.Transfer, "MSK-") })
        {
            var result = await RunAsync(h => h.CreateNumberSeries.HandleAsync(new CreateNumberSeries(CompanyId, type, validFrom, prefix, 5), CancellationToken.None));
            ResultAssert.Succeeds(result);
        }
    }

    public Task<Result<Guid>> DraftAsync(StockDocumentType type, Guid? source, Guid? target, params StockLineInput[] lines) =>
        RunAsync(h => h.CreateStockDocument.HandleAsync(
            new CreateStockDocument(CompanyId, type, Today, source, target, null, lines), CancellationToken.None));

    public Task<Result> ConfirmAsync(Guid documentId) =>
        RunAsync(h => h.ConfirmStockDocument.HandleAsync(new ConfirmStockDocument(CompanyId, documentId), CancellationToken.None));

    public Task<Result<Guid>> StornoAsync(Guid documentId) =>
        RunAsync(h => h.StornoStockDocument.HandleAsync(new StornoStockDocument(CompanyId, documentId), CancellationToken.None));

    /// <summary>Creates and confirms a document; fails the test if either step fails.</summary>
    public async Task<Guid> PostAsync(StockDocumentType type, Guid? source, Guid? target, params StockLineInput[] lines)
    {
        var draft = await DraftAsync(type, source, target, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmAsync(draft.Value));
        return draft.Value;
    }

    public Task<Guid> ReceiveAsync(Guid warehouseId, params StockLineInput[] lines) => PostAsync(StockDocumentType.Receipt, null, warehouseId, lines);

    public Task<Guid> IssueAsync(Guid warehouseId, params StockLineInput[] lines) => PostAsync(StockDocumentType.Issue, warehouseId, null, lines);

    public Task<StockItem?> StockAsync(Guid warehouseId, Guid articleId) =>
        QueryAsync(db => db.StockItems.AsNoTracking()
            .SingleOrDefaultAsync(i => i.CompanyId == CompanyId && i.StorehouseId == warehouseId && i.ArticleId == articleId));

    public Task<StockDocument> DocumentAsync(Guid documentId) =>
        QueryAsync(db => db.StockDocuments.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.Id == documentId));

    public static StockLineInput Line(Guid articleId, decimal quantity, decimal? unitCost = null, string? lot = null, DateOnly? expiry = null, params string[] serials) =>
        new(articleId, quantity, unitCost, lot, expiry, serials);
}
