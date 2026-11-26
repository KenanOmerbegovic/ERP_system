using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Numbering;

/// <summary>The next number of a document type for a date, inside the caller's transaction.</summary>
internal static class DocumentNumbers
{
    public static async Task<Result<string>> NextAsync(
        ISalesDbContext db, ISalesLocks locks, CompanyId companyId, SalesDocumentType type, DateOnly date, CancellationToken cancellationToken)
    {
        var series = await db.NumberSeries.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.DocumentType == type && s.ValidFrom <= date)
            .OrderByDescending(s => s.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (series is null)
        {
            return SalesErrors.NumberSeriesNotConfigured;
        }

        var counter = await locks.NextNumberAsync(companyId, type, date.Year, cancellationToken);
        return series.Format(date.Year, counter);
    }

    /// <summary>Fails early, before any lock or warehouse call, when no series covers the date.</summary>
    public static Task<bool> ConfiguredAsync(ISalesDbContext db, CompanyId companyId, SalesDocumentType type, DateOnly date, CancellationToken cancellationToken) =>
        db.NumberSeries.AnyAsync(s => s.CompanyId == companyId && s.DocumentType == type && s.ValidFrom <= date, cancellationToken);
}
