import { describe, expect, it } from 'vitest';

import {
  ArticleTracking,
  PurchaseDocumentState,
  type PurchaseOrderResponse,
  type VatRateResponse,
} from '@/api/generated/model';

import {
  emptyOrder,
  invoiceLinesFrom,
  invoiceProblems,
  orderProblems,
  priceDifference,
  receiptFromOrder,
  receiptProblems,
  roundKm,
  toOrderRequest,
  toReceiptRequest,
  vatSummary,
  type InvoiceForm,
} from './purchasing-form';

const order: PurchaseOrderResponse = {
  id: 'order-1',
  status: PurchaseDocumentState.Confirmed,
  number: 'NAR-2026-00001',
  supplierId: 's',
  supplierCode: 'D001',
  supplierName: 'Dobavljač',
  orderDate: '2026-11-01',
  expectedDate: null,
  warehouseId: 'w',
  note: null,
  reversesOrderId: null,
  reversedByOrderId: null,
  confirmedAt: null,
  netAmount: 0,
  lines: [
    {
      id: 'l1',
      lineNumber: 1,
      articleId: 'a1',
      articleCode: 'A1',
      articleName: 'Brašno',
      tracking: ArticleTracking.Lot,
      quantity: 10,
      unitPrice: 2,
      vatCode: 'PDV17',
      netAmount: 20,
      receivedQuantity: 4,
      invoicedQuantity: 0,
      openQuantity: 6,
    },
    {
      id: 'l2',
      lineNumber: 2,
      articleId: 'a2',
      articleCode: 'A2',
      articleName: 'Vijak',
      tracking: ArticleTracking.None,
      quantity: 5,
      unitPrice: 1,
      vatCode: 'PDV17',
      netAmount: 5,
      receivedQuantity: 5,
      invoicedQuantity: 5,
      openQuantity: 0,
    },
  ],
};

const rates: VatRateResponse[] = [
  { id: '1', code: 'PDV17', name: 'Opća', rate: 0.17, validFrom: '2006-01-01' },
  { id: '2', code: 'PDV17', name: 'Opća', rate: 0.18, validFrom: '2027-01-01' },
  { id: '3', code: 'OSL', name: 'Oslobođeno', rate: 0, validFrom: '2006-01-01' },
];

describe('purchasing forms', () => {
  it('rounds KM like the server (midpoints away from zero)', () => {
    expect(roundKm(0.125)).toBe(0.13);
    expect(roundKm(2.0978)).toBe(2.1);
    expect(roundKm(-0.125)).toBe(-0.13);
  });

  it('FR_PUR_01 order needs a supplier, articles, quantities and prices; an empty VAT code means the default', () => {
    const form = emptyOrder('2026-11-25');

    expect(orderProblems(form)).toEqual([
      'supplierRequired',
      'articleRequired',
      'quantityPositive',
      'priceRequired',
    ]);
    form.supplierId = 's';
    form.lines[0] = { articleId: 'a', articleLabel: '', quantity: 2, unitPrice: 1.5, vatCode: ' ' };
    expect(orderProblems(form)).toEqual([]);
    expect(toOrderRequest(form).lines[0]).toEqual({
      articleId: 'a',
      quantity: 2,
      unitPrice: 1.5,
      vatCode: null,
    });
  });

  it('FR_PUR_01 a receipt starts with what is still open, at the order price', () => {
    const receipt = receiptFromOrder(order, '2026-11-25');

    expect(receipt.lines.map((line) => [line.orderLineId, line.quantity, line.unitCost])).toEqual([
      ['l1', 6, 2],
    ]);
    expect(receipt.warehouseId).toBe('w');
  });

  it('FR_PUR_01 a receipt cannot take more than is open, and respects LOT tracking', () => {
    const receipt = receiptFromOrder(order, '2026-11-25');
    receipt.lines.push({ ...receipt.lines[0]!, quantity: 1, lotNumber: 'L2' });

    expect(receiptProblems(receipt)).toEqual(['lotRequired', 'quantityExceedsOpen']);
    receipt.lines = [{ ...receipt.lines[0]!, quantity: 6, lotNumber: ' L1 ' }];
    expect(receiptProblems(receipt)).toEqual([]);
    expect(toReceiptRequest(receipt).lines[0]).toMatchObject({
      lotNumber: 'L1',
      serialNumbers: [],
    });
  });

  it('FR_PUR_02 invoice lines start from uninvoiced receipt lines and cannot exceed them', () => {
    const lines = invoiceLinesFrom([
      {
        receiptId: 'r',
        receiptNumber: 'PRI-1',
        receiptDate: '2026-11-02',
        receiptLineId: 'rl',
        articleId: 'a',
        articleCode: 'A1',
        articleName: 'Brašno',
        receivedQuantity: 10,
        invoicedQuantity: 4,
        unitCost: 2,
        vatCode: 'PDV17',
      },
    ]);
    const form: InvoiceForm = {
      purchaseOrderId: 'order-1',
      orderLabel: '',
      supplierInvoiceNumber: 'F-1',
      invoiceDate: '2026-11-25',
      dueDate: '2026-11-20',
      note: '',
      lines,
    };

    expect(lines[0]).toMatchObject({ remainingQuantity: 6, quantity: 6, unitPrice: 2 });
    form.lines[0]!.quantity = 7;
    expect(invoiceProblems(form)).toEqual(['dueBeforeInvoice', 'quantityExceedsUninvoiced']);
  });

  it('FR_PUR_02 price difference is net minus the received value', () => {
    expect(priceDifference({ quantity: 3, unitPrice: 1.05, receivedUnitCost: 1.1 })).toBe(-0.15);
    expect(priceDifference({ quantity: 10, unitPrice: 2.15, receivedUnitCost: 2 })).toBe(1.5);
  });

  it('FR_PUR_02 previews PDV per rate with the rate valid on the invoice date', () => {
    const lines = [
      { quantity: 1, unitPrice: 100, vatCode: 'PDV17' },
      { quantity: 1, unitPrice: 12.34, vatCode: 'PDV17' },
      { quantity: 2, unitPrice: 25, vatCode: 'OSL' },
    ];

    const summary = vatSummary(lines, rates, '2026-11-25');

    expect(summary.rows).toEqual([
      { vatCode: 'PDV17', rate: 0.17, base: 112.34, vat: 19.1 },
      { vatCode: 'OSL', rate: 0, base: 50, vat: 0 },
    ]);
    expect([summary.net, summary.vat, summary.gross]).toEqual([162.34, 19.1, 181.44]);
    expect(vatSummary(lines, rates, '2027-01-01').rows[0]!.rate).toBe(0.18);
    expect(
      vatSummary([{ quantity: 1, unitPrice: 1, vatCode: 'X' }], rates, '2026-11-25').vat,
    ).toBeNull();
  });
});
