import { PermissionCode } from '@/api/generated/model';

/**
 * Permission codes the client checks, grouped per module. The values come from the generated `PermissionCode`
 * (Erp.Api publishes every module's codes in its OpenAPI document), so a permission renamed or removed on the server
 * fails the type check here instead of silently never matching. GET /api/permissions returns the same set.
 */
export const CorePermissions = {
  companiesRead: PermissionCode.CoreCompaniesRead,
  businessPartnersRead: PermissionCode.CoreBusinessPartnersRead,
  articlesRead: PermissionCode.CoreArticlesRead,
  unitsOfMeasureRead: PermissionCode.CoreUnitsOfMeasureRead,
  vatRatesRead: PermissionCode.CoreVatRatesRead,
  vatRatesWrite: PermissionCode.CoreVatRatesWrite,
} as const;

export const IdentityPermissions = {
  usersRead: PermissionCode.IdentityUsersRead,
  rolesRead: PermissionCode.IdentityRolesRead,
} as const;

export const WarehousePermissions = {
  warehousesRead: PermissionCode.WarehouseWarehousesRead,
  warehousesWrite: PermissionCode.WarehouseWarehousesWrite,
  stockDocumentsRead: PermissionCode.WarehouseStockDocumentsRead,
  stockDocumentsWrite: PermissionCode.WarehouseStockDocumentsWrite,
  stockDocumentsConfirm: PermissionCode.WarehouseStockDocumentsConfirm,
  stockRead: PermissionCode.WarehouseStockRead,
  settingsWrite: PermissionCode.WarehouseSettingsWrite,
} as const;

export const PurchasingPermissions = {
  ordersRead: PermissionCode.PurchasingOrdersRead,
  ordersWrite: PermissionCode.PurchasingOrdersWrite,
  ordersConfirm: PermissionCode.PurchasingOrdersConfirm,
  receiptsRead: PermissionCode.PurchasingReceiptsRead,
  receiptsWrite: PermissionCode.PurchasingReceiptsWrite,
  receiptsConfirm: PermissionCode.PurchasingReceiptsConfirm,
  invoicesRead: PermissionCode.PurchasingInvoicesRead,
  invoicesWrite: PermissionCode.PurchasingInvoicesWrite,
  invoicesConfirm: PermissionCode.PurchasingInvoicesConfirm,
  settingsWrite: PermissionCode.PurchasingSettingsWrite,
} as const;

export const SalesPermissions = {
  offersRead: PermissionCode.SalesOffersRead,
  offersWrite: PermissionCode.SalesOffersWrite,
  offersSend: PermissionCode.SalesOffersSend,
  ordersRead: PermissionCode.SalesOrdersRead,
  ordersWrite: PermissionCode.SalesOrdersWrite,
  ordersConfirm: PermissionCode.SalesOrdersConfirm,
  deliveriesRead: PermissionCode.SalesDeliveriesRead,
  deliveriesWrite: PermissionCode.SalesDeliveriesWrite,
  deliveriesConfirm: PermissionCode.SalesDeliveriesConfirm,
  invoicesRead: PermissionCode.SalesInvoicesRead,
  invoicesWrite: PermissionCode.SalesInvoicesWrite,
  invoicesConfirm: PermissionCode.SalesInvoicesConfirm,
  creditNotesRead: PermissionCode.SalesCreditnotesRead,
  creditNotesWrite: PermissionCode.SalesCreditnotesWrite,
  creditNotesConfirm: PermissionCode.SalesCreditnotesConfirm,
  settingsWrite: PermissionCode.SalesSettingsWrite,
} as const;
