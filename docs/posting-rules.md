# Posting rules

Finance (Phase 3, FR-FIN-02) posts general ledger entries from the integration events the modules publish through
their outboxes. Accounts are company configuration (effective-dated, CLAUDE.md: no hardcoded accounts); below they are
named by **role**, and the Phase 3 feature maps roles to the company's chart of accounts (FR-FIN-01). All amounts are
KM. A storno publishes the same event with `Reverses…Id` set and every quantity and amount negated (red storno), so
it posts the same entry with negative amounts.

## Purchasing

| Event | When | Debit | Credit |
| --- | --- | --- | --- |
| `PurchaseOrderConfirmed` | Order confirmed | — (commitment only, no posting) | — |
| `GoodsReceived` | Goods receipt confirmed | Stock (`TotalValue`) | Goods received not invoiced (`TotalValue`) |
| `SupplierInvoiceRegistered` | Supplier invoice registered | Goods received not invoiced (Σ `ReceivedValue`); Input PDV (`VatAmount`, per `VatTotals`); Price differences (Σ `PriceDifference`, may be negative) | Payables to the supplier (`GrossAmount`) |

## Sales

| Event | When | Debit | Credit |
| --- | --- | --- | --- |
| `SalesOrderConfirmed` | Order confirmed | — (commitment only, no posting) | — |
| `GoodsDelivered` | Delivery note confirmed (stock issued) | Cost of goods sold (`CostValue`) | Stock (`CostValue`) |
| `InvoiceIssued` | Invoice confirmed | Receivables from the customer (`GrossAmount`) | Revenue from goods / from services (Σ `NetAmount` of lines with `IsService` false / true); Output PDV (`VatAmount`, per `VatTotals`) |
| `CreditNoteIssued` | Credit note confirmed | Revenue (Σ `NetAmount` of lines); Output PDV (`VatAmount`, per `VatTotals`) | Receivables from the customer (`GrossAmount`) |
| `CreditNoteIssued`, lines with `Kind` = Return | Same credit note | Stock (`ReturnedCostValue`) | Cost of goods sold (`ReturnedCostValue`) |

Notes:

- The cost of goods sold is posted when goods leave the warehouse (`GoodsDelivered`), not when they are invoiced, at
  the warehouse's weighted average cost; the event carries the cost per line (`GoodsDeliveredLine.CostValue`).
- Services never pass through the warehouse: they appear only in `InvoiceIssued` (and in credit notes as value
  reductions).
- A credit note's PDV is computed at the rates of the invoice it corrects. Returned goods come back at the cost per
  unit of their delivery, so the cost of goods sold is reversed by what they left at.
- PDV per rate (`VatTotals`) feeds the PDV return (FR-FIN-05): input PDV from supplier invoices, output PDV from
  invoices and credit notes, by document date.
- Warehouse publishes `StockReceived`, `StockIssued` and `StockTransferred` for every confirmed stock document,
  including those Purchasing and Sales create through the command contract (their ids are the `StockDocumentId` in
  `GoodsReceived`, `GoodsDelivered` and `CreditNoteIssued`). Those must not be posted twice; see "Stock events of
  documents other modules own" in docs/open-decisions.md. Rules for stock documents users make directly in the
  Warehouse module are decided with Finance.
