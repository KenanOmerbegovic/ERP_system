import type { RouteRecordRaw } from 'vue-router';

import { navigation } from './navigation';
import { CorePermissions } from './permissions';

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

/** Navigation items without a page yet. */
const placeholders: RouteRecordRaw[] = navigation
  .flatMap((module) => module.items)
  .filter((item) => item.route !== 'partners')
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
