using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Seeder.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Seeder;

/// <summary>
/// FR-GEN-03 for purchasing: every order's received and invoiced quantities equal its confirmed receipts and invoices;
/// every confirmed receipt has its warehouse stock document, of the same value; and every invoice's PDV is what the
/// country's calculator computes for it.
/// </summary>
internal static class PurchasingChecks
{
    public static async Task RunAsync(IServiceProvider provider, SeededCompany seeded, List<string> problems, CancellationToken cancellationToken)
    {
        var purchasing = provider.GetRequiredService<IPurchasingDbContext>();
        var warehouse = provider.GetRequiredService<IWarehouseDbContext>();
        var companyId = seeded.CompanyId;

        var orders = await purchasing.PurchaseOrders.AsNoTracking().Include(o => o.Lines).Where(o => o.CompanyId == companyId).ToListAsync(cancellationToken);
        if (orders.Count != seeded.PurchaseOrders.Count || orders.Any(o => o.Status != DocumentStatus.Confirmed))
        {
            problems.Add($"Expected {seeded.PurchaseOrders.Count} confirmed purchase orders, found {orders.Count(o => o.Status == DocumentStatus.Confirmed)} of {orders.Count}.");
        }

        var receipts = await purchasing.GoodsReceipts.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.CompanyId == companyId && r.Status == DocumentStatus.Confirmed).ToListAsync(cancellationToken);
        var invoices = await purchasing.SupplierInvoices.AsNoTracking().Include(i => i.Lines).Include(i => i.VatTotals)
            .Where(i => i.CompanyId == companyId && i.Status == DocumentStatus.Confirmed).ToListAsync(cancellationToken);
        var received = receipts.SelectMany(r => r.Lines.Select(l => (l.OrderLineId, Quantity: r.IsStorno ? -l.Quantity : l.Quantity)))
            .GroupBy(x => x.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var invoiced = invoices.SelectMany(i => i.Lines.Select(l => (l.OrderLineId, Quantity: i.IsStorno ? -l.Quantity : l.Quantity)))
            .GroupBy(x => x.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var line in orders.SelectMany(o => o.Lines))
        {
            if (line.ReceivedQuantity != received.GetValueOrDefault(line.Id) || line.InvoicedQuantity != invoiced.GetValueOrDefault(line.Id))
            {
                problems.Add($"Order line {line.Id}: received {line.ReceivedQuantity} and invoiced {line.InvoicedQuantity} differ from its receipts and invoices.");
            }
        }

        var stockDocumentIds = receipts.Select(r => r.StockDocumentId).OfType<Guid>().ToList();
        var stockDocuments = await warehouse.StockDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => stockDocumentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        foreach (var receipt in receipts)
        {
            if (receipt.StockDocumentId is not { } id || !stockDocuments.TryGetValue(id, out var document)
                || document.SourceDocumentId != receipt.Id || document.TotalValue != receipt.TotalValue)
            {
                problems.Add($"Goods receipt {receipt.Number} has no matching confirmed stock document in the warehouse.");
            }
        }

        var calculator = provider.GetRequiredService<IVatCalculator>();
        foreach (var invoice in invoices)
        {
            var calculation = await calculator.CalculateAsync(
                new VatCalculationRequest(
                    companyId, invoice.InvoiceDate,
                    invoice.Lines.Select(l => new VatLineInput(l.LineNumber.ToString(CultureInfo.InvariantCulture), l.NetAmount, l.VatCode)).ToList()),
                cancellationToken);
            if (!calculation.IsSuccess || calculation.Value.VatAmount != invoice.VatAmount || calculation.Value.GrossAmount != invoice.GrossAmount)
            {
                problems.Add($"Supplier invoice {invoice.Number}: PDV {invoice.VatAmount} differs from the calculator's.");
            }
        }
    }
}
