using Erp.SharedKernel.Results;

namespace Erp.Modules.Sales.Application;

public static class SalesErrors
{
    public static readonly Error OfferNotFound = Error.NotFound("Offer.NotFound", "Offer not found.");

    public static readonly Error OfferNotDraft = Error.Conflict("Offer.NotDraft", "The offer was sent and cannot be changed.");

    public static readonly Error OfferNotSent = Error.Conflict("Offer.NotSent", "Only a sent offer can be accepted or rejected.");

    public static readonly Error OrderNotFound = Error.NotFound("SalesOrder.NotFound", "Sales order not found.");

    public static readonly Error OrderNotDraft =
        Error.Conflict("SalesOrder.NotDraft", "The order is confirmed and cannot be changed; reverse it with a storno.");

    public static readonly Error OrderNotOpen =
        Error.Conflict("SalesOrder.NotOpen", "The order is not confirmed, or it was reversed; nothing can be delivered or invoiced on it.");

    public static readonly Error OrderHasDeliveries =
        Error.Conflict("SalesOrder.HasDeliveries", "Goods were delivered or invoiced on this order; reverse those first.");

    public static readonly Error OrderNotConfirmed = Error.Conflict("SalesOrder.NotConfirmed", "Only a confirmed order can be reversed.");

    public static readonly Error IsStorno = Error.Conflict("Document.IsStorno", "A storno cannot be reversed.");

    public static readonly Error AlreadyReversed = Error.Conflict("Document.AlreadyReversed", "The document is already reversed.");

    public static readonly Error DeliveryNoteNotFound = Error.NotFound("DeliveryNote.NotFound", "Delivery note not found.");

    public static readonly Error DeliveryNoteNotDraft =
        Error.Conflict("DeliveryNote.NotDraft", "The delivery note is confirmed or being confirmed and cannot be changed.");

    public static readonly Error DeliveryNoteNotConfirmed = Error.Conflict("DeliveryNote.NotConfirmed", "Only a confirmed delivery note can be reversed.");

    public static readonly Error DeliveryNoteInvoiced =
        Error.Conflict("DeliveryNote.Invoiced", "The delivery note is invoiced; reverse the invoice first.");

    public static readonly Error InvoiceNotFound = Error.NotFound("SalesInvoice.NotFound", "Invoice not found.");

    public static readonly Error InvoiceNotDraft =
        Error.Conflict("SalesInvoice.NotDraft", "The invoice is confirmed and cannot be changed; correct it with a storno or a credit note.");

    public static readonly Error InvoiceNotConfirmed = Error.Conflict("SalesInvoice.NotConfirmed", "Only a confirmed invoice can be reversed.");

    public static readonly Error InvoiceNotOpen =
        Error.Conflict("SalesInvoice.NotOpen", "The invoice is not confirmed, is a storno or was reversed; no credit note can be made on it.");

    public static readonly Error InvoiceCredited =
        Error.Conflict("SalesInvoice.Credited", "Credit notes were made on this invoice; reverse them first or credit the rest.");

    public static readonly Error CreditNoteNotFound = Error.NotFound("CreditNote.NotFound", "Credit note not found.");

    public static readonly Error CreditNoteNotDraft =
        Error.Conflict("CreditNote.NotDraft", "The credit note is confirmed or being confirmed and cannot be changed.");

    public static readonly Error CreditNoteNotConfirmed = Error.Conflict("CreditNote.NotConfirmed", "Only a confirmed credit note can be reversed.");

    public static readonly Error NumberSeriesNotConfigured =
        new("NumberSeries.NotConfigured", "No number series is configured for this document type and date.");

    public static readonly Error DuplicateNumberSeries =
        Error.Conflict("NumberSeries.Duplicate", "A number series for this document type already starts on this date.");
}

/// <summary>Validation codes for fields checked against stored data; clients map them to UI text.</summary>
public static class SalesValidationCodes
{
    public const string CustomerNotFound = "Customer.NotFound";
    public const string NotACustomer = "Customer.NotActiveCustomer";
    public const string ArticleNotFound = "Article.NotFound";
    public const string ArticleInactive = "Article.Inactive";
    public const string VatCodeRequired = "Line.VatCodeRequired";
    public const string VatRateNotFound = "Vat.RateNotFound";
    public const string DateInFuture = "Date.InFuture";
    public const string BeforeOfferDate = "Date.BeforeOfferDate";
    public const string BeforeOrderDate = "Date.BeforeOrderDate";
    public const string BeforeInvoiceDate = "Date.BeforeInvoiceDate";
    public const string ValidUntilBeforeOfferDate = "ValidUntil.BeforeOfferDate";
    public const string DueBeforeInvoice = "DueDate.BeforeInvoiceDate";
    public const string OrderLineNotFound = "OrderLine.NotFound";
    public const string ServiceNotDelivered = "OrderLine.ServiceNotDelivered";
    public const string GoodsNeedDelivery = "OrderLine.GoodsNeedDelivery";
    public const string QuantityExceedsOpen = "Quantity.ExceedsOpen";
    public const string DeliveryLineNotFound = "DeliveryLine.NotFound";
    public const string QuantityExceedsUninvoiced = "Quantity.ExceedsUninvoiced";
    public const string LineSourceRequired = "Line.SourceRequired";
    public const string InvoiceLineNotFound = "InvoiceLine.NotFound";
    public const string ServiceNotReturned = "InvoiceLine.ServiceNotReturned";
    public const string QuantityExceedsReturnable = "Quantity.ExceedsReturnable";
    public const string AmountExceedsCreditable = "Amount.ExceedsCreditable";
    public const string WarehouseRequired = "WarehouseId.RequiredForReturns";
    public const string LotNumberRequired = "StockLine.LotNumberRequired";
    public const string LotNumberNotAllowed = "StockLine.LotNumberNotAllowed";
    public const string SerialNumberCountMismatch = "StockLine.SerialNumberCountMismatch";
    public const string SerialNumbersNotAllowed = "StockLine.SerialNumbersNotAllowed";
    public const string SerialNumberNotDelivered = "StockLine.SerialNumberNotDelivered";
}
