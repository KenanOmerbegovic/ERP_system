using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>Saves a draft invoice against an order's deliveries and services, with its PDV computed (FR-SAL-03, FR-SAL-04).</summary>
public sealed record CreateSalesInvoice(
    CompanyId CompanyId,
    Guid SalesOrderId,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SalesInvoiceDraftLine> Lines) : ISalesInvoiceDraft;

internal sealed class CreateSalesInvoiceValidator : AbstractValidator<CreateSalesInvoice>
{
    public CreateSalesInvoiceValidator(IClock clock)
    {
        Include(new SalesInvoiceDraftValidator(clock));
    }
}

public sealed class CreateSalesInvoiceHandler(ISalesDbContext db, IVatCalculator vatCalculator, IValidator<CreateSalesInvoice> validator, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateSalesInvoice command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, command.SalesOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var lines = await SalesInvoiceRules.ResolveAsync(db, order.Value, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        var invoice = SalesInvoice.CreateDraft(
            command.CompanyId, order.Value.Id, order.Value.CustomerId, command.InvoiceDate, command.DueDate, command.Note, lines.Value, clock.UtcNow);
        var vat = await SalesInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}
