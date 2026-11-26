import {
  ArticleKind,
  ArticleTracking,
  CreditNoteLineKind,
  type CreditNoteRequest,
  type CreditNoteResponse,
  type DeliveryNoteRequest,
  type DeliveryNoteResponse,
  type InvoiceableLineResponse,
  type OfferRequest,
  type OfferResponse,
  type SalesInvoiceRequest,
  type SalesInvoiceResponse,
  type SalesOrderRequest,
  type SalesOrderResponse,
} from '@/api/generated/model';

import { lineAmount, roundKm } from '../purchasing/purchasing-form';
import { parseSerialNumbers } from '../warehouse/stock-document-form';

export { lineAmount, roundKm, vatSummary } from '../purchasing/purchasing-form';

/** Message keys under sales.problems in src/i18n. */
export type Problem =
  | 'customerRequired'
  | 'warehouseRequired'
  | 'orderRequired'
  | 'invoiceRequired'
  | 'articleRequired'
  | 'quantityPositive'
  | 'priceRequired'
  | 'quantityExceedsOpen'
  | 'quantityExceedsUninvoiced'
  | 'quantityExceedsReturnable'
  | 'amountExceedsCreditable'
  | 'amountPositive'
  | 'lotRequired'
  | 'serialCount'
  | 'serviceNotReturned'
  | 'validUntilBeforeDate'
  | 'dueBeforeInvoice'
  | 'linesRequired';

const positive = (value: number | null): value is number => value !== null && value > 0;

// ---- Offers and sales orders ------------------------------------------------

export interface SalesLineForm {
  articleId: string | null;
  articleLabel: string;
  isService: boolean;
  quantity: number | null;
  unitPrice: number | null;
  /** Empty: the article's default VAT code. */
  vatCode: string;
}

export interface OfferForm {
  customerId: string | null;
  customerLabel: string;
  offerDate: string;
  validUntil: string | null;
  note: string;
  lines: SalesLineForm[];
}

export interface OrderForm {
  customerId: string | null;
  customerLabel: string;
  orderDate: string;
  warehouseId: string | null;
  note: string;
  lines: SalesLineForm[];
}

export const emptySalesLine = (): SalesLineForm => ({
  articleId: null,
  articleLabel: '',
  isService: false,
  quantity: null,
  unitPrice: null,
  vatCode: '',
});

export const emptyOffer = (today: string): OfferForm => ({
  customerId: null,
  customerLabel: '',
  offerDate: today,
  validUntil: null,
  note: '',
  lines: [emptySalesLine()],
});

export const emptyOrder = (today: string): OrderForm => ({
  customerId: null,
  customerLabel: '',
  orderDate: today,
  warehouseId: null,
  note: '',
  lines: [emptySalesLine()],
});

function lineProblems(customerId: string | null, lines: SalesLineForm[]): Set<Problem> {
  const problems = new Set<Problem>();
  if (!customerId) problems.add('customerRequired');
  if (lines.length === 0) problems.add('linesRequired');
  for (const line of lines) {
    if (!line.articleId) problems.add('articleRequired');
    if (!positive(line.quantity)) problems.add('quantityPositive');
    if (line.unitPrice === null || line.unitPrice < 0) problems.add('priceRequired');
  }

  return problems;
}

export function offerProblems(form: OfferForm): Problem[] {
  const problems = lineProblems(form.customerId, form.lines);
  if (form.validUntil && form.validUntil < form.offerDate) problems.add('validUntilBeforeDate');
  return [...problems];
}

export const orderProblems = (form: OrderForm): Problem[] => [
  ...lineProblems(form.customerId, form.lines),
];

const toLines = (lines: SalesLineForm[]) =>
  lines.map((line) => ({
    articleId: line.articleId ?? '',
    quantity: line.quantity ?? 0,
    unitPrice: line.unitPrice ?? 0,
    vatCode: line.vatCode.trim() || null,
  }));

export const toOfferRequest = (form: OfferForm): OfferRequest => ({
  customerId: form.customerId ?? '',
  offerDate: form.offerDate,
  validUntil: form.validUntil || null,
  note: form.note.trim() || null,
  lines: toLines(form.lines),
});

export const toOrderRequest = (form: OrderForm): SalesOrderRequest => ({
  customerId: form.customerId ?? '',
  orderDate: form.orderDate,
  warehouseId: form.warehouseId,
  note: form.note.trim() || null,
  lines: toLines(form.lines),
});

const linesFrom = (lines: OfferResponse['lines'] | SalesOrderResponse['lines']): SalesLineForm[] =>
  lines.map((line) => ({
    articleId: line.articleId,
    articleLabel: `${line.articleCode} – ${line.articleName}`,
    isService: line.kind === ArticleKind.Service,
    quantity: Number(line.quantity),
    unitPrice: Number(line.unitPrice),
    vatCode: line.vatCode,
  }));

export const offerFromResponse = (offer: OfferResponse): OfferForm => ({
  customerId: offer.customerId,
  customerLabel: `${offer.customerCode} – ${offer.customerName}`,
  offerDate: offer.offerDate,
  validUntil: offer.validUntil,
  note: offer.note ?? '',
  lines: linesFrom(offer.lines),
});

export const orderFromResponse = (order: SalesOrderResponse): OrderForm => ({
  customerId: order.customerId,
  customerLabel: `${order.customerCode} – ${order.customerName}`,
  orderDate: order.orderDate,
  warehouseId: order.warehouseId,
  note: order.note ?? '',
  lines: linesFrom(order.lines),
});

// ---- Delivery notes ---------------------------------------------------------

export interface DeliveryLineForm {
  orderLineId: string;
  articleLabel: string;
  tracking: ArticleTracking;
  /** Ordered minus delivered when the note was started; the server re-checks it. */
  openQuantity: number;
  quantity: number | null;
  lotNumber: string;
  serialNumbers: string;
}

export interface DeliveryForm {
  salesOrderId: string | null;
  orderLabel: string;
  warehouseId: string | null;
  deliveryDate: string;
  note: string;
  lines: DeliveryLineForm[];
}

/** A delivery note of every goods line still open on the order, from its default warehouse. */
export function deliveryFromOrder(order: SalesOrderResponse, today: string): DeliveryForm {
  return {
    salesOrderId: order.id,
    orderLabel: `${order.number ?? ''} – ${order.customerName}`,
    warehouseId: order.warehouseId,
    deliveryDate: today,
    note: '',
    lines: order.lines
      .filter((line) => line.kind === ArticleKind.Goods && Number(line.openQuantity) > 0)
      .map((line) => ({
        orderLineId: line.id,
        articleLabel: `${line.articleCode} – ${line.articleName}`,
        tracking: line.tracking,
        openQuantity: Number(line.openQuantity),
        quantity: Number(line.openQuantity),
        lotNumber: '',
        serialNumbers: '',
      })),
  };
}

export function deliveryFromResponse(
  note: DeliveryNoteResponse,
  order: SalesOrderResponse | null,
): DeliveryForm {
  const orderLines = new Map(order?.lines.map((line) => [line.id, line]) ?? []);
  return {
    salesOrderId: note.salesOrderId,
    orderLabel: `${note.orderNumber ?? ''} – ${note.customerName}`,
    warehouseId: note.warehouseId,
    deliveryDate: note.deliveryDate,
    note: note.note ?? '',
    lines: note.lines.map((line) => {
      const orderLine = orderLines.get(line.orderLineId);
      return {
        orderLineId: line.orderLineId,
        articleLabel: `${line.articleCode} – ${line.articleName}`,
        tracking: orderLine?.tracking ?? ArticleTracking.None,
        openQuantity: orderLine ? Number(orderLine.openQuantity) : Number(line.quantity),
        quantity: Number(line.quantity),
        lotNumber: line.lotNumber ?? '',
        serialNumbers: line.serialNumbers.join('\n'),
      };
    }),
  };
}

export function deliveryProblems(form: DeliveryForm): Problem[] {
  const problems = new Set<Problem>();
  if (!form.salesOrderId) problems.add('orderRequired');
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

export const toDeliveryRequest = (form: DeliveryForm): DeliveryNoteRequest => ({
  salesOrderId: form.salesOrderId ?? '',
  warehouseId: form.warehouseId ?? '',
  deliveryDate: form.deliveryDate,
  note: form.note.trim() || null,
  lines: form.lines.map((line) => ({
    orderLineId: line.orderLineId,
    quantity: line.quantity ?? 0,
    lotNumber: line.tracking === ArticleTracking.Lot ? line.lotNumber.trim() : null,
    serialNumbers:
      line.tracking === ArticleTracking.Serial ? parseSerialNumbers(line.serialNumbers) : [],
  })),
});

// ---- Invoices ---------------------------------------------------------------

export interface InvoiceLineForm {
  /** Goods: the delivery line invoiced. */
  deliveryLineId: string | null;
  /** Services: the order line invoiced. */
  orderLineId: string | null;
  sourceLabel: string;
  articleLabel: string;
  isService: boolean;
  /** Left to invoice when the line was added; the server re-checks it. */
  remainingQuantity: number;
  unitPrice: number;
  vatCode: string;
  quantity: number | null;
}

export interface InvoiceForm {
  salesOrderId: string | null;
  orderLabel: string;
  invoiceDate: string;
  dueDate: string;
  note: string;
  lines: InvoiceLineForm[];
}

/** Invoice lines for everything still to invoice: delivered goods and ordered services. */
export function invoiceLinesFrom(lines: InvoiceableLineResponse[]): InvoiceLineForm[] {
  return lines.map((line) => {
    const remaining = Number(line.quantity) - Number(line.invoicedQuantity);
    const isService = line.kind === ArticleKind.Service;
    return {
      deliveryLineId: isService ? null : (line.deliveryLineId ?? null),
      orderLineId: isService ? line.orderLineId : null,
      sourceLabel: line.deliveryNoteNumber ?? '',
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      isService,
      remainingQuantity: remaining,
      unitPrice: Number(line.unitPrice),
      vatCode: line.vatCode,
      quantity: remaining,
    };
  });
}

export const invoiceFromResponse = (invoice: SalesInvoiceResponse): InvoiceForm => ({
  salesOrderId: invoice.salesOrderId,
  orderLabel: `${invoice.orderNumber ?? ''} – ${invoice.customerName}`,
  invoiceDate: invoice.invoiceDate,
  dueDate: invoice.dueDate,
  note: invoice.note ?? '',
  lines: invoice.lines.map((line) => ({
    deliveryLineId: line.deliveryLineId ?? null,
    orderLineId: line.kind === ArticleKind.Service ? line.orderLineId : null,
    sourceLabel: '',
    articleLabel: `${line.articleCode} – ${line.articleName}`,
    isService: line.kind === ArticleKind.Service,
    // A saved draft does not reserve; its own quantity is what it may at least keep.
    remainingQuantity: Number(line.quantity),
    unitPrice: Number(line.unitPrice),
    vatCode: line.vatCode,
    quantity: Number(line.quantity),
  })),
});

export function invoiceProblems(form: InvoiceForm): Problem[] {
  const problems = new Set<Problem>();
  if (!form.salesOrderId) problems.add('orderRequired');
  if (form.dueDate < form.invoiceDate) problems.add('dueBeforeInvoice');
  if (form.lines.length === 0) problems.add('linesRequired');
  for (const line of form.lines) {
    if (!positive(line.quantity)) problems.add('quantityPositive');
    else if (line.quantity > line.remainingQuantity) problems.add('quantityExceedsUninvoiced');
  }

  return [...problems];
}

export const toInvoiceRequest = (form: InvoiceForm): SalesInvoiceRequest => ({
  salesOrderId: form.salesOrderId ?? '',
  invoiceDate: form.invoiceDate,
  dueDate: form.dueDate,
  note: form.note.trim() || null,
  lines: form.lines.map((line) => ({
    deliveryLineId: line.deliveryLineId,
    orderLineId: line.orderLineId,
    quantity: line.quantity ?? 0,
  })),
});

// ---- Credit notes -----------------------------------------------------------

export interface CreditLineForm {
  invoiceLineId: string;
  articleLabel: string;
  isService: boolean;
  kind: CreditNoteLineKind;
  unitPrice: number;
  vatCode: string;
  /** What the invoice line can still give: pieces back to stock, and KM net. */
  returnableQuantity: number;
  creditableAmount: number;
  /** Return: pieces coming back. */
  quantity: number | null;
  /** Value reduction: KM net taken off. */
  netAmount: number | null;
  serialNumbers: string;
  tracking: ArticleTracking;
}

export interface CreditNoteForm {
  invoiceId: string | null;
  invoiceLabel: string;
  creditNoteDate: string;
  warehouseId: string | null;
  note: string;
  lines: CreditLineForm[];
}

/** One line per invoice line: goods default to a return of nothing, services to a value reduction. */
export function creditLinesFrom(
  invoice: SalesInvoiceResponse,
  tracking: (articleId: string) => ArticleTracking = () => ArticleTracking.None,
): CreditLineForm[] {
  return invoice.lines.map((line) => {
    const isService = line.kind === ArticleKind.Service;
    return {
      invoiceLineId: line.id,
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      isService,
      kind: isService ? CreditNoteLineKind.ValueReduction : CreditNoteLineKind.Return,
      unitPrice: Number(line.unitPrice),
      vatCode: line.vatCode,
      returnableQuantity: isService ? 0 : Number(line.quantity) - Number(line.returnedQuantity),
      creditableAmount: roundKm(Number(line.netAmount) - Number(line.creditedAmount)),
      quantity: null,
      netAmount: null,
      serialNumbers: '',
      tracking: tracking(line.articleId),
    };
  });
}

/** A saved credit note back into the editor, with what its invoice can still give. */
export function creditNoteFromResponse(
  creditNote: CreditNoteResponse,
  invoice: SalesInvoiceResponse | null,
): CreditNoteForm {
  const invoiceLines = new Map(invoice?.lines.map((line) => [line.id, line]) ?? []);
  return {
    invoiceId: creditNote.invoiceId,
    invoiceLabel: `${creditNote.invoiceNumber ?? ''} – ${creditNote.customerName}`,
    creditNoteDate: creditNote.creditNoteDate,
    warehouseId: creditNote.warehouseId,
    note: creditNote.note ?? '',
    lines: creditNote.lines.map((line) => {
      const invoiceLine = invoiceLines.get(line.invoiceLineId);
      const isReturn = line.kind === CreditNoteLineKind.Return;
      return {
        invoiceLineId: line.invoiceLineId,
        articleLabel: `${line.articleCode} – ${line.articleName}`,
        isService: line.articleKind === ArticleKind.Service,
        kind: line.kind,
        unitPrice: invoiceLine ? Number(invoiceLine.unitPrice) : Number(line.unitPrice),
        vatCode: line.vatCode,
        // A saved draft does not reserve: what the invoice line has left, at least its own share.
        returnableQuantity: invoiceLine
          ? Number(invoiceLine.quantity) - Number(invoiceLine.returnedQuantity)
          : Number(line.quantity),
        creditableAmount: invoiceLine
          ? roundKm(Number(invoiceLine.netAmount) - Number(invoiceLine.creditedAmount))
          : Number(line.netAmount),
        quantity: isReturn ? Number(line.quantity) : null,
        netAmount: isReturn ? null : Number(line.netAmount),
        serialNumbers: line.serialNumbers.join('\n'),
        tracking: line.serialNumbers.length > 0 ? ArticleTracking.Serial : ArticleTracking.None,
      };
    }),
  };
}

/** Net KM a credit line takes off: quantity × the invoice price for a return, the amount for a reduction. */
export const creditAmount = (
  line: Pick<CreditLineForm, 'kind' | 'quantity' | 'unitPrice' | 'netAmount'>,
) =>
  line.kind === CreditNoteLineKind.Return
    ? lineAmount(line.quantity, line.unitPrice)
    : roundKm(line.netAmount ?? 0);

/** Lines that credit something; empty ones (no quantity and no amount) are left out of the request. */
export const activeCreditLines = (form: CreditNoteForm) =>
  form.lines.filter((line) =>
    line.kind === CreditNoteLineKind.Return
      ? (line.quantity ?? 0) !== 0
      : (line.netAmount ?? 0) !== 0,
  );

export function creditNoteProblems(form: CreditNoteForm): Problem[] {
  const problems = new Set<Problem>();
  const lines = activeCreditLines(form);
  if (!form.invoiceId) problems.add('invoiceRequired');
  if (lines.length === 0) problems.add('linesRequired');
  if (lines.some((line) => line.kind === CreditNoteLineKind.Return) && !form.warehouseId)
    problems.add('warehouseRequired');
  const amounts = new Map<string, number>();
  const quantities = new Map<string, number>();
  for (const line of lines) {
    if (line.kind === CreditNoteLineKind.Return) {
      if (line.isService) problems.add('serviceNotReturned');
      if (!positive(line.quantity)) {
        problems.add('quantityPositive');
        continue;
      }

      const quantity = (quantities.get(line.invoiceLineId) ?? 0) + line.quantity;
      quantities.set(line.invoiceLineId, quantity);
      if (quantity > line.returnableQuantity) problems.add('quantityExceedsReturnable');
      if (
        line.tracking === ArticleTracking.Serial &&
        parseSerialNumbers(line.serialNumbers).length !== line.quantity
      ) {
        problems.add('serialCount');
      }
    } else if (!positive(line.netAmount)) {
      problems.add('amountPositive');
      continue;
    }

    const amount = roundKm((amounts.get(line.invoiceLineId) ?? 0) + creditAmount(line));
    amounts.set(line.invoiceLineId, amount);
    if (amount > line.creditableAmount) problems.add('amountExceedsCreditable');
  }

  return [...problems];
}

export const toCreditNoteRequest = (form: CreditNoteForm): CreditNoteRequest => {
  const lines = activeCreditLines(form);
  return {
    invoiceId: form.invoiceId ?? '',
    creditNoteDate: form.creditNoteDate,
    warehouseId: lines.some((line) => line.kind === CreditNoteLineKind.Return)
      ? form.warehouseId
      : null,
    note: form.note.trim() || null,
    lines: lines.map((line) =>
      line.kind === CreditNoteLineKind.Return
        ? {
            invoiceLineId: line.invoiceLineId,
            kind: line.kind,
            quantity: line.quantity ?? 0,
            netAmount: null,
            serialNumbers: parseSerialNumbers(line.serialNumbers),
          }
        : {
            invoiceLineId: line.invoiceLineId,
            kind: line.kind,
            quantity: null,
            netAmount: roundKm(line.netAmount ?? 0),
            serialNumbers: [],
          },
    ),
  };
};
