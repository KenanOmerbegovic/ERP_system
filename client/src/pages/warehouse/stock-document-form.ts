import { isAxiosError } from 'axios';

import {
  ArticleTracking,
  StockDocumentKind,
  type StockDocumentRequest,
  type StockDocumentResponse,
  type StockShortageResponse,
} from '@/api/generated/model';

/** One line as the editor holds it; serial numbers are typed one per line (or separated by commas). */
export interface LineForm {
  articleId: string | null;
  /** Code and name, shown in the article picker. */
  articleLabel: string;
  tracking: ArticleTracking;
  quantity: number | null;
  unitCost: number | null;
  lotNumber: string;
  lotExpiryDate: string | null;
  serialNumbers: string;
}

export interface DocumentForm {
  type: StockDocumentKind;
  /** yyyy-MM-dd */
  documentDate: string;
  sourceWarehouseId: string | null;
  targetWarehouseId: string | null;
  note: string;
  lines: LineForm[];
}

/** Message keys under warehouse.problems in src/i18n. */
export type Problem =
  | 'articleRequired'
  | 'quantityPositive'
  | 'unitCostRequired'
  | 'lotRequired'
  | 'serialCount'
  | 'warehouseRequired'
  | 'sameWarehouse'
  | 'linesRequired';

export const needsSource = (type: StockDocumentKind) => type !== StockDocumentKind.Receipt;
export const needsTarget = (type: StockDocumentKind) => type !== StockDocumentKind.Issue;
export const hasUnitCost = (type: StockDocumentKind) => type === StockDocumentKind.Receipt;

export function emptyLine(): LineForm {
  return {
    articleId: null,
    articleLabel: '',
    tracking: ArticleTracking.None,
    quantity: null,
    unitCost: null,
    lotNumber: '',
    lotExpiryDate: null,
    serialNumbers: '',
  };
}

export function emptyDocument(today: string): DocumentForm {
  return {
    type: StockDocumentKind.Receipt,
    documentDate: today,
    sourceWarehouseId: null,
    targetWarehouseId: null,
    note: '',
    lines: [emptyLine()],
  };
}

/** Serial numbers from free text: one per line or separated by commas or semicolons; blanks dropped. */
export function parseSerialNumbers(text: string): string[] {
  return text
    .split(/[\n,;]/)
    .map((serial) => serial.trim())
    .filter((serial) => serial.length > 0);
}

/** The same checks the API makes, so the user sees them before saving. The API stays the authority. */
export function lineProblems(line: LineForm, type: StockDocumentKind): Problem[] {
  const problems: Problem[] = [];
  if (!line.articleId) {
    problems.push('articleRequired');
  }

  if (line.quantity === null || !(line.quantity > 0)) {
    problems.push('quantityPositive');
  }

  if (hasUnitCost(type) && (line.unitCost === null || line.unitCost < 0)) {
    problems.push('unitCostRequired');
  }

  if (line.tracking === ArticleTracking.Lot && line.lotNumber.trim() === '') {
    problems.push('lotRequired');
  }

  if (
    line.tracking === ArticleTracking.Serial &&
    parseSerialNumbers(line.serialNumbers).length !== line.quantity
  ) {
    problems.push('serialCount');
  }

  return problems;
}

export function documentProblems(form: DocumentForm): Problem[] {
  const problems = new Set<Problem>();
  if (
    (needsSource(form.type) && !form.sourceWarehouseId) ||
    (needsTarget(form.type) && !form.targetWarehouseId)
  ) {
    problems.add('warehouseRequired');
  }

  if (
    form.type === StockDocumentKind.Transfer &&
    form.sourceWarehouseId !== null &&
    form.sourceWarehouseId === form.targetWarehouseId
  ) {
    problems.add('sameWarehouse');
  }

  if (form.lines.length === 0) {
    problems.add('linesRequired');
  }

  form.lines.forEach((line) => lineProblems(line, form.type).forEach((p) => problems.add(p)));
  return [...problems];
}

/** The request body; fields the document type does not use are left out (null). */
export function toRequest(form: DocumentForm): StockDocumentRequest {
  return {
    type: form.type,
    documentDate: form.documentDate,
    sourceWarehouseId: needsSource(form.type) ? form.sourceWarehouseId : null,
    targetWarehouseId: needsTarget(form.type) ? form.targetWarehouseId : null,
    note: form.note.trim() === '' ? null : form.note.trim(),
    lines: form.lines.map((line) => ({
      articleId: line.articleId ?? '',
      quantity: line.quantity ?? 0,
      unitCost: hasUnitCost(form.type) ? line.unitCost : null,
      lotNumber: line.tracking === ArticleTracking.Lot ? line.lotNumber.trim() : null,
      lotExpiryDate:
        line.tracking === ArticleTracking.Lot && hasUnitCost(form.type) ? line.lotExpiryDate : null,
      serialNumbers:
        line.tracking === ArticleTracking.Serial ? parseSerialNumbers(line.serialNumbers) : [],
    })),
  };
}

/** A saved document back into the editor. The response does not carry tracking; it is inferred from the line. */
export function fromResponse(document: StockDocumentResponse): DocumentForm {
  return {
    type: document.type,
    documentDate: document.documentDate,
    sourceWarehouseId: document.sourceWarehouseId,
    targetWarehouseId: document.targetWarehouseId,
    note: document.note ?? '',
    lines: document.lines.map((line) => ({
      articleId: line.articleId,
      articleLabel: `${line.articleCode} – ${line.articleName}`,
      tracking:
        line.serialNumbers.length > 0
          ? ArticleTracking.Serial
          : line.lotNumber
            ? ArticleTracking.Lot
            : ArticleTracking.None,
      quantity: Number(line.quantity),
      unitCost: line.unitCost === null ? null : Number(line.unitCost),
      lotNumber: line.lotNumber ?? '',
      lotExpiryDate: line.lotExpiryDate,
      serialNumbers: line.serialNumbers.join('\n'),
    })),
  };
}

interface ProblemBody {
  code?: string;
  shortages?: StockShortageResponse[] | null;
}

/** The error code of a problem response (e.g. Stock.Insufficient), or null for other failures. */
export function problemCode(error: unknown): string | null {
  if (!isAxiosError<ProblemBody>(error)) {
    return null;
  }

  return error.response?.data?.code ?? null;
}

/** The shortages of a 409 Stock.Insufficient, or null for any other failure. */
export function shortagesOf(error: unknown): StockShortageResponse[] | null {
  if (!isAxiosError<ProblemBody>(error) || error.response?.data?.code !== 'Stock.Insufficient') {
    return null;
  }

  return error.response.data.shortages ?? [];
}
