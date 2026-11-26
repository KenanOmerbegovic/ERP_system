namespace Erp.Modules.Sales.Contracts;

/// <summary>Permissions the Sales module checks. Confirm also covers storno. Companies combine them into roles.</summary>
public static class SalesPermissions
{
    public const string OffersRead = "sales.offers.read";
    public const string OffersWrite = "sales.offers.write";

    /// <summary>Sending, accepting and rejecting offers; accepting creates a draft sales order.</summary>
    public const string OffersSend = "sales.offers.send";

    public const string OrdersRead = "sales.orders.read";
    public const string OrdersWrite = "sales.orders.write";
    public const string OrdersConfirm = "sales.orders.confirm";
    public const string DeliveriesRead = "sales.deliveries.read";
    public const string DeliveriesWrite = "sales.deliveries.write";

    /// <summary>Confirming a delivery note issues stock from the warehouse.</summary>
    public const string DeliveriesConfirm = "sales.deliveries.confirm";

    public const string InvoicesRead = "sales.invoices.read";
    public const string InvoicesWrite = "sales.invoices.write";
    public const string InvoicesConfirm = "sales.invoices.confirm";
    public const string CreditNotesRead = "sales.creditnotes.read";
    public const string CreditNotesWrite = "sales.creditnotes.write";

    /// <summary>Confirming a credit note with returned goods brings them back into the warehouse.</summary>
    public const string CreditNotesConfirm = "sales.creditnotes.confirm";

    /// <summary>Sales configuration, e.g. document number series.</summary>
    public const string SettingsWrite = "sales.settings.write";

    public static IReadOnlyList<string> All { get; } =
    [
        OffersRead,
        OffersWrite,
        OffersSend,
        OrdersRead,
        OrdersWrite,
        OrdersConfirm,
        DeliveriesRead,
        DeliveriesWrite,
        DeliveriesConfirm,
        InvoicesRead,
        InvoicesWrite,
        InvoicesConfirm,
        CreditNotesRead,
        CreditNotesWrite,
        CreditNotesConfirm,
        SettingsWrite,
    ];
}

/// <summary>
/// Draft: editable. Confirming: a delivery note or credit note whose stock movement is being made in the warehouse; it
/// cannot be edited, and confirming it again completes it. Confirmed: numbered and immutable; corrected by storno or,
/// for invoices, a credit note.
/// </summary>
public enum SalesDocumentState
{
    Draft,
    Confirming,
    Confirmed,
}

/// <summary>Draft: editable. Sent: numbered and given to the customer, no longer editable. Accepted or Rejected: final.</summary>
public enum OfferState
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

/// <summary>A credit note line either returns goods to stock or only reduces the invoiced value.</summary>
public enum CreditNoteLineKind
{
    Return,
    ValueReduction,
}
