/**
 * Permission codes the client checks, as declared by each module's Contracts project
 * (Erp.Modules.Core.Contracts/CorePermissions.cs, Erp.Modules.Identity.Contracts IdentityPermissions,
 * Erp.Modules.Warehouse.Contracts/WarehousePermissions.cs, Erp.Modules.Purchasing.Contracts/PurchasingPermissions.cs).
 * The OpenAPI document does not carry them, so they are listed here; GET /api/permissions returns the full set.
 */
export const CorePermissions = {
  companiesRead: 'core.companies.read',
  businessPartnersRead: 'core.business-partners.read',
  articlesRead: 'core.articles.read',
  unitsOfMeasureRead: 'core.units-of-measure.read',
  vatRatesRead: 'core.vat-rates.read',
  vatRatesWrite: 'core.vat-rates.write',
} as const;

export const IdentityPermissions = {
  usersRead: 'identity.users.read',
  rolesRead: 'identity.roles.read',
} as const;

export const WarehousePermissions = {
  warehousesRead: 'warehouse.warehouses.read',
  warehousesWrite: 'warehouse.warehouses.write',
  stockDocumentsRead: 'warehouse.stock-documents.read',
  stockDocumentsWrite: 'warehouse.stock-documents.write',
  stockDocumentsConfirm: 'warehouse.stock-documents.confirm',
  stockRead: 'warehouse.stock.read',
  settingsWrite: 'warehouse.settings.write',
} as const;

export const PurchasingPermissions = {
  ordersRead: 'purchasing.orders.read',
  ordersWrite: 'purchasing.orders.write',
  ordersConfirm: 'purchasing.orders.confirm',
  receiptsRead: 'purchasing.receipts.read',
  receiptsWrite: 'purchasing.receipts.write',
  receiptsConfirm: 'purchasing.receipts.confirm',
  invoicesRead: 'purchasing.invoices.read',
  invoicesWrite: 'purchasing.invoices.write',
  invoicesConfirm: 'purchasing.invoices.confirm',
  settingsWrite: 'purchasing.settings.write',
} as const;
