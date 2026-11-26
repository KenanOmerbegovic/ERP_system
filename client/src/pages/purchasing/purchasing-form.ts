import {
  ArticleTracking,
  type GoodsReceiptRequest,
  type GoodsReceiptResponse,
  type InvoiceableReceiptLineResponse,
  type PurchaseOrderRequest,
  type PurchaseOrderResponse,
  type SupplierInvoiceRequest,
  type SupplierInvoiceResponse,
  type VatRateResponse,
} from '@/api/generated/model';

import { parseSerialNumbers } from '../warehouse/stock-document-form';

/** Message keys under purchasing.problems in src/i18n. */
export type Problem =
  | 'supplierRequired'
  | 'warehouseRequired'
  | 'orderRequired'
  | 'articleRequired'
  | 'quantityPositive'
  | 'priceRequired'
  | 'quantityExceedsOpen'
  | 'quantityExceedsUninvoiced'
  | 'lotRequired'
  | 'serialCount'
  | 'supplierInvoiceNumberRequired'
  | 'dueBeforeInvoice'
  | 'linesRequired';

/** KM rounded to fening, midpoints away from zero (as Money.Round on the server). */
export function roundKm(value: number): number {
  return (Math.sign(value) * Math.round(Math.abs(value) * 100 + 1e-9)) / 100;
}

/** Quantity × unit price in KM, rounded to fening. */
export const lineAmount = (quantity: number | null, unitPrice: number | null) =>
  roundKm((quantity ?? 0) * (unitPrice ?? 0));

const positive = (value: number | null): value is number => value !== null && value > 0;

// ---- Purchase orders -------------------------------------------------------

export interface OrderLineForm {
  articleId: string | null;
  articleLabel: string;
  quantity: number | null;
  unitPrice: number | null;
  /** Empty: the article's default VAT code. */
  vatCode: string;
}

export interface OrderForm {
  supplierId: string | null;
  supplierLabel: string;
  orderDate: string;
  expectedDate: string | null;
  warehouseId: string | null;
  note: string;
  lines: OrderLineForm[];
}

export const emptyOrderLine = (): OrderLineForm => ({
  articleId: null,
  articleLabel: '',
  quantity: null,
  unitPrice: null,
  vatCode: '',
});

export const emptyOrder = (today: string): OrderForm => ({
  supplierId: null,
  supplierLabel: '',
  orderDate: today,
  expectedDate: null,
  warehouseId: null,
  note: '',
  lines: [emptyOrderLine()],
});

export function orderProblems(form: OrderForm): Problem[] {
  const problems = new Set<Problem>();
  if (!form.supplierId) problems.add('supplierRequired');
  if (form.lines.length === 0) problems.add('linesRequired');
  for (const line of form.lines) {
    if (!line.articleId) problems.add('articleRequired');
    if (!positive(line.quantity)) problems.add('quantityPositive');
    if (line.unitPrice === null || line.unitPrice < 0) problems.add('priceRequired');
  }

  return [...problems];
}

export function toOrderRequest(form: OrderForm): PurchaseOrderRequest {
  return {
    supplierId: form.supplierId ?? '',
    orderDate: form.orderDate,
    expectedDate: form.expectedDate || null,
    warehouseId: form.warehouseId,
    note: form.note.trim() || null,
    lines: form.lines.map((line) => ({
      articleId: line.articleId ?? '',
      quantity: line.quantity ?? 0,
      unitPrice: line.unitPrice ?? 0,
      vatCode: line.vatCode.trim() || null,
    })),
  };
}

export function orderFromResponse(order: PurchaseOrderResponse): OrderForm {
  return {
    supplierId: order.supplierId,
    supplierLabel: `${order.supplierCode} – ${order.supplierName}`,
    orderDate: order.orderDate,
    expectedDate: order.expectedDate,
    warehouseId: order.warehouseId,
    note: order.note ?? '',
    lines: order.lines.map((line) => ({
      articleId: line.articleId,
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      quantity: Number(line.quantity),
      unitPrice: Number(line.unitPrice),
      vatCode: line.vatCode,
    })),
  };
}

// ---- Goods receipts ---------------------------------------------------------

export interface ReceiptLineForm {
  orderLineId: string;
  articleLabel: string;
  tracking: ArticleTracking;
  /** Ordered minus received when the receipt was started; the server re-checks it. */
  openQuantity: number;
  unitCost: number;
  quantity: number | null;
  lotNumber: string;
  lotExpiryDate: string | null;
  serialNumbers: string;
}

export interface ReceiptForm {
  purchaseOrderId: string | null;
  orderLabel: string;
  warehouseId: string | null;
  receiptDate: string;
  note: string;
  lines: ReceiptLineForm[];
}

/** A receipt of everything still open on the order, into its default warehouse. */
export function receiptFromOrder(order: PurchaseOrderResponse, today: string): ReceiptForm {
  return {
    purchaseOrderId: order.id,
    orderLabel: `${order.number ?? ''} – ${order.supplierName}`,
    warehouseId: order.warehouseId,
    receiptDate: today,
    note: '',
    lines: order.lines
      .filter((line) => Number(line.openQuantity) > 0)
      .map((line) => ({
        orderLineId: line.id,
        articleLabel: `${line.articleCode} – ${line.articleName}`,
        tracking: line.tracking,
        openQuantity: Number(line.openQuantity),
        unitCost: Number(line.unitPrice),
        quantity: Number(line.openQuantity),
        lotNumber: '',
        lotExpiryDate: null,
        serialNumbers: '',
      })),
  };
}

/** A saved receipt back into the editor; open quantities come from its order. */
export function receiptFromResponse(
  receipt: GoodsReceiptResponse,
  order: PurchaseOrderResponse | null,
): ReceiptForm {
  const orderLines = new Map(order?.lines.map((line) => [line.id, line]) ?? []);
  return {
    purchaseOrderId: receipt.purchaseOrderId,
    orderLabel: `${receipt.orderNumber ?? ''} – ${receipt.supplierName}`,
    warehouseId: receipt.warehouseId,
    receiptDate: receipt.receiptDate,
    note: receipt.note ?? '',
    lines: receipt.lines.map((line) => {
      const orderLine = orderLines.get(line.orderLineId);
      return {
        orderLineId: line.orderLineId,
        articleLabel: `${line.articleCode} – ${line.articleName}`,
        tracking: orderLine?.tracking ?? ArticleTracking.None,
        openQuantity: orderLine ? Number(orderLine.openQuantity) : Number(line.quantity),
        unitCost: Number(line.unitCost),
        quantity: Number(line.quantity),
        lotNumber: line.lotNumber ?? '',
        lotExpiryDate: line.lotExpiryDate,
        serialNumbers: line.serialNumbers.join('\n'),
      };
    }),
  };
}

export function receiptProblems(form: ReceiptForm): Problem[] {
  const problems = new Set<Problem>();
  if (!form.purchaseOrderId) problems.add('orderRequired');
  if (!form.warehouseId) problems.add('warehouseRequired');
  if (form.lines.length === 0) problems.add('linesRequired');
  const requested = new Map<string, number>();
  for (const line of form.lines) {
    if (!positive(line.quantity)) {
      problems.add('quantityPositive');
      continue;
    }

    const total = (requested.get(line.orderLineId) ?? 0) + line.quantity;
    requested.set(line.orderLineId, total);
    if (total > line.openQuantity) problems.add('quantityExceedsOpen');
    if (line.tracking === ArticleTracking.Lot && line.lotNumber.trim() === '')
      problems.add('lotRequired');
    if (
      line.tracking === ArticleTracking.Serial &&
      parseSerialNumbers(line.serialNumbers).length !== line.quantity
    ) {
      problems.add('serialCount');
    }
  }

  return [...problems];
}

export function toReceiptRequest(form: ReceiptForm): GoodsReceiptRequest {
  return {
    purchaseOrderId: form.purchaseOrderId ?? '',
    warehouseId: form.warehouseId ?? '',
    receiptDate: form.receiptDate,
    note: form.note.trim() || null,
    lines: form.lines.map((line) => ({
      orderLineId: line.orderLineId,
      quantity: line.quantity ?? 0,
      lotNumber: line.tracking === ArticleTracking.Lot ? line.lotNumber.trim() : null,
      lotExpiryDate: line.tracking === ArticleTracking.Lot ? line.lotExpiryDate : null,
      serialNumbers:
        line.tracking === ArticleTracking.Serial ? parseSerialNumbers(line.serialNumbers) : [],
    })),
  };
}

// ---- Supplier invoices ------------------------------------------------------

export interface InvoiceLineForm {
  receiptLineId: string;
  receiptLabel: string;
  articleLabel: string;
  /** Received minus invoiced when the line was added; the server re-checks it. */
  remainingQuantity: number;
  receivedUnitCost: number;
  quantity: number | null;
  unitPrice: number | null;
  vatCode: string;
}

export interface InvoiceForm {
  purchaseOrderId: string | null;
  orderLabel: string;
  supplierInvoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  note: string;
  lines: InvoiceLineForm[];
}

/** Invoice lines for every uninvoiced receipt line, at the received cost (the price the supplier usually charges). */
export function invoiceLinesFrom(lines: InvoiceableReceiptLineResponse[]): InvoiceLineForm[] {
  return lines.map((line) => {
    const remaining = Number(line.receivedQuantity) - Number(line.invoicedQuantity);
    return {
      receiptLineId: line.receiptLineId,
      receiptLabel: line.receiptNumber,
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      remainingQuantity: remaining,
      receivedUnitCost: Number(line.unitCost),
      quantity: remaining,
      unitPrice: Number(line.unitCost),
      vatCode: line.vatCode,
    };
  });
}

export function invoiceFromResponse(invoice: SupplierInvoiceResponse): InvoiceForm {
  return {
    purchaseOrderId: invoice.purchaseOrderId,
    orderLabel: `${invoice.orderNumber ?? ''} – ${invoice.supplierName}`,
    supplierInvoiceNumber: invoice.supplierInvoiceNumber,
    invoiceDate: invoice.invoiceDate,
    dueDate: invoice.dueDate,
    note: invoice.note ?? '',
    lines: invoice.lines.map((line) => ({
      receiptLineId: line.receiptLineId,
      receiptLabel: '',
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      // A saved draft does not reserve; its own quantity is what it may at least keep.
      remainingQuantity: Number(line.quantity),
      receivedUnitCost: Number(line.receivedUnitCost),
      quantity: Number(line.quantity),
      unitPrice: Number(line.unitPrice),
      vatCode: line.vatCode,
    })),
  };
}

export function invoiceProblems(form: InvoiceForm): Problem[] {
  const problems = new Set<Problem>();
  if (!form.purchaseOrderId) problems.add('orderRequired');
  if (form.supplierInvoiceNumber.trim() === '') problems.add('supplierInvoiceNumberRequired');
  if (form.dueDate < form.invoiceDate) problems.add('dueBeforeInvoice');
  if (form.lines.length === 0) problems.add('linesRequired');
  for (const line of form.lines) {
    if (!positive(line.quantity)) problems.add('quantityPositive');
    else if (line.quantity > line.remainingQuantity) problems.add('quantityExceedsUninvoiced');
    if (line.unitPrice === null || line.unitPrice < 0) problems.add('priceRequired');
  }

  return [...problems];
}

export function toInvoiceRequest(form: InvoiceForm): SupplierInvoiceRequest {
  return {
    purchaseOrderId: form.purchaseOrderId ?? '',
    supplierInvoiceNumber: form.supplierInvoiceNumber.trim(),
    invoiceDate: form.invoiceDate,
    dueDate: form.dueDate,
    note: form.note.trim() || null,
    lines: form.lines.map((line) => ({
      receiptLineId: line.receiptLineId,
      quantity: line.quantity ?? 0,
      unitPrice: line.unitPrice ?? 0,
      vatCode: line.vatCode.trim() || null,
    })),
  };
}

/** Net − the stock value of the same quantity at the received cost: what is reported to Finance. */
export const priceDifference = (
  line: Pick<InvoiceLineForm, 'quantity' | 'unitPrice' | 'receivedUnitCost'>,
) =>
  roundKm(
    lineAmount(line.quantity, line.unitPrice) - lineAmount(line.quantity, line.receivedUnitCost),
  );

export interface VatSummaryRow {
  vatCode: string;
  rate: number | null;
  base: number;
  vat: number | null;
}

/**
 * A preview of the PDV per rate while the invoice is edited, by the same rule the server applies (each line's PDV
 * rounded to fening, then summed per code). The rate is the code's latest rate on or before the invoice date; a code
 * without one shows no PDV. The saved invoice's amounts come from the server.
 */
export function vatSummary(
  lines: Pick<InvoiceLineForm, 'quantity' | 'unitPrice' | 'vatCode'>[],
  rates: VatRateResponse[],
  date: string,
) {
  const rateOf = (code: string) =>
    rates
      .filter((rate) => rate.code === code && rate.validFrom <= date)
      .sort((a, b) => b.validFrom.localeCompare(a.validFrom))[0];
  const rows = new Map<string, VatSummaryRow>();
  for (const line of lines) {
    const code = line.vatCode.trim();
    const net = lineAmount(line.quantity, line.unitPrice);
    const rate = rateOf(code);
    const row = rows.get(code) ?? {
      vatCode: code,
      rate: rate ? Number(rate.rate) : null,
      base: 0,
      vat: rate ? 0 : null,
    };
    row.base = roundKm(row.base + net);
    if (row.vat !== null && row.rate !== null) row.vat = roundKm(row.vat + roundKm(net * row.rate));
    rows.set(code, row);
  }

  const summary = [...rows.values()].sort(
    (a, b) => (b.rate ?? -1) - (a.rate ?? -1) || a.vatCode.localeCompare(b.vatCode),
  );
  const net = roundKm(summary.reduce((sum, row) => sum + row.base, 0));
  const vat = summary.every((row) => row.vat !== null)
    ? roundKm(summary.reduce((sum, row) => sum + (row.vat ?? 0), 0))
    : null;
  return { rows: summary, net, vat, gross: vat === null ? null : roundKm(net + vat) };
}
