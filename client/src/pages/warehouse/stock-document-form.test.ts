import { AxiosError, AxiosHeaders } from 'axios';
import { describe, expect, it } from 'vitest';

import { ArticleTracking, StockDocumentKind } from '@/api/generated/model';

import {
  documentProblems,
  emptyDocument,
  emptyLine,
  parseSerialNumbers,
  problemCode,
  shortagesOf,
  toRequest,
  type DocumentForm,
  type LineForm,
} from './stock-document-form';

const line = (changes: Partial<LineForm>): LineForm => ({
  ...emptyLine(),
  articleId: 'article-1',
  quantity: 1,
  unitCost: 2,
  ...changes,
});

const receipt = (lines: LineForm[]): DocumentForm => ({
  ...emptyDocument('2026-11-25'),
  targetWarehouseId: 'warehouse-1',
  lines,
});

function problem(status: number, data: unknown): AxiosError {
  const headers = new AxiosHeaders();
  return new AxiosError('failed', 'ERR_BAD_REQUEST', { headers }, undefined, {
    status,
    statusText: '',
    headers,
    config: { headers },
    data,
  });
}

describe('stock document form', () => {
  it('FR_WH_02 splits serial numbers on lines, commas and semicolons', () => {
    expect(parseSerialNumbers(' SN-1\nSN-2, SN-3;;\n ')).toEqual(['SN-1', 'SN-2', 'SN-3']);
  });

  it('FR_WH_02 needs one serial number per piece', () => {
    const serial = line({ tracking: ArticleTracking.Serial, quantity: 2, serialNumbers: 'SN-1' });

    expect(documentProblems(receipt([serial]))).toContain('serialCount');
    expect(documentProblems(receipt([{ ...serial, serialNumbers: 'SN-1\nSN-2' }]))).toEqual([]);
  });

  it('FR_WH_02 needs a LOT for LOT tracked articles', () => {
    expect(documentProblems(receipt([line({ tracking: ArticleTracking.Lot })]))).toEqual([
      'lotRequired',
    ]);
  });

  it('FR_WH_01 needs a unit cost on receipts only', () => {
    const noCost = line({ unitCost: null });

    expect(documentProblems(receipt([noCost]))).toEqual(['unitCostRequired']);
    expect(
      documentProblems({
        ...receipt([noCost]),
        type: StockDocumentKind.Issue,
        targetWarehouseId: null,
        sourceWarehouseId: 'warehouse-1',
      }),
    ).toEqual([]);
  });

  it('FR_WH_01 needs the warehouses of its type, two different ones for a transfer', () => {
    const transfer: DocumentForm = {
      ...receipt([line({ unitCost: null })]),
      type: StockDocumentKind.Transfer,
      sourceWarehouseId: 'warehouse-1',
    };

    expect(documentProblems(transfer)).toEqual(['sameWarehouse']);
    expect(documentProblems({ ...transfer, sourceWarehouseId: null })).toEqual([
      'warehouseRequired',
    ]);
  });

  it('FR_WH_01 sends only the fields its type and tracking use', () => {
    const issue: DocumentForm = {
      ...receipt([
        line({ tracking: ArticleTracking.Lot, lotNumber: ' L-1 ', lotExpiryDate: '2027-01-01' }),
        line({ tracking: ArticleTracking.Serial, serialNumbers: 'SN-1' }),
      ]),
      type: StockDocumentKind.Issue,
      sourceWarehouseId: 'warehouse-2',
      note: '  ',
    };

    const request = toRequest(issue);

    expect(request.targetWarehouseId).toBeNull();
    expect(request.sourceWarehouseId).toBe('warehouse-2');
    expect(request.note).toBeNull();
    expect(request.lines[0]).toMatchObject({
      unitCost: null,
      lotNumber: 'L-1',
      lotExpiryDate: null,
      serialNumbers: [],
    });
    expect(request.lines[1]).toMatchObject({ lotNumber: null, serialNumbers: ['SN-1'] });
  });

  it('FR_WH_03 reads the shortages of a Stock.Insufficient problem', () => {
    const shortage = {
      warehouseId: 'w',
      articleId: 'a',
      articleCode: 'A001',
      lotNumber: null,
      serialNumber: null,
      requested: 12,
      available: 10,
    };

    expect(
      shortagesOf(problem(409, { code: 'Stock.Insufficient', shortages: [shortage] })),
    ).toEqual([shortage]);
    expect(shortagesOf(problem(409, { code: 'StockDocument.NotDraft' }))).toBeNull();
    expect(problemCode(problem(422, { code: 'NumberSeries.NotConfigured' }))).toBe(
      'NumberSeries.NotConfigured',
    );
    expect(shortagesOf(new Error('network'))).toBeNull();
  });
});
