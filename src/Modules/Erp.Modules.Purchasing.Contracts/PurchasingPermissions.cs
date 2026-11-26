namespace Erp.Modules.Purchasing.Contracts;

/// <summary>Permissions the Purchasing module checks. Confirm also covers storno. Companies combine them into roles.</summary>
public static class PurchasingPermissions
{
    public const string OrdersRead = "purchasing.orders.read";
    public const string OrdersWrite = "purchasing.orders.write";
    public const string OrdersConfirm = "purchasing.orders.confirm";
    public const string ReceiptsRead = "purchasing.receipts.read";
    public const string ReceiptsWrite = "purchasing.receipts.write";

    /// <summary>Confirming a receipt moves stock into the warehouse.</summary>
    public const string ReceiptsConfirm = "purchasing.receipts.confirm";

    public const string InvoicesRead = "purchasing.invoices.read";
    public const string InvoicesWrite = "purchasing.invoices.write";
    public const string InvoicesConfirm = "purchasing.invoices.confirm";

    /// <summary>Purchasing configuration, e.g. document number series.</summary>
    public const string SettingsWrite = "purchasing.settings.write";

    public static IReadOnlyList<string> All { get; } =
    [
        OrdersRead,
        OrdersWrite,
        OrdersConfirm,
        ReceiptsRead,
        ReceiptsWrite,
        ReceiptsConfirm,
        InvoicesRead,
        InvoicesWrite,
        InvoicesConfirm,
        SettingsWrite,
    ];
}

/// <summary>
/// Draft: editable. Confirming: a goods receipt whose stock receipt is being made in the warehouse; it cannot be edited,
/// and confirming it again completes it. Confirmed: numbered and immutable; corrected by storno.
/// </summary>
public enum PurchaseDocumentState
{
    Draft,
    Confirming,
    Confirmed,
}
