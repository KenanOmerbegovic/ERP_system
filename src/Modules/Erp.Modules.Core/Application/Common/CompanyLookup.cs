using Erp.Modules.Core.Application.Abstractions;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.Common;

internal static class CompanyLookup
{
    public static Task<bool> CompanyExistsAsync(this ICoreDbContext db, CompanyId companyId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);

    /// <summary>True for no code, or a code the company has a VAT rate for (any date).</summary>
    public static async Task<bool> VatCodeExistsAsync(this ICoreDbContext db, CompanyId companyId, string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        var trimmed = code.Trim();
        return await db.VatRates.AnyAsync(rate => rate.CompanyId == companyId && rate.Code == trimmed, cancellationToken);
    }
}
