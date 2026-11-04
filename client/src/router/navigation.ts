import { CorePermissions, IdentityPermissions } from './permissions';

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
 * The module navigation. Only Core has working pages so far; its other items and the
 * administration pages lead to a placeholder page but are already permission-checked.
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
    ],
  },
  { key: 'sales', icon: 'point_of_sale', items: [] },
  { key: 'purchasing', icon: 'local_shipping', items: [] },
  { key: 'inventory', icon: 'warehouse', items: [] },
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
