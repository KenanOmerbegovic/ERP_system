using Erp.Messaging.Outbox;
using Erp.Modules.Core.Contracts.Articles;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.CreditNotes;
using Erp.Modules.Sales.Application.Deliveries;
using Erp.Modules.Sales.Application.Invoices;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Application.Offers;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Application.References;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Infrastructure;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Contracts.Commands;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Messaging;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarehouseArticleCreatedHandler = Erp.Modules.Warehouse.Application.Articles.ArticleCreatedHandler;

namespace Erp.Modules.Sales.Tests.TestSupport;

/// <summary>
/// A fresh company in the shared database, with helpers that set up customers, articles, stock and documents through
/// the modules' handlers. Each call runs in its own scope, like an HTTP request.
/// </summary>
internal sealed class SalesScenario(SalesHost host)
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

    public Task<T> QueryAsync<T>(Func<SalesDbContext, Task<T>> query) =>
        InScopeAsync(services => query(services.GetRequiredService<SalesDbContext>()));

    /// <summary>A business partner as Core announces it.</summary>
    public async Task<Guid> CustomerAsync(string code = "K001", bool isCustomer = true, bool isActive = true)
    {
        var id = Guid.CreateVersion7();
        await RunAsync<SalesBusinessPartnerCreatedHandler, int>(async h =>
        {
            await h.HandleAsync(
                new BusinessPartnerCreated(Guid.CreateVersion7(), DateTimeOffset.UtcNow, CompanyId.Value, id, code, $"Kupac {code}", "4200000000001", null, "BA",
                    isCustomer, IsSupplier: false, isActive),
                CancellationToken.None);
            return 0;
        });
        return id;
    }

    /// <summary>An article as Core announces it, delivered to both Warehouse and Sales.</summary>
    public async Task<Guid> ArticleAsync(ArticleTracking tracking = ArticleTracking.None, string? vatCode = "PDV17", ArticleKind kind = ArticleKind.Goods)
    {
        var id = Guid.CreateVersion7();
        var created = new ArticleCreated(
            Guid.CreateVersion7(), DateTimeOffset.UtcNow, CompanyId.Value, id, "A" + id.ToString("N")[^8..], "Artikal", kind,
            kind == ArticleKind.Service ? ArticleTracking.None : tracking, Guid.NewGuid(), true, vatCode);
        if (kind == ArticleKind.Goods)
        {
            await RunAsync<WarehouseArticleCreatedHandler, int>(async h => { await h.HandleAsync(created, CancellationToken.None); return 0; });
        }

        await RunAsync<SalesArticleCreatedHandler, int>(async h => { await h.HandleAsync(created, CancellationToken.None); return 0; });
        return id;
    }

    public Task<Guid> ServiceAsync(string? vatCode = "PDV17") => ArticleAsync(vatCode: vatCode, kind: ArticleKind.Service);

    /// <summary>A warehouse, with Warehouse's number series (needed to issue and receive stock).</summary>
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

    /// <summary>Puts goods into stock (as Purchasing would) at a unit cost.</summary>
    public async Task StockInAsync(Guid warehouse, Guid article, decimal quantity, decimal unitCost, string? lot = null, params string[] serials)
    {
        var result = await RunAsync<IStockDocumentCommands, Result<StockDocumentReference>>(h => h.ReceiveAsync(
            new StockReceiptCommand(new IdempotencyKey("test", Guid.CreateVersion7()), CompanyId, warehouse, Today, null,
                [new StockReceiptCommandLine(article, quantity, unitCost, lot, null, serials)]),
            CancellationToken.None));
        ResultAssert.Succeeds(result);
    }

    public async Task NumberSeriesAsync()
    {
        var validFrom = new DateOnly(Today.Year, 1, 1);
        foreach (var (type, prefix) in new[]
                 {
                     (SalesDocumentType.Offer, "PON-"), (SalesDocumentType.SalesOrder, "NRK-"), (SalesDocumentType.DeliveryNote, "OTP-"),
                     (SalesDocumentType.SalesInvoice, "IFK-"), (SalesDocumentType.CreditNote, "KNO-"),
                 })
        {
            ResultAssert.Succeeds(await RunAsync<CreateSalesNumberSeriesHandler, Result<Guid>>(h =>
                h.HandleAsync(new CreateSalesNumberSeries(CompanyId, type, validFrom, prefix, 5), CancellationToken.None)));
        }
    }

    /// <summary>Customer, warehouse and number series: everything an order needs.</summary>
    public async Task<(Guid Customer, Guid Warehouse)> SetUpAsync()
    {
        await NumberSeriesAsync();
        return (await CustomerAsync(), await WarehouseAsync());
    }

    public Task<Result<Guid>> DraftOfferAsync(Guid customer, params SalesDraftLine[] lines) =>
        RunAsync<CreateOfferHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateOffer(CompanyId, customer, Today, Today.AddDays(15), null, lines), CancellationToken.None));

    public Task<Result> SendOfferAsync(Guid offer) =>
        RunAsync<SendOfferHandler, Result>(h => h.HandleAsync(new SendOffer(CompanyId, offer), CancellationToken.None));

    public Task<Result<Guid>> AcceptOfferAsync(Guid offer, Guid? warehouse = null) =>
        RunAsync<AcceptOfferHandler, Result<Guid>>(h => h.HandleAsync(new AcceptOffer(CompanyId, offer, Today, warehouse), CancellationToken.None));

    public Task<Result<Guid>> DraftOrderAsync(Guid customer, Guid? warehouse, params SalesDraftLine[] lines) =>
        RunAsync<CreateSalesOrderHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateSalesOrder(CompanyId, customer, Today, warehouse, null, lines), CancellationToken.None));

    public Task<Result> ConfirmOrderAsync(Guid order) =>
        RunAsync<ConfirmSalesOrderHandler, Result>(h => h.HandleAsync(new ConfirmSalesOrder(CompanyId, order), CancellationToken.None));

    public async Task<Guid> OrderAsync(Guid customer, Guid? warehouse, params SalesDraftLine[] lines)
    {
        var draft = await DraftOrderAsync(customer, warehouse, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmOrderAsync(draft.Value));
        return draft.Value;
    }

    public Task<IReadOnlyList<Guid>> OrderLinesAsync(Guid orderId) =>
        QueryAsync<IReadOnlyList<Guid>>(async db => await db.SalesOrders.Where(o => o.Id == orderId).SelectMany(o => o.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync());

    public Task<Result<Guid>> DraftDeliveryAsync(Guid order, Guid warehouse, params DeliveryNoteLineInput[] lines) =>
        RunAsync<CreateDeliveryNoteHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateDeliveryNote(CompanyId, order, warehouse, Today, null, lines), CancellationToken.None));

    public Task<Result> ConfirmDeliveryAsync(Guid deliveryNote) =>
        RunAsync<ConfirmDeliveryNoteHandler, Result>(h => h.HandleAsync(new ConfirmDeliveryNote(CompanyId, deliveryNote), CancellationToken.None));

    public async Task<Guid> DeliverAsync(Guid order, Guid warehouse, params DeliveryNoteLineInput[] lines)
    {
        var draft = await DraftDeliveryAsync(order, warehouse, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmDeliveryAsync(draft.Value));
        return draft.Value;
    }

    public Task<IReadOnlyList<Guid>> DeliveryLinesAsync(Guid deliveryNoteId) =>
        QueryAsync<IReadOnlyList<Guid>>(async db =>
            await db.DeliveryNotes.Where(d => d.Id == deliveryNoteId).SelectMany(d => d.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync());

    public Task<Result<Guid>> DraftInvoiceAsync(Guid order, params SalesInvoiceDraftLine[] lines) =>
        RunAsync<CreateSalesInvoiceHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateSalesInvoice(CompanyId, order, Today, Today.AddDays(15), null, lines), CancellationToken.None));

    public Task<Result> ConfirmInvoiceAsync(Guid invoice) =>
        RunAsync<ConfirmSalesInvoiceHandler, Result>(h => h.HandleAsync(new ConfirmSalesInvoice(CompanyId, invoice), CancellationToken.None));

    public async Task<Guid> InvoiceAsync(Guid order, params SalesInvoiceDraftLine[] lines)
    {
        var draft = await DraftInvoiceAsync(order, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmInvoiceAsync(draft.Value));
        return draft.Value;
    }

    public Task<IReadOnlyList<Guid>> InvoiceLinesAsync(Guid invoiceId) =>
        QueryAsync<IReadOnlyList<Guid>>(async db =>
            await db.SalesInvoices.Where(i => i.Id == invoiceId).SelectMany(i => i.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync());

    public Task<Result<Guid>> DraftCreditNoteAsync(Guid invoice, Guid? warehouse, params CreditNoteDraftLine[] lines) =>
        RunAsync<CreateCreditNoteHandler, Result<Guid>>(h => h.HandleAsync(
            new CreateCreditNote(CompanyId, invoice, Today, warehouse, null, lines), CancellationToken.None));

    public Task<Result> ConfirmCreditNoteAsync(Guid creditNote) =>
        RunAsync<ConfirmCreditNoteHandler, Result>(h => h.HandleAsync(new ConfirmCreditNote(CompanyId, creditNote), CancellationToken.None));

    public async Task<Guid> CreditAsync(Guid invoice, Guid? warehouse, params CreditNoteDraftLine[] lines)
    {
        var draft = await DraftCreditNoteAsync(invoice, warehouse, lines);
        ResultAssert.Succeeds(draft);
        ResultAssert.Succeeds(await ConfirmCreditNoteAsync(draft.Value));
        return draft.Value;
    }

    /// <summary>Stock of an article in a warehouse, read through Warehouse's own query (FR-WH-04).</summary>
    public async Task<(decimal Quantity, decimal Value)> StockAsync(Guid warehouse, Guid article)
    {
        var result = await RunAsync<ListStockBalancesHandler, Result<PagedResponse<StockBalanceResponse>>>(h =>
            h.HandleAsync(new ListStockBalances(CompanyId, warehouse, article, null, true, 1, 10), CancellationToken.None));
        var balance = result.Value.Items.SingleOrDefault();
        return balance is null ? (0m, 0m) : (balance.Quantity, balance.Value);
    }

    /// <summary>The integration events this company's Sales documents published, oldest first.</summary>
    public Task<IReadOnlyList<T>> EventsAsync<T>()
        where T : IIntegrationEvent =>
        QueryAsync<IReadOnlyList<T>>(async db =>
        {
            var type = typeof(T).FullName!;
            var messages = await db.Set<OutboxMessage>().AsNoTracking().Where(m => m.Type == type).OrderBy(m => m.OccurredAt).ToListAsync();
            return messages.Select(m => System.Text.Json.JsonSerializer.Deserialize<T>(m.Content, System.Text.Json.JsonSerializerOptions.Web)!)
                .Where(e => CompanyOf(e) == CompanyId.Value)
                .ToList();
        });

    public static SalesDraftLine Line(Guid article, decimal quantity, decimal unitPrice, string? vatCode = null) => new(article, quantity, unitPrice, vatCode);

    public static DeliveryNoteLineInput Delivery(Guid orderLine, decimal quantity, string? lot = null, params string[] serials) => new(orderLine, quantity, lot, serials);

    public static SalesInvoiceDraftLine Goods(Guid deliveryLine, decimal quantity) => new(deliveryLine, null, quantity);

    public static SalesInvoiceDraftLine Service(Guid orderLine, decimal quantity) => new(null, orderLine, quantity);

    public static CreditNoteDraftLine Return(Guid invoiceLine, decimal quantity, params string[] serials) => new(invoiceLine, CreditKind.Return, quantity, null, serials);

    public static CreditNoteDraftLine Reduction(Guid invoiceLine, decimal amount) => new(invoiceLine, CreditKind.ValueReduction, null, amount, []);

    private static Guid CompanyOf(object integrationEvent) => (Guid)integrationEvent.GetType().GetProperty("CompanyId")!.GetValue(integrationEvent)!;
}
