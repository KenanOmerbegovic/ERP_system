import { describe, expect, it } from 'vitest';

import {
  ArticleKind,
  ArticleTracking,
  CreditNoteLineKind,
  SalesDocumentState,
  type SalesInvoiceResponse,
  type SalesOrderResponse,
} from '@/api/generated/model';

import {
  activeCreditLines,
  creditAmount,
  creditLinesFrom,
  creditNoteProblems,
  deliveryFromOrder,
  deliveryProblems,
  emptyOffer,
  invoiceLinesFrom,
  invoiceProblems,
  offerProblems,
  toCreditNoteRequest,
  toInvoiceRequest,
  toOfferRequest,
  type CreditNoteForm,
  type InvoiceForm,
} from './sales-form';

const orderLine = (
  overrides: Partial<SalesOrderResponse['lines'][number]>,
): SalesOrderResponse['lines'][number] => ({
  id: 'ol-1',
  lineNumber: 1,
  articleId: 'a-1',
  articleCode: 'A1',
  articleName: 'Vijak',
  kind: ArticleKind.Goods,
  tracking: ArticleTracking.None,
  quantity: 5,
  unitPrice: 10,
  vatCode: 'PDV17',
  netAmount: 50,
  deliveredQuantity: 2,
  invoicedQuantity: 0,
  openQuantity: 3,
  invoiceableQuantity: 2,
  ...overrides,
});

const order: SalesOrderResponse = {
  id: 'order-1',
  status: SalesDocumentState.Confirmed,
  number: 'NRK-2026-00001',
  customerId: 'c',
  customerCode: 'K001',
  customerName: 'Kupac',
  orderDate: '2026-11-01',
  warehouseId: 'w',
  note: null,
  offerId: null,
  offerNumber: null,
  reversesOrderId: null,
  reversedByOrderId: null,
  confirmedAt: null,
  netAmount: 0,
  lines: [
    orderLine({}),
    orderLine({ id: 'ol-2', lineNumber: 2, tracking: ArticleTracking.Serial, openQuantity: 1 }),
    orderLine({ id: 'ol-3', lineNumber: 3, kind: ArticleKind.Service, openQuantity: 0 }),
    orderLine({ id: 'ol-4', lineNumber: 4, openQuantity: 0 }),
  ],
};

const invoice: SalesInvoiceResponse = {
  id: 'inv-1',
  status: SalesDocumentState.Confirmed,
  number: 'IFK-2026-00001',
  salesOrderId: 'order-1',
  orderNumber: 'NRK-2026-00001',
  customerId: 'c',
  customerName: 'Kupac',
  customerTaxId: null,
  customerVatNumber: null,
  invoiceDate: '2026-11-02',
  dueDate: '2026-11-17',
  note: null,
  reversesInvoiceId: null,
  reversedByInvoiceId: null,
  confirmedAt: null,
  netAmount: 80,
  vatAmount: 13.6,
  grossAmount: 93.6,
  creditedAmount: 0,
  vatTotals: [],
  lines: [
    {
      id: 'il-1',
      lineNumber: 1,
      orderLineId: 'ol-1',
      deliveryNoteId: 'd',
      deliveryLineId: 'dl-1',
      articleId: 'a-1',
      articleCode: 'A1',
      articleName: 'Vijak',
      kind: ArticleKind.Goods,
      quantity: 3,
      unitPrice: 10,
      vatCode: 'PDV17',
      vatRate: 0.17,
      netAmount: 30,
      vatAmount: 5.1,
      creditedAmount: 10,
      returnedQuantity: 1,
    },
    {
      id: 'il-2',
      lineNumber: 2,
      orderLineId: 'ol-3',
      deliveryNoteId: null,
      deliveryLineId: null,
      articleId: 'a-3',
      articleCode: 'U1',
      articleName: 'Montaža',
      kind: ArticleKind.Service,
      quantity: 1,
      unitPrice: 50,
      vatCode: 'PDV17',
      vatRate: 0.17,
      netAmount: 50,
      vatAmount: 8.5,
      creditedAmount: 0,
      returnedQuantity: 0,
    },
  ],
};

describe('offers', () => {
  it('needs a customer, priced lines and a validity not before the offer date', () => {
    const form = { ...emptyOffer('2026-11-02'), validUntil: '2026-11-01' };

    expect(offerProblems(form)).toEqual(
      expect.arrayContaining([
        'customerRequired',
        'articleRequired',
        'quantityPositive',
        'priceRequired',
        'validUntilBeforeDate',
      ]),
    );
  });

  it('sends an empty VAT code as the article default', () => {
    const form = {
      ...emptyOffer('2026-11-02'),
      customerId: 'c',
      lines: [
        {
          articleId: 'a',
          articleLabel: '',
          isService: false,
          quantity: 2,
          unitPrice: 3,
          vatCode: ' ',
        },
      ],
    };

    expect(toOfferRequest(form).lines[0]).toEqual({
      articleId: 'a',
      quantity: 2,
      unitPrice: 3,
      vatCode: null,
    });
  });
});

describe('delivery notes', () => {
  it('start with every open goods line, never services', () => {
    const form = deliveryFromOrder(order, '2026-11-03');

    expect(form.lines.map((l) => [l.orderLineId, l.quantity])).toEqual([
      ['ol-1', 3],
      ['ol-2', 1],
    ]);
    expect(form.warehouseId).toBe('w');
  });

  it('refuse more than is open and serial numbers that do not match the quantity', () => {
    const form = deliveryFromOrder(order, '2026-11-03');
    form.lines[0]!.quantity = 4;

    expect(deliveryProblems(form)).toEqual(
      expect.arrayContaining(['quantityExceedsOpen', 'serialCount']),
    );
  });
});

describe('invoices', () => {
  it('take delivered goods by delivery line and services by order line', () => {
    const lines = invoiceLinesFrom([
      {
        orderLineId: 'ol-1',
        deliveryNoteId: 'd',
        deliveryNoteNumber: 'OTP-1',
        deliveryDate: '2026-11-03',
        deliveryLineId: 'dl-1',
        articleId: 'a-1',
        articleCode: 'A1',
        articleName: 'Vijak',
        kind: ArticleKind.Goods,
        quantity: 2,
        invoicedQuantity: 0,
        unitPrice: 10,
        vatCode: 'PDV17',
      },
      {
        orderLineId: 'ol-3',
        deliveryNoteId: null,
        deliveryNoteNumber: null,
        deliveryDate: null,
        deliveryLineId: null,
        articleId: 'a-3',
        articleCode: 'U1',
        articleName: 'Montaža',
        kind: ArticleKind.Service,
        quantity: 2,
        invoicedQuantity: 1,
        unitPrice: 50,
        vatCode: 'PDV17',
      },
    ]);
    const form: InvoiceForm = {
      salesOrderId: 'order-1',
      orderLabel: '',
      invoiceDate: '2026-11-04',
      dueDate: '2026-11-04',
      note: '',
      lines,
    };

    expect(toInvoiceRequest(form).lines).toEqual([
      { deliveryLineId: 'dl-1', orderLineId: null, quantity: 2 },
      { deliveryLineId: null, orderLineId: 'ol-3', quantity: 1 },
    ]);
    lines[1]!.quantity = 2;
    expect(invoiceProblems({ ...form, dueDate: '2026-11-01' })).toEqual([
      'dueBeforeInvoice',
      'quantityExceedsUninvoiced',
    ]);
  });
});

describe('credit notes', () => {
  const form = (): CreditNoteForm => ({
    invoiceId: 'inv-1',
    invoiceLabel: '',
    creditNoteDate: '2026-11-10',
    warehouseId: null,
    note: '',
    lines: creditLinesFrom(invoice),
  });

  it('offer returns for goods and value reductions for services, with what is left', () => {
    const lines = creditLinesFrom(invoice);

    expect(lines.map((l) => [l.kind, l.returnableQuantity, l.creditableAmount])).toEqual([
      [CreditNoteLineKind.Return, 2, 20],
      [CreditNoteLineKind.ValueReduction, 0, 50],
    ]);
  });

  it('credit a return at the invoice price and need a warehouse for it', () => {
    const credit = form();
    credit.lines[0]!.quantity = 2;

    expect(creditAmount(credit.lines[0]!)).toBe(20);
    expect(creditNoteProblems(credit)).toEqual(['warehouseRequired']);
    expect(toCreditNoteRequest({ ...credit, warehouseId: 'w' })).toMatchObject({
      warehouseId: 'w',
      lines: [
        { invoiceLineId: 'il-1', kind: CreditNoteLineKind.Return, quantity: 2, netAmount: null },
      ],
    });
  });

  it('refuse more than the invoice line has left and leave out untouched lines', () => {
    const credit = form();
    credit.lines[0]!.quantity = 3;
    credit.lines[1]!.netAmount = 50.01;

    expect(creditNoteProblems({ ...credit, warehouseId: 'w' })).toEqual(
      expect.arrayContaining(['quantityExceedsReturnable', 'amountExceedsCreditable']),
    );
    credit.lines[0]!.quantity = null;
    credit.lines[1]!.netAmount = 12.345;
    expect(activeCreditLines(credit).map((l) => l.invoiceLineId)).toEqual(['il-2']);
    expect(toCreditNoteRequest(credit)).toMatchObject({
      warehouseId: null,
      lines: [{ netAmount: 12.35 }],
    });
  });
});
