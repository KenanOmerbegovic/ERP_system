import {
  CorePermissions,
  IdentityPermissions,
  PurchasingPermissions,
  SalesPermissions,
  WarehousePermissions,
} from './permissions';

/** A message key under `modules` in src/i18n. */
type ModuleKey = 'core' | 'sales' | 'purchasing' | 'inventory' | 'accounting' | 'administration';

export interface NavigationItem {
  /** Route name. */
  readonly route: string;
  readonly labelKey: string;
  readonly icon: string;
  /** Hidden from users who lack it in the current company. */
  readonly permission: string;
}

export interface NavigationModule {
  readonly key: ModuleKey;
  readonly icon: string;
  /** Empty for modules that are not built yet: they show as placeholders. */
  readonly items: readonly NavigationItem[];
}

/**
 * The module navigation. Core's partners and VAT rates, and the sales, purchasing and warehouse pages work; the other items lead to
 * a placeholder page but are already permission-checked.
 */
export const navigation: readonly NavigationModule[] = [
  {
    key: 'core',
    icon: 'inventory_2',
    items: [
      {
        route: 'partners',
        labelKey: 'modules.core.partners',
        icon: 'handshake',
        permission: CorePermissions.businessPartnersRead,
      },
      {
        route: 'articles',
        labelKey: 'modules.core.articles',
        icon: 'category',
        permission: CorePermissions.articlesRead,
      },
      {
        route: 'units-of-measure',
        labelKey: 'modules.core.unitsOfMeasure',
        icon: 'straighten',
        permission: CorePermissions.unitsOfMeasureRead,
      },
      {
        route: 'vat-rates',
        labelKey: 'modules.core.vatRates',
        icon: 'percent',
        permission: CorePermissions.vatRatesRead,
      },
    ],
  },
  {
    key: 'sales',
    icon: 'point_of_sale',
    items: [
      {
        route: 'offers',
        labelKey: 'modules.sales.offers',
        icon: 'request_page',
        permission: SalesPermissions.offersRead,
      },
      {
        route: 'sales-orders',
        labelKey: 'modules.sales.orders',
        icon: 'shopping_cart',
        permission: SalesPermissions.ordersRead,
      },
      {
        route: 'delivery-notes',
        labelKey: 'modules.sales.deliveries',
        icon: 'local_shipping',
        permission: SalesPermissions.deliveriesRead,
      },
      {
        route: 'sales-invoices',
        labelKey: 'modules.sales.invoices',
        icon: 'receipt',
        permission: SalesPermissions.invoicesRead,
      },
      {
        route: 'credit-notes',
        labelKey: 'modules.sales.creditNotes',
        icon: 'assignment_return',
        permission: SalesPermissions.creditNotesRead,
      },
      {
        route: 'sales-number-series',
        labelKey: 'modules.sales.numberSeries',
        icon: 'pin',
        permission: SalesPermissions.ordersRead,
      },
    ],
  },
  {
    key: 'purchasing',
    icon: 'local_shipping',
    items: [
      {
        route: 'purchase-orders',
        labelKey: 'modules.purchasing.orders',
        icon: 'assignment',
        permission: PurchasingPermissions.ordersRead,
      },
      {
        route: 'goods-receipts',
        labelKey: 'modules.purchasing.receipts',
        icon: 'move_to_inbox',
        permission: PurchasingPermissions.receiptsRead,
      },
      {
        route: 'supplier-invoices',
        labelKey: 'modules.purchasing.invoices',
        icon: 'request_quote',
        permission: PurchasingPermissions.invoicesRead,
      },
      {
        route: 'purchasing-number-series',
        labelKey: 'modules.purchasing.numberSeries',
        icon: 'pin',
        permission: PurchasingPermissions.ordersRead,
      },
    ],
  },
  {
    key: 'inventory',
    icon: 'warehouse',
    items: [
      {
        route: 'stock-levels',
        labelKey: 'modules.inventory.stockLevels',
        icon: 'inventory',
        permission: WarehousePermissions.stockRead,
      },
      {
        route: 'stock-documents',
        labelKey: 'modules.inventory.stockDocuments',
        icon: 'receipt_long',
        permission: WarehousePermissions.stockDocumentsRead,
      },
      {
        route: 'warehouses',
        labelKey: 'modules.inventory.warehouses',
        icon: 'store',
        permission: WarehousePermissions.warehousesRead,
      },
      {
        route: 'warehouse-number-series',
        labelKey: 'modules.inventory.numberSeries',
        icon: 'pin',
        permission: WarehousePermissions.stockDocumentsRead,
      },
    ],
  },
  { key: 'accounting', icon: 'account_balance', items: [] },
  {
    key: 'administration',
    icon: 'admin_panel_settings',
    items: [
      {
        route: 'users',
        labelKey: 'modules.administration.users',
        icon: 'group',
        permission: IdentityPermissions.usersRead,
      },
      {
        route: 'roles',
        labelKey: 'modules.administration.roles',
        icon: 'badge',
        permission: IdentityPermissions.rolesRead,
      },
    ],
  },
];
