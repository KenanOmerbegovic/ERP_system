using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>Replaces a draft invoice's content and recomputes its PDV. A confirmed invoice cannot be changed (FR-SAL-05).</summary>
public sealed record UpdateSalesInvoice(
    CompanyId CompanyId,
    Guid InvoiceId,
    Guid SalesOrderId,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string? Note,
    IReadOnlyList<SalesInvoiceDraftLine> Lines) : ISalesInvoiceDraft;

internal sealed class UpdateSalesInvoiceValidator : AbstractValidator<UpdateSalesInvoice>
{
    public UpdateSalesInvoiceValidator(IClock clock)
    {
        Include(new SalesInvoiceDraftValidator(clock));
    }
}

public sealed class UpdateSalesInvoiceHandler(ISalesDbContext db, ISalesLocks locks, IVatCalculator vatCalculator, IValidator<UpdateSalesInvoice> validator)
{
    public async Task<Result> HandleAsync(UpdateSalesInvoice command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.LockInvoiceAsync(command.CompanyId, command.InvoiceId, cancellationToken))
        {
            return SalesErrors.InvoiceNotFound;
        }

        var invoice = await db.SalesInvoices.Include(i => i.Lines).Include(i => i.VatTotals).AsSplitQuery()
            .FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);
        if (!invoice.IsDraft)
        {
            return SalesErrors.InvoiceNotDraft;
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (command.SalesOrderId != invoice.SalesOrderId)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(command.SalesOrderId), "SalesInvoice.OrderFixed");
            return errors.ToError();
        }

        var order = await OrderLookup.OpenOrderAsync(db, command.CompanyId, invoice.SalesOrderId, tracked: false, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        var lines = await SalesInvoiceRules.ResolveAsync(db, order.Value, command, cancellationToken);
        if (lines.IsFailure)
        {
            return lines.Error;
        }

        invoice.UpdateDraft(command.InvoiceDate, command.DueDate, command.Note, lines.Value);
        var vat = await SalesInvoiceRules.ApplyVatAsync(vatCalculator, invoice, cancellationToken);
        if (vat.IsFailure)
        {
            return vat;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
