import type { Messages } from '../bs-BA';

export default {
  app: {
    name: 'ERP',
  },
  common: {
    loading: 'Loading…',
    search: 'Search',
    retry: 'Try again',
    yes: 'Yes',
    no: 'No',
    comingSoon: 'Coming soon',
  },
  auth: {
    title: 'Sign in',
    subtitle: 'Sign in with your user account',
    email: 'Email',
    password: 'Password',
    signIn: 'Sign in',
    signOut: 'Sign out',
    sessionExpired: 'Your session has expired. Please sign in again.',
    errors: {
      invalidCredentials: 'Wrong email or password.',
      lockedOut:
        'The account is temporarily locked after too many failed sign-ins. Try again later.',
      tooManyRequests: 'Too many attempts. Wait a minute and try again.',
      unavailable: 'The server is unreachable. Check your connection and try again.',
      generic: 'Sign-in failed.',
    },
  },
  validation: {
    required: 'Required',
  },
  layout: {
    home: 'Home',
    toggleNavigation: 'Show or hide navigation',
    language: 'Language',
    company: 'Company',
    userMenu: 'User menu',
    noCompany: 'Your account is not a member of any company yet.',
  },
  home: {
    title: 'Welcome, {name}',
    subtitle: 'Choose a module in the navigation.',
  },
  modules: {
    core: {
      title: 'Master data',
      partners: 'Business partners',
      articles: 'Articles',
      unitsOfMeasure: 'Units of measure',
    },
    sales: {
      title: 'Sales',
    },
    purchasing: {
      title: 'Purchasing',
    },
    inventory: {
      title: 'Inventory',
    },
    accounting: {
      title: 'Accounting',
    },
    administration: {
      title: 'Administration',
      users: 'Users',
      roles: 'Roles',
    },
  },
  partners: {
    title: 'Business partners',
    searchHint: 'Code or name',
    columns: {
      code: 'Code',
      name: 'Name',
      taxId: 'Tax ID',
      vatNumber: 'VAT number',
      city: 'City',
      type: 'Type',
      status: 'Status',
    },
    customer: 'Customer',
    supplier: 'Supplier',
    active: 'Active',
    inactive: 'Inactive',
    empty: 'No business partners.',
    loadFailed: 'Loading business partners failed.',
    rowsPerPage: 'Rows per page',
    pagination: '{first}–{last} of {total}',
  },
  placeholder: {
    description: 'This part of the application is under construction.',
  },
  errors: {
    notFound: 'Page not found.',
    forbidden: 'You do not have access to this page.',
    goHome: 'Go to home',
  },
  languages: {
    'bs-BA': 'Bosanski',
    'en-US': 'English',
  },
} satisfies Messages;
