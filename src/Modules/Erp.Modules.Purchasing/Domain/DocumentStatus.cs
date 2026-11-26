using Erp.SharedKernel.Monetary;

namespace Erp.Modules.Purchasing.Domain;

/// <summary>
/// Draft: editable. Confirming: a goods receipt waiting for its warehouse receipt (not editable). Confirmed: numbered and
/// immutable (NFR-INT-01); corrected only by a storno.
/// </summary>
public enum DocumentStatus
{
    Draft,
    Confirming,
    Confirmed,
}

internal static class Amounts
{
    /// <summary>Quantity × unit price in KM, rounded to fening.</summary>
    public static decimal LineAmount(decimal quantity, decimal unitPrice) => (Money.Km(unitPrice) * quantity).Round().Amount;
}
