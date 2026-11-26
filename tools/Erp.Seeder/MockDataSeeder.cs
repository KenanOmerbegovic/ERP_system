using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Core.Application.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Invoices;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Application.Orders;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.CreditNotes;
using Erp.Modules.Sales.Application.Deliveries;
using Erp.Modules.Sales.Application.Invoices;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Application.Offers;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Contracts.Users;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Seeder.Data;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Erp.Seeder;

internal sealed record SeededCompany(
    CompanyId CompanyId,
    Guid AdministratorId,
    IReadOnlyDictionary<string, Guid> RoleIds,
    IReadOnlyDictionary<string, Guid> ArticleIds,
    IReadOnlyDictionary<string, Guid> WarehouseIds,
    IReadOnlyList<SeededPurchaseOrder> PurchaseOrders,
    IReadOnlyList<SeededSalesOrder> SalesOrders);

/// <summary>A seeded order with its receipt and invoice, when the scenario made them.</summary>
internal sealed record SeededPurchaseOrder(PurchaseOrderSeed Seed, Guid OrderId, Guid? ReceiptId, Guid? InvoiceId);

/// <summary>A seeded sales order with the documents its scenario made.</summary>
internal sealed record SeededSalesOrder(SalesOrderSeed Seed, Guid OrderId, Guid? DeliveryNoteId, Guid? InvoiceId, Guid? CreditNoteId, Guid? InvoiceStornoId);

/// <summary>
/// Creates the mock data only through Application commands, in the order a real company would:
/// registration key, administrator account, company, then roles, users, master data, warehouses and opening stock,
/// then a purchasing scenario (orders, receipts that move stock, supplier invoices) and last a sales scenario (offers,
/// orders, deliveries that issue stock, invoices, credit notes and stornos).
/// Each command runs in its own scope, like an HTTP request.
/// </summary>
internal sealed class MockDataSeeder(IServiceProvider services)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    public async Task<SeededCompany> SeedAsync(SeedData data, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        var key = await SendAsync<IssueRegistrationKeyHandler, IssuedRegistrationKey>(
            (handler, ct) => handler.HandleAsync(new IssueRegistrationKey(), ct), "Issue registration key", cancellationToken);
        var tokens = await SendAsync<RegisterAccountHandler, AuthTokens>(
            (handler, ct) => handler.HandleAsync(
                new RegisterAccount(key.Key, data.Administrator.Email, data.Administrator.DisplayName, password), ct),
            $"Register {data.Administrator.Email}",
            cancellationToken);

        // The same id the API reads from the access token's "sub" claim.
        var administratorId = Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(tokens.AccessToken).Subject);

        var company = data.Company;
        var companyId = await SendAsync<CreateCompanyHandler, CompanyId>(
            (handler, ct) => handler.HandleAsync(
                new CreateCompany(administratorId, company.Name, company.TaxId, company.VatNumber, company.Address, company.PostalCode, company.City), ct),
            $"Create company {company.Name}",
            cancellationToken);
        await WaitForMembershipAsync(administratorId, companyId, cancellationToken);

        var roleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var role in data.Roles)
        {
            roleIds[role.Name] = await SendAsync<CreateRoleHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateRole(companyId, role.Name, role.Permissions), ct), $"Create role {role.Name}", cancellationToken);
            await SendAsync<CreateUserHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateUser(companyId, role.User.Email, role.User.DisplayName, password, [roleIds[role.Name]]), ct),
                $"Create user {role.User.Email}",
                cancellationToken);
        }

        foreach (var rate in data.VatRates)
        {
            await SendAsync<CreateVatRateHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateVatRate(companyId, rate.Code, rate.Name, rate.Rate, rate.ValidFrom), ct),
                $"Create VAT rate {rate.Code}",
                cancellationToken);
        }

        var unitIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var unit in data.Units)
        {
            unitIds[unit.Code] = await SendAsync<CreateUnitOfMeasureHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateUnitOfMeasure(companyId, unit.Code, unit.Name), ct), $"Create unit {unit.Code}", cancellationToken);
        }

        var partnerIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var partner in data.Partners)
        {
            partnerIds[partner.Code] = await SendAsync<CreateBusinessPartnerHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateBusinessPartner(
                        companyId,
                        partner.Code,
                        partner.Name,
                        partner.TaxId,
                        partner.VatNumber,
                        partner.Address,
                        partner.PostalCode,
                        partner.City,
                        CountryCode: null,
                        partner.IsCustomer,
                        partner.IsSupplier,
                        partner.IsActive),
                    ct),
                $"Create business partner {partner.Code}",
                cancellationToken);
        }

        var articleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var article in data.Articles)
        {
            articleIds[article.Code] = await SendAsync<CreateArticleHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateArticle(companyId, article.Code, article.Name, article.Type, article.Tracking, unitIds[article.UnitCode], article.DefaultVatCode, article.IsActive), ct),
                $"Create article {article.Code}",
                cancellationToken);
        }

        var warehouseIds = await SeedWarehouseAsync(data, companyId, articleIds, cancellationToken);
        var orders = await SeedPurchasingAsync(data, companyId, partnerIds, articleIds, warehouseIds[data.Warehouses[0].Code], cancellationToken);
        var sales = await SeedSalesAsync(data, companyId, partnerIds, articleIds, warehouseIds[data.Warehouses[0].Code], cancellationToken);
        return new SeededCompany(companyId, administratorId, roleIds, articleIds, warehouseIds, orders, sales);
    }

    /// <summary>Number series, then each order confirmed, received (fully or partly) and invoiced as its seed says.</summary>
    private async Task<IReadOnlyList<SeededPurchaseOrder>> SeedPurchasingAsync(
        SeedData data,
        CompanyId companyId,
        IReadOnlyDictionary<string, Guid> partnerIds,
        IReadOnlyDictionary<string, Guid> articleIds,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        foreach (var series in data.PurchasingNumberSeries)
        {
            await SendAsync<CreatePurchasingNumberSeriesHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreatePurchasingNumberSeries(companyId, series.DocumentType, series.ValidFrom, series.Prefix, series.Padding), ct),
                $"Create purchasing number series {series.Prefix}",
                cancellationToken);
        }

        await WaitForPurchasingReferencesAsync(companyId, partnerIds.Count, articleIds.Count, cancellationToken);

        var seeded = new List<SeededPurchaseOrder>();
        foreach (var (order, index) in data.PurchaseOrders.Select((order, index) => (order, index + 1)))
        {
            var orderId = await SendAsync<CreatePurchaseOrderHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreatePurchaseOrder(
                        companyId, partnerIds[order.SupplierCode], order.OrderDate, null, warehouseId, null,
                        order.Lines.Select(line => new PurchaseOrderDraftLine(articleIds[line.ArticleCode], line.Quantity, line.UnitPrice, null)).ToList()),
                    ct),
                $"Create purchase order {index}",
                cancellationToken);
            await SendAsync<ConfirmPurchaseOrderHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmPurchaseOrder(companyId, orderId), ct), $"Confirm purchase order {index}", cancellationToken);
            if (order.ReceivedShare == 0m)
            {
                seeded.Add(new SeededPurchaseOrder(order, orderId, null, null));
                continue;
            }

            var orderLineIds = await PurchaseOrderLineIdsAsync(orderId, cancellationToken);
            var receiptId = await SendAsync<CreateGoodsReceiptHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateGoodsReceipt(
                        companyId, orderId, warehouseId, order.ReceiptDate, null,
                        order.Lines.Select((line, i) => new GoodsReceiptLineInput(orderLineIds[i], line.ReceivedQuantity(order.ReceivedShare), line.LotNumber, null, []))
                            .Where(line => line.Quantity > 0m)
                            .ToList()),
                    ct),
                $"Create goods receipt for order {index}",
                cancellationToken);
            await SendAsync<ConfirmGoodsReceiptHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmGoodsReceipt(companyId, receiptId), ct), $"Confirm goods receipt for order {index}", cancellationToken);
            if (order.SupplierInvoiceNumber is null)
            {
                seeded.Add(new SeededPurchaseOrder(order, orderId, receiptId, null));
                continue;
            }

            var receiptLineIds = await ReceiptLineIdsAsync(receiptId, cancellationToken);
            var invoiceId = await SendAsync<CreateSupplierInvoiceHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateSupplierInvoice(
                        companyId, orderId, order.SupplierInvoiceNumber, order.InvoiceDate, order.InvoiceDate.AddDays(30), null,
                        order.Lines.Select((line, i) => new SupplierInvoiceDraftLine(
                            receiptLineIds[i], line.Quantity, i == 0 ? Math.Round(line.UnitPrice * order.InvoicePriceFactor, 2) : line.UnitPrice, null)).ToList()),
                    ct),
                $"Create supplier invoice {order.SupplierInvoiceNumber}",
                cancellationToken);
            await SendAsync<ConfirmSupplierInvoiceHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmSupplierInvoice(companyId, invoiceId), ct),
                $"Register supplier invoice {order.SupplierInvoiceNumber}",
                cancellationToken);
            seeded.Add(new SeededPurchaseOrder(order, orderId, receiptId, invoiceId));
        }

        return seeded;
    }

    /// <summary>
    /// Number series, the offers that stay offers, then each sales order through its scenario: (offer, accept,) confirm,
    /// deliver from the first warehouse, invoice, and correct by a credit note or an invoice storno.
    /// </summary>
    private async Task<IReadOnlyList<SeededSalesOrder>> SeedSalesAsync(
        SeedData data,
        CompanyId companyId,
        IReadOnlyDictionary<string, Guid> partnerIds,
        IReadOnlyDictionary<string, Guid> articleIds,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        foreach (var series in data.SalesNumberSeries)
        {
            await SendAsync<CreateSalesNumberSeriesHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateSalesNumberSeries(companyId, series.DocumentType, series.ValidFrom, series.Prefix, series.Padding), ct),
                $"Create sales number series {series.Prefix}",
                cancellationToken);
        }

        await WaitForSalesReferencesAsync(companyId, partnerIds.Count, articleIds.Count, cancellationToken);
        List<SalesDraftLine> Lines(IEnumerable<SalesLineSeed> lines) =>
            lines.Select(line => new SalesDraftLine(articleIds[line.ArticleCode], line.Quantity, line.UnitPrice, null)).ToList();

        foreach (var (offer, index) in data.Offers.Select((offer, index) => (offer, index + 1)))
        {
            var offerId = await SentOfferAsync(companyId, partnerIds[offer.CustomerCode], offer.OfferDate, Lines(offer.Lines), $"offer {index}", cancellationToken);
            if (offer.Outcome == OfferOutcome.Rejected)
            {
                await SendAsync<RejectOfferHandler>((handler, ct) => handler.HandleAsync(new RejectOffer(companyId, offerId), ct), $"Reject offer {index}", cancellationToken);
            }
        }

        var seeded = new List<SeededSalesOrder>();
        foreach (var (order, index) in data.SalesOrders.Select((order, index) => (order, index + 1)))
        {
            var customerId = partnerIds[order.CustomerCode];
            Guid orderId;
            if (order.FromOffer)
            {
                var offerId = await SentOfferAsync(companyId, customerId, order.OfferDate, Lines(order.Lines), $"offer for sales order {index}", cancellationToken);
                orderId = await SendAsync<AcceptOfferHandler, Guid>(
                    (handler, ct) => handler.HandleAsync(new AcceptOffer(companyId, offerId, order.OrderDate, warehouseId), ct), $"Accept offer for sales order {index}", cancellationToken);
            }
            else
            {
                orderId = await SendAsync<CreateSalesOrderHandler, Guid>(
                    (handler, ct) => handler.HandleAsync(new CreateSalesOrder(companyId, customerId, order.OrderDate, warehouseId, null, Lines(order.Lines)), ct),
                    $"Create sales order {index}",
                    cancellationToken);
            }

            await SendAsync<ConfirmSalesOrderHandler>((handler, ct) => handler.HandleAsync(new ConfirmSalesOrder(companyId, orderId), ct), $"Confirm sales order {index}", cancellationToken);
            if (order.DeliveredShare == 0m)
            {
                seeded.Add(new SeededSalesOrder(order, orderId, null, null, null, null));
                continue;
            }

            var orderLineIds = await SalesOrderLineIdsAsync(orderId, cancellationToken);
            var deliveryId = await SendAsync<CreateDeliveryNoteHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateDeliveryNote(
                        companyId, orderId, warehouseId, order.DeliveryDate, null,
                        order.Lines.Select((line, i) => new DeliveryNoteLineInput(orderLineIds[i], SalesOrderSeed.Delivered(line, order.DeliveredShare), null, []))
                            .Where(line => line.Quantity > 0m)
                            .ToList()),
                    ct),
                $"Create delivery note for sales order {index}",
                cancellationToken);
            await SendAsync<ConfirmDeliveryNoteHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmDeliveryNote(companyId, deliveryId), ct), $"Confirm delivery note for sales order {index}", cancellationToken);
            if (!order.Invoiced)
            {
                seeded.Add(new SeededSalesOrder(order, orderId, deliveryId, null, null, null));
                continue;
            }

            var deliveryLineIds = await DeliveryLineIdsAsync(deliveryId, cancellationToken);
            var invoiceLines = new List<SalesInvoiceDraftLine>();
            var deliveryLine = 0;
            for (var i = 0; i < order.Lines.Count; i++)
            {
                var line = order.Lines[i];
                if (line.IsService)
                {
                    invoiceLines.Add(new SalesInvoiceDraftLine(null, orderLineIds[i], line.Quantity));
                }
                else if (SalesOrderSeed.Delivered(line, order.DeliveredShare) is var delivered and > 0m)
                {
                    invoiceLines.Add(new SalesInvoiceDraftLine(deliveryLineIds[deliveryLine++], null, delivered));
                }
            }

            var invoiceId = await SendAsync<CreateSalesInvoiceHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateSalesInvoice(companyId, orderId, order.InvoiceDate, order.InvoiceDate.AddDays(15), null, invoiceLines), ct),
                $"Create invoice for sales order {index}",
                cancellationToken);
            await SendAsync<ConfirmSalesInvoiceHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmSalesInvoice(companyId, invoiceId), ct), $"Issue invoice for sales order {index}", cancellationToken);
            seeded.Add(await CorrectAsync(companyId, warehouseId, order, orderId, deliveryId, invoiceId, index, cancellationToken));
        }

        return seeded;
    }

    private async Task<SeededSalesOrder> CorrectAsync(
        CompanyId companyId, Guid warehouseId, SalesOrderSeed order, Guid orderId, Guid deliveryId, Guid invoiceId, int index, CancellationToken cancellationToken)
    {
        if (order.Correction == SalesCorrection.InvoiceStorno)
        {
            var stornoId = await SendAsync<StornoSalesInvoiceHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new StornoSalesInvoice(companyId, invoiceId), ct), $"Reverse invoice of sales order {index}", cancellationToken);
            return new SeededSalesOrder(order, orderId, deliveryId, invoiceId, null, stornoId);
        }

        if (order.Correction == SalesCorrection.None)
        {
            return new SeededSalesOrder(order, orderId, deliveryId, invoiceId, null, null);
        }

        await using var scope = services.CreateAsyncScope();
        var first = await scope.ServiceProvider.GetRequiredService<ISalesDbContext>().SalesInvoices
            .Where(i => i.Id == invoiceId).SelectMany(i => i.Lines).Where(l => !l.IsService).OrderBy(l => l.LineNumber).FirstAsync(cancellationToken);
        var line = order.Correction == SalesCorrection.Return
            ? new CreditNoteDraftLine(first.Id, CreditKind.Return, first.Quantity == decimal.Truncate(first.Quantity) ? 1m : Math.Round(first.Quantity / 2m, 2), null, [])
            : new CreditNoteDraftLine(first.Id, CreditKind.ValueReduction, null, Math.Round(first.NetAmount * 0.1m, 2, MidpointRounding.AwayFromZero), []);
        var creditNoteId = await SendAsync<CreateCreditNoteHandler, Guid>(
            (handler, ct) => handler.HandleAsync(
                new CreateCreditNote(companyId, invoiceId, order.CreditNoteDate, line.Kind == CreditKind.Return ? warehouseId : null, "Reklamacija kupca", [line]), ct),
            $"Create credit note for sales order {index}",
            cancellationToken);
        await SendAsync<ConfirmCreditNoteHandler>(
            (handler, ct) => handler.HandleAsync(new ConfirmCreditNote(companyId, creditNoteId), ct), $"Confirm credit note for sales order {index}", cancellationToken);
        return new SeededSalesOrder(order, orderId, deliveryId, invoiceId, creditNoteId, null);
    }

    private async Task<Guid> SentOfferAsync(CompanyId companyId, Guid customerId, DateOnly date, List<SalesDraftLine> lines, string what, CancellationToken cancellationToken)
    {
        var offerId = await SendAsync<CreateOfferHandler, Guid>(
            (handler, ct) => handler.HandleAsync(new CreateOffer(companyId, customerId, date, date.AddDays(15), null, lines), ct), $"Create {what}", cancellationToken);
        await SendAsync<SendOfferHandler>((handler, ct) => handler.HandleAsync(new SendOffer(companyId, offerId), ct), $"Send {what}", cancellationToken);
        return offerId;
    }

    private async Task<IReadOnlyList<Guid>> SalesOrderLineIdsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISalesDbContext>().SalesOrders
            .Where(o => o.Id == orderId).SelectMany(o => o.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> DeliveryLineIdsAsync(Guid deliveryNoteId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISalesDbContext>().DeliveryNotes
            .Where(d => d.Id == deliveryNoteId).SelectMany(d => d.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Sales learns about partners and articles through Core's outbox, moments after they are created.</summary>
    private async Task WaitForSalesReferencesAsync(CompanyId companyId, int partners, int articles, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var sales = scope.ServiceProvider.GetRequiredService<ISalesDbContext>();
                if (await sales.Customers.CountAsync(c => c.CompanyId == companyId, cancellationToken) == partners
                    && await sales.Articles.CountAsync(a => a.CompanyId == companyId, cancellationToken) == articles)
                {
                    return;
                }
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new SeedFailedException("Business partners and articles did not reach the Sales module; is the outbox processor running?");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Guid>> PurchaseOrderLineIdsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPurchasingDbContext>().PurchaseOrders
            .Where(o => o.Id == orderId).SelectMany(o => o.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ReceiptLineIdsAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPurchasingDbContext>().GoodsReceipts
            .Where(r => r.Id == receiptId).SelectMany(r => r.Lines).OrderBy(l => l.LineNumber).Select(l => l.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Purchasing learns about partners and articles through Core's outbox, moments after they are created.</summary>
    private async Task WaitForPurchasingReferencesAsync(CompanyId companyId, int partners, int articles, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var purchasing = scope.ServiceProvider.GetRequiredService<IPurchasingDbContext>();
                if (await purchasing.Suppliers.CountAsync(s => s.CompanyId == companyId, cancellationToken) == partners
                    && await purchasing.Articles.CountAsync(a => a.CompanyId == companyId, cancellationToken) == articles)
                {
                    return;
                }
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new SeedFailedException("Business partners and articles did not reach the Purchasing module; is the outbox processor running?");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    /// <summary>Number series and warehouses, then the opening stock as confirmed receipts.</summary>
    private async Task<Dictionary<string, Guid>> SeedWarehouseAsync(
        SeedData data, CompanyId companyId, IReadOnlyDictionary<string, Guid> articleIds, CancellationToken cancellationToken)
    {
        foreach (var series in data.NumberSeries)
        {
            await SendAsync<CreateNumberSeriesHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateNumberSeries(companyId, series.DocumentType, series.ValidFrom, series.Prefix, series.Padding), ct),
                $"Create number series {series.Prefix}",
                cancellationToken);
        }

        var warehouseIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var warehouse in data.Warehouses)
        {
            warehouseIds[warehouse.Code] = await SendAsync<CreateWarehouseHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateWarehouse(companyId, warehouse.Code, warehouse.Name, IsActive: true), ct),
                $"Create warehouse {warehouse.Code}",
                cancellationToken);
        }

        await WaitForArticlesAsync(companyId, articleIds.Count, cancellationToken);

        foreach (var (receipt, index) in data.OpeningReceipts.Select((receipt, index) => (receipt, index + 1)))
        {
            var lines = receipt.Lines
                .Select(line => new StockLineInput(articleIds[line.ArticleCode], line.Quantity, line.UnitCost, line.LotNumber, line.LotExpiryDate, line.SerialNumbers))
                .ToList();
            var documentId = await SendAsync<CreateStockDocumentHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateStockDocument(companyId, StockDocumentType.Receipt, receipt.DocumentDate, null, warehouseIds[receipt.WarehouseCode], "Početno stanje", lines),
                    ct),
                $"Create opening receipt {index} ({receipt.WarehouseCode})",
                cancellationToken);
            await SendAsync<ConfirmStockDocumentHandler>(
                (handler, ct) => handler.HandleAsync(new ConfirmStockDocument(companyId, documentId), ct),
                $"Confirm opening receipt {index} ({receipt.WarehouseCode})",
                cancellationToken);
        }

        return warehouseIds;
    }

    /// <summary>Warehouse learns about articles through Core's outbox (ArticleCreated), moments after they are created.</summary>
    private async Task WaitForArticlesAsync(CompanyId companyId, int count, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var warehouse = scope.ServiceProvider.GetRequiredService<IWarehouseDbContext>();
                if (await warehouse.Articles.CountAsync(article => article.CompanyId == companyId, cancellationToken) == count)
                {
                    return;
                }
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new SeedFailedException("The articles did not reach the Warehouse module; is the outbox processor running?");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    /// <summary>The administrator's membership arrives through Core's outbox (CompanyCreated), moments after the company.</summary>
    private async Task WaitForMembershipAsync(Guid userId, CompanyId companyId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + EventTimeout;
        while (true)
        {
            var me = await SendAsync<GetCurrentUserHandler, CurrentUserResponse>(
                (handler, ct) => handler.HandleAsync(new GetCurrentUser(userId), ct), "Read the administrator", cancellationToken);
            if (me.Companies.Any(membership => membership.CompanyId == companyId.Value))
            {
                return;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new SeedFailedException("The administrator did not become a member of the company; is the outbox processor running?");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private async Task<T> SendAsync<THandler, T>(
        Func<THandler, CancellationToken, Task<Result<T>>> send,
        string step,
        CancellationToken cancellationToken)
        where THandler : notnull
    {
        await using var scope = services.CreateAsyncScope();
        var result = await send(scope.ServiceProvider.GetRequiredService<THandler>(), cancellationToken);
        return result.IsSuccess ? result.Value : throw new SeedFailedException($"{step} failed: {Describe(result.Error)}");
    }

    private async Task SendAsync<THandler>(
        Func<THandler, CancellationToken, Task<Result>> send,
        string step,
        CancellationToken cancellationToken)
        where THandler : notnull
    {
        await using var scope = services.CreateAsyncScope();
        var result = await send(scope.ServiceProvider.GetRequiredService<THandler>(), cancellationToken);
        if (result.IsFailure)
        {
            throw new SeedFailedException($"{step} failed: {Describe(result.Error)}");
        }
    }

    internal static string Describe(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Errors.Select(field => $"{field.Key}: {string.Join(", ", field.Value)}"))
        : $"{error.Code} ({error.Message})";
}
