using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Purchasing.Application.Invoices;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Application.Orders;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Application.References;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Purchasing.Infrastructure;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarehouseArticleCreatedHandler = Erp.Modules.Warehouse.Application.Articles.ArticleCreatedHandler;

namespace Erp.Modules.Purchasing.Tests.TestSupport;

/// <summary>
/// A fresh company in the shared database, with helpers that set up suppliers, articles, warehouses and documents through
/// the modules' handlers. Each call runs in its own scope, like an HTTP request.
/// </summary>
internal sealed class PurchasingScenario(PurchasingHost host)
{
    public CompanyId CompanyId { get; } = CompanyId.New();

    public DateOnly Today => host.Clock.Today;

    public async Task<T> RunAsync<THandler, T>(Func<THandler, Task<T>> action)
        where THandler : notnull
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task<T> QueryAsync<T>(Func<PurchasingDbContext, Task<T>> query) =>
        InScopeAsync(services => query(services.GetRequiredService<PurchasingDbContext>()));

    /// <summary>A business partner as Core announces it.</summary>
    public async Task<Guid> SupplierAsync(string code = "D001", bool isSupplier = true, bool isActive = true)
    {
        var id = Guid.CreateVersion7();
        await RunAsync<BusinessPartnerCreatedHandler, int>(async h =>
        {
            await h.HandleAsync(
                new BusinessPartnerCreated(Guid.CreateVersion7(), DateTimeOffset.UtcNow, CompanyId.Value, id, code, $"Dobavljač {code}", "4200000000001", null, "BA",
                    IsCustomer: false, isSupplier, isActive),
                CancellationToken.None);
            return 0;
        });
        return id;
    }

    /// <summary>An article as Core announces it, delivered to both Warehouse and Purchasing.</summary>
    public async Task<Guid> ArticleAsync(ArticleTracking tracking = ArticleTracking.None, string? vatCode = "PDV17", ArticleKind kind = ArticleKind.Goods, string? code = null)
    {
        var id = Guid.CreateVersion7();
        var created = new ArticleCreated(
            Guid.CreateVersion7(), DateTimeOffset.UtcNow, CompanyId.Value, id, code ?? "A" + id.ToString("N")[^8..], "Artikal", kind, tracking, Guid.NewGuid(), true, vatCode);
        await RunAsync<WarehouseArticleCreatedHandler, int>(async h => { await h.HandleAsync(created, CancellationToken.None); return 0; });
        await RunAsync<PurchasingArticleCreatedHandler, int>(async h => { await h.HandleAsync(created, CancellationToken.None); return 0; });
        return id;
    }

    /// <summary>A warehouse, with Warehouse's number series (needed to confirm stock receipts).</summary>
    public async Task<Guid> WarehouseAsync(string code = "MP1")
    {
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var (type, prefix) in new[] { (StockDocumentType.Receipt, "PRM-"), (StockDocumentType.Issue, "IZD-"), (StockDocumentType.Transfer, "MSK-") })
        {
            await RunAsync<CreateNumberSeriesHandler, Result<Guid>>(h => h.HandleAsync(new CreateNumberSeries(CompanyId, type, validFrom, prefix, 5), CancellationToken.None));
        }

        var result = await RunAsync<CreateWarehouseHandler, Result<Guid>>(h => h.HandleAsync(new CreateWarehouse(CompanyId, code, $"Magacin {code}", true), CancellationToken.None));
        ResultAssert.Succeeds(result);
        return result.Value;
    }

    public async Task NumberSeriesAsync()
    {
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var (type, prefix) in new[] { (PurchasingDocumentType.PurchaseOrder, "NAR-"), (PurchasingDocumentType.GoodsReceipt, "PRI-"), (PurchasingDocumentType.SupplierInvoice, "ULF-") })
        {
            ResultAssert.Succeeds(await RunAsync<CreatePurchasingNumberSeriesHandler, Result<Guid>>(h =>
                h.HandleAsync(new CreatePurchasingNumberSeries(CompanyId, type, validFrom, prefix, 5), CancellationToken.None)));
        }
    }

    /// <summary>Supplier, articles, a warehouse and number series: everything an order needs.</summary>
    public async Task<(Guid Supplier, Guid Warehouse)> SetUpAsync()
    {
        await NumberSeriesAsync();
        return (await SupplierAsync(), await WarehouseAsync());
    }

    public Task<Result<Guid>> DraftOrderAsync(Guid supplier, Guid? warehouse, params PurchaseOrderDraftLine[] lines) =>
        RunAsync<CreatePurchaseOrderHandler, Result<Guid>>(h => h.HandleAsync(
            new CreatePurchaseOrder(CompanyId, supplier, Today, null, warehouse, null, lines), CancellationToken.None));

    public async Task<Guid> OrderAsync(Guid supplier, Guid? warehouse, params PurchaseOrderDraftLine[] lines)
    {
        var draft = await DraftOrderAsync(supplier, warehouse, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await RunAsync<ConfirmPurchaseOrderHandler, Result>(h => h.HandleAsync(new ConfirmPurchaseOrder(CompanyId, draft.Value), CancellationToken.None)));
        return draft.Value;
    }

    public Task<IReadOnlyList<Guid>> OrderLinesAsync(Guid orderId) =>
        QueryAsync<IReadOnlyList<Guid>>(async db => await db.PurchaseOrders.Where(o => o.Id == orderId).SelectMany(o => o.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync());

    public Task<Result<Guid>> DraftReceiptAsync(Guid order, Guid warehouse, params GoodsReceiptLineInput[] lines) =>
        RunAsync<CreateGoodsReceiptHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateGoodsReceipt(CompanyId, order, warehouse, Today, null, lines), CancellationToken.None));

    public Task<Result> ConfirmReceiptAsync(Guid receipt) =>
        RunAsync<ConfirmGoodsReceiptHandler, Result>(h => h.HandleAsync(new ConfirmGoodsReceipt(CompanyId, receipt), CancellationToken.None));

    public async Task<Guid> ReceiveAsync(Guid order, Guid warehouse, params GoodsReceiptLineInput[] lines)
    {
        var draft = await DraftReceiptAsync(order, warehouse, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmReceiptAsync(draft.Value));
        return draft.Value;
    }

    public Task<IReadOnlyList<Guid>> ReceiptLinesAsync(Guid receiptId) =>
        QueryAsync<IReadOnlyList<Guid>>(async db => await db.GoodsReceipts.Where(r => r.Id == receiptId).SelectMany(r => r.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync());

    public Task<Result<Guid>> DraftInvoiceAsync(Guid order, string supplierNumber, params SupplierInvoiceDraftLine[] lines) =>
        RunAsync<CreateSupplierInvoiceHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateSupplierInvoice(CompanyId, order, supplierNumber, Today, Today.AddDays(30), null, lines), CancellationToken.None));

    public Task<Result> ConfirmInvoiceAsync(Guid invoice) =>
        RunAsync<ConfirmSupplierInvoiceHandler, Result>(h => h.HandleAsync(new ConfirmSupplierInvoice(CompanyId, invoice), CancellationToken.None));

    /// <summary>Stock of an article in a warehouse, read through Warehouse's own query (FR-WH-04).</summary>
    public async Task<(decimal Quantity, decimal Value)> StockAsync(Guid warehouse, Guid article)
    {
        var result = await RunAsync<ListStockBalancesHandler, Result<Erp.Modules.Core.Contracts.Common.PagedResponse<Erp.Modules.Warehouse.Contracts.Stock.StockBalanceResponse>>>(h =>
            h.HandleAsync(new ListStockBalances(CompanyId, warehouse, article, null, true, 1, 10), CancellationToken.None));
        var balance = result.Value.Items.SingleOrDefault();
        return balance is null ? (0m, 0m) : (balance.Quantity, balance.Value);
    }

    public static PurchaseOrderDraftLine OrderLine(Guid article, decimal quantity, decimal unitPrice, string? vatCode = null) => new(article, quantity, unitPrice, vatCode);

    public static GoodsReceiptLineInput ReceiptLine(Guid orderLine, decimal quantity, string? lot = null, params string[] serials) =>
        new(orderLine, quantity, lot, null, serials);
}
