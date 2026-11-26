using Erp.SharedKernel.Results;

namespace Erp.Modules.Purchasing.Application;

public static class PurchasingErrors
{
    public static readonly Error OrderNotFound = Error.NotFound("PurchaseOrder.NotFound", "Purchase order not found.");

    public static readonly Error OrderNotDraft =
        Error.Conflict("PurchaseOrder.NotDraft", "The order is confirmed and cannot be changed; reverse it with a storno.");

    public static readonly Error OrderNotOpen =
        Error.Conflict("PurchaseOrder.NotOpen", "The order is not confirmed, or it was reversed; nothing can be received or invoiced on it.");

    public static readonly Error OrderHasReceipts =
        Error.Conflict("PurchaseOrder.HasReceipts", "Goods were received or invoiced on this order; reverse those first.");

    public static readonly Error OrderNotConfirmed = Error.Conflict("PurchaseOrder.NotConfirmed", "Only a confirmed order can be reversed.");

    public static readonly Error IsStorno = Error.Conflict("Document.IsStorno", "A storno cannot be reversed.");

    public static readonly Error AlreadyReversed = Error.Conflict("Document.AlreadyReversed", "The document is already reversed.");

    public static readonly Error ReceiptNotFound = Error.NotFound("GoodsReceipt.NotFound", "Goods receipt not found.");

    public static readonly Error ReceiptNotDraft =
        Error.Conflict("GoodsReceipt.NotDraft", "The receipt is confirmed or being confirmed and cannot be changed.");

    public static readonly Error ReceiptNotConfirmed = Error.Conflict("GoodsReceipt.NotConfirmed", "Only a confirmed receipt can be reversed.");

    public static readonly Error ReceiptInvoiced =
        Error.Conflict("GoodsReceipt.Invoiced", "The receipt is invoiced; reverse the supplier invoice first.");

    public static readonly Error InvoiceNotFound = Error.NotFound("SupplierInvoice.NotFound", "Supplier invoice not found.");

    public static readonly Error InvoiceNotDraft =
        Error.Conflict("SupplierInvoice.NotDraft", "The invoice is registered and cannot be changed; reverse it with a storno.");

    public static readonly Error InvoiceNotConfirmed = Error.Conflict("SupplierInvoice.NotConfirmed", "Only a registered invoice can be reversed.");

    public static readonly Error DuplicateSupplierInvoiceNumber =
        Error.Conflict("SupplierInvoice.DuplicateNumber", "This supplier's invoice with this number is already registered.");

    public static readonly Error NumberSeriesNotConfigured =
        new("NumberSeries.NotConfigured", "No number series is configured for this document type and date.");

    public static readonly Error DuplicateNumberSeries =
        Error.Conflict("NumberSeries.Duplicate", "A number series for this document type already starts on this date.");
}

/// <summary>Validation codes for fields checked against stored data; clients map them to UI text.</summary>
public static class PurchasingValidationCodes
{
    public const string SupplierNotFound = "Supplier.NotFound";
    public const string NotASupplier = "Supplier.NotActiveSupplier";
    public const string ArticleNotFound = "Article.NotFound";
    public const string ArticleNotOrderable = "Article.NotOrderable";
    public const string VatCodeRequired = "Line.VatCodeRequired";
    public const string VatRateNotFound = "Vat.RateNotFound";
    public const string DateInFuture = "Date.InFuture";
    public const string BeforeOrderDate = "Date.BeforeOrderDate";
    public const string OrderLineNotFound = "OrderLine.NotFound";
    public const string QuantityExceedsOpen = "Quantity.ExceedsOpen";
    public const string ReceiptLineNotFound = "ReceiptLine.NotFound";
    public const string QuantityExceedsUninvoiced = "Quantity.ExceedsUninvoiced";
    public const string LotNumberRequired = "StockLine.LotNumberRequired";
    public const string LotNumberNotAllowed = "StockLine.LotNumberNotAllowed";
    public const string SerialNumberCountMismatch = "StockLine.SerialNumberCountMismatch";
    public const string SerialNumbersNotAllowed = "StockLine.SerialNumbersNotAllowed";
    public const string DueBeforeInvoice = "DueDate.BeforeInvoiceDate";
}
