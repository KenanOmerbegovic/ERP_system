import type { RouteRecordRaw } from 'vue-router';

import { navigation } from './navigation';
import { CorePermissions, PurchasingPermissions, WarehousePermissions } from './permissions';

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable without signing in (the login page). */
    public?: boolean;
    /** Required in the current company; checked by the guard in boot/auth.ts. */
    permission?: string;
    /** Message key for a placeholder page's title. */
    titleKey?: string;
  }
}

const placeholderPage = () => import('@/pages/PlaceholderPage.vue');

/** Navigation items that have a page; every other item leads to the placeholder. */
const built = new Set([
  'partners',
  'warehouses',
  'stock-documents',
  'stock-levels',
  'warehouse-number-series',
  'vat-rates',
  'purchase-orders',
  'goods-receipts',
  'supplier-invoices',
  'purchasing-number-series',
]);

/** Navigation items without a page yet. */
const placeholders: RouteRecordRaw[] = navigation
  .flatMap((module) => module.items)
  .filter((item) => !built.has(item.route))
  .map((item) => ({
    path: item.route,
    name: item.route,
    component: placeholderPage,
    meta: { permission: item.permission, titleKey: item.labelKey },
  }));

/** Modules not built yet. */
const placeholderModules: RouteRecordRaw[] = navigation
  .filter((module) => module.items.length === 0)
  .map((module) => ({
    path: `modules/${module.key}`,
    name: `module-${module.key}`,
    component: placeholderPage,
    meta: { titleKey: `modules.${module.key}.title` },
  }));

type PageLoader = NonNullable<RouteRecordRaw['component']>;

/** A list page and its document page (id "new" opens an empty draft), e.g. purchase-orders and purchase-orders/:id. */
function documentRoutes(
  list: string,
  item: string,
  permission: string,
  listPage: PageLoader,
  itemPage: PageLoader,
): RouteRecordRaw[] {
  return [
    { path: list, name: list, component: listPage, meta: { permission } },
    { path: `${list}/:id`, name: item, component: itemPage, props: true, meta: { permission } },
  ];
}

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    component: () => import('@/layouts/AuthLayout.vue'),
    children: [
      {
        path: '',
        name: 'login',
        component: () => import('@/pages/LoginPage.vue'),
        meta: { public: true },
      },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('@/pages/HomePage.vue') },
      {
        path: 'partners',
        name: 'partners',
        component: () => import('@/pages/partners/PartnersPage.vue'),
        meta: { permission: CorePermissions.businessPartnersRead },
      },
      {
        path: 'warehouses',
        name: 'warehouses',
        component: () => import('@/pages/warehouse/WarehousesPage.vue'),
        meta: { permission: WarehousePermissions.warehousesRead },
      },
      {
        path: 'stock-documents',
        name: 'stock-documents',
        component: () => import('@/pages/warehouse/StockDocumentsPage.vue'),
        meta: { permission: WarehousePermissions.stockDocumentsRead },
      },
      {
        // id "new" opens an empty draft.
        path: 'stock-documents/:id',
        name: 'stock-document',
        component: () => import('@/pages/warehouse/StockDocumentPage.vue'),
        props: true,
        meta: { permission: WarehousePermissions.stockDocumentsRead },
      },
      {
        path: 'stock-levels',
        name: 'stock-levels',
        component: () => import('@/pages/warehouse/StockLevelsPage.vue'),
        meta: { permission: WarehousePermissions.stockRead },
      },
      {
        path: 'warehouse-number-series',
        name: 'warehouse-number-series',
        component: () => import('@/pages/warehouse/NumberSeriesPage.vue'),
        meta: { permission: WarehousePermissions.stockDocumentsRead },
      },
      {
        path: 'vat-rates',
        name: 'vat-rates',
        component: () => import('@/pages/core/VatRatesPage.vue'),
        meta: { permission: CorePermissions.vatRatesRead },
      },
      ...documentRoutes(
        'purchase-orders',
        'purchase-order',
        PurchasingPermissions.ordersRead,
        () => import('@/pages/purchasing/PurchaseOrdersPage.vue'),
        () => import('@/pages/purchasing/PurchaseOrderPage.vue'),
      ),
      ...documentRoutes(
        'goods-receipts',
        'goods-receipt',
        PurchasingPermissions.receiptsRead,
        () => import('@/pages/purchasing/GoodsReceiptsPage.vue'),
        () => import('@/pages/purchasing/GoodsReceiptPage.vue'),
      ),
      ...documentRoutes(
        'supplier-invoices',
        'supplier-invoice',
        PurchasingPermissions.invoicesRead,
        () => import('@/pages/purchasing/SupplierInvoicesPage.vue'),
        () => import('@/pages/purchasing/SupplierInvoicePage.vue'),
      ),
      {
        path: 'purchasing-number-series',
        name: 'purchasing-number-series',
        component: () => import('@/pages/purchasing/PurchasingNumberSeriesPage.vue'),
        meta: { permission: PurchasingPermissions.ordersRead },
      },
      ...placeholders,
      ...placeholderModules,
      {
        path: 'forbidden',
        name: 'forbidden',
        component: () => import('@/pages/ErrorForbidden.vue'),
      },
    ],
  },

  // Always leave this as last one
  {
    path: '/:catchAll(.*)*',
    component: () => import('@/pages/ErrorNotFound.vue'),
    meta: { public: true },
  },
];

export default routes;
