/**
 * Permission codes the client checks, as declared by each module's Contracts project
 * (Erp.Modules.Core.Contracts/CorePermissions.cs, Erp.Modules.Identity.Contracts IdentityPermissions,
 * Erp.Modules.Warehouse.Contracts/WarehousePermissions.cs).
 * The OpenAPI document does not carry them, so they are listed here; GET /api/permissions returns the full set.
 */
export const CorePermissions = {
  companiesRead: 'core.companies.read',
  businessPartnersRead: 'core.business-partners.read',
  articlesRead: 'core.articles.read',
  unitsOfMeasureRead: 'core.units-of-measure.read',
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
