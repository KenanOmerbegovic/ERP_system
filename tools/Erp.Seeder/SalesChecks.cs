using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Seeder.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Seeder;

/// <summary>
/// FR-GEN-03 for sales: offers and orders are where their scenarios left them; every order line's delivered and
/// invoiced quantities equal its confirmed delivery notes and invoices (never more invoiced than delivered for goods);
/// every invoice line's credited amount equals its credit notes; every delivery note and every credit note with returns
/// has its warehouse stock document of the same value (the cost of goods sold, or what came back); and every invoice's
/// and credit note's PDV is what the country's calculator computes for it.
/// </summary>
internal static class SalesChecks
{
    public static async Task RunAsync(IServiceProvider provider, SeedData data, SeededCompany seeded, List<string> problems, CancellationToken cancellationToken)
    {
        var sales = provider.GetRequiredService<ISalesDbContext>();
        var warehouse = provider.GetRequiredService<IWarehouseDbContext>();
        var companyId = seeded.CompanyId;

        var offers = await sales.Offers.AsNoTracking().Where(o => o.CompanyId == companyId).ToListAsync(cancellationToken);
        var expectedAccepted = data.SalesOrders.Count(o => o.FromOffer);
        if (offers.Count(o => o.Status == OfferStatus.Accepted) != expectedAccepted
            || offers.Count(o => o.Status == OfferStatus.Sent) != data.Offers.Count(o => o.Outcome == OfferOutcome.Sent)
            || offers.Count(o => o.Status == OfferStatus.Rejected) != data.Offers.Count(o => o.Outcome == OfferOutcome.Rejected))
        {
            problems.Add($"Offers: expected {expectedAccepted} accepted and {data.Offers.Count} still sent or rejected; found {offers.Count} in other states.");
        }

        var orders = await sales.SalesOrders.AsNoTracking().Include(o => o.Lines).Where(o => o.CompanyId == companyId).ToListAsync(cancellationToken);
        if (orders.Count != seeded.SalesOrders.Count || orders.Any(o => o.Status != DocumentStatus.Confirmed))
        {
            problems.Add($"Expected {seeded.SalesOrders.Count} confirmed sales orders, found {orders.Count(o => o.Status == DocumentStatus.Confirmed)} of {orders.Count}.");
        }

        var notes = await sales.DeliveryNotes.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.CompanyId == companyId && d.Status == DocumentStatus.Confirmed).ToListAsync(cancellationToken);
        var invoices = await sales.SalesInvoices.AsNoTracking().Include(i => i.Lines).Include(i => i.VatTotals).AsSplitQuery()
            .Where(i => i.CompanyId == companyId && i.Status == DocumentStatus.Confirmed).ToListAsync(cancellationToken);
        var creditNotes = await sales.CreditNotes.AsNoTracking().Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .Where(c => c.CompanyId == companyId && c.Status == DocumentStatus.Confirmed).ToListAsync(cancellationToken);

        var delivered = notes.SelectMany(d => d.Lines.Select(l => (l.OrderLineId, Quantity: d.IsStorno ? -l.Quantity : l.Quantity)))
            .GroupBy(x => x.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var invoiced = invoices.SelectMany(i => i.Lines.Select(l => (l.OrderLineId, Quantity: i.IsStorno ? -l.Quantity : l.Quantity)))
            .GroupBy(x => x.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var line in orders.SelectMany(o => o.Lines))
        {
            if (line.DeliveredQuantity != delivered.GetValueOrDefault(line.Id) || line.InvoicedQuantity != invoiced.GetValueOrDefault(line.Id)
                || (!line.IsService && line.InvoicedQuantity > line.DeliveredQuantity))
            {
                problems.Add($"Sales order line {line.Id}: delivered {line.DeliveredQuantity} and invoiced {line.InvoicedQuantity} differ from its documents.");
            }
        }

        var credited = creditNotes.SelectMany(c => c.Lines.Select(l => (l.InvoiceLineId, Amount: c.IsStorno ? -l.NetAmount : l.NetAmount)))
            .GroupBy(x => x.InvoiceLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        foreach (var line in invoices.SelectMany(i => i.Lines))
        {
            if (line.CreditedAmount != credited.GetValueOrDefault(line.Id) || line.CreditedAmount > line.NetAmount)
            {
                problems.Add($"Invoice line {line.Id}: credited {line.CreditedAmount} differs from its credit notes.");
            }
        }

        var stockDocumentIds = notes.Select(d => d.StockDocumentId).Concat(creditNotes.Select(c => c.StockDocumentId)).OfType<Guid>().ToList();
        var stockDocuments = await warehouse.StockDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => stockDocumentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        foreach (var note in notes)
        {
            if (note.StockDocumentId is not { } id || !stockDocuments.TryGetValue(id, out var document)
                || document.SourceDocumentId != note.Id || document.TotalValue != note.CostValue)
            {
                problems.Add($"Delivery note {note.Number} has no matching stock document (cost of goods sold) in the warehouse.");
            }
        }

        foreach (var creditNote in creditNotes.Where(c => c.HasReturns))
        {
            if (creditNote.StockDocumentId is not { } id || !stockDocuments.TryGetValue(id, out var document)
                || document.SourceDocumentId != creditNote.Id || document.TotalValue != creditNote.ReturnedCostValue)
            {
                problems.Add($"Credit note {creditNote.Number} has no matching stock document for its returns in the warehouse.");
            }
        }

        var calculator = provider.GetRequiredService<IVatCalculator>();
        foreach (var (number, date, lines, vat, gross) in invoices.Select(i => (i.Number, i.InvoiceDate, i.Lines.Select(l => (l.LineNumber, l.NetAmount, l.VatCode)), i.VatAmount, i.GrossAmount))
                     .Concat(creditNotes.Select(c => (c.Number, c.VatDate, c.Lines.Select(l => (l.LineNumber, l.NetAmount, l.VatCode)), c.VatAmount, c.GrossAmount))))
        {
            var calculation = await calculator.CalculateAsync(
                new VatCalculationRequest(
                    companyId, date, lines.Select(l => new VatLineInput(l.LineNumber.ToString(CultureInfo.InvariantCulture), l.NetAmount, l.VatCode)).ToList()),
                cancellationToken);
            if (!calculation.IsSuccess || calculation.Value.VatAmount != vat || calculation.Value.GrossAmount != gross)
            {
                problems.Add($"{number}: PDV {vat} differs from the calculator's.");
            }
        }
    }
}
