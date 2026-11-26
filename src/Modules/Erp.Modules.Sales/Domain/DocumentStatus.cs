using Erp.SharedKernel.Monetary;

namespace Erp.Modules.Sales.Domain;

/// <summary>
/// Draft: editable. Confirming: a delivery note or credit note waiting for its warehouse movement (not editable).
/// Confirmed: numbered and immutable (NFR-INT-01); corrected only by a storno or, for an invoice, a credit note.
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

    /// <summary>An amount in KM rounded to fening.</summary>
    public static decimal Km(decimal amount) => Money.Km(amount).Round().Amount;
}

/// <summary>PDV of one line or one rate, as the country's calculator computed it.</summary>
public sealed record VatAmounts(string VatCode, decimal Rate, decimal Base, decimal VatAmount);
