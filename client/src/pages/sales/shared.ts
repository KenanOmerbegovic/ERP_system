import { useQuasar } from 'quasar';
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';

import { listArticles, listBusinessPartners } from '@/api/generated/erp-api';
import {
  ArticleKind,
  type ArticleResponse,
  type BusinessPartnerResponse,
} from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

import { problemCode } from '../warehouse/stock-document-form';

export { useVatRates } from '../purchasing/shared';

type Update = (callback: () => void) => void;

/** What a sales list page asks for and gets (see SalesDocumentList.vue). */
export interface ListQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: string;
}

export interface ListPage<T> {
  items: T[];
  page: number | string;
  pageSize: number | string;
  totalCount: number | string;
}

/** Active customers for a q-select with server-side search (Core's business partners). */
export function useCustomerPicker() {
  const auth = useAuthStore();
  const customers = ref<BusinessPartnerResponse[]>([]);
  function filter(text: string, update: Update) {
    const companyId = auth.companyId;
    if (!companyId) {
      update(() => undefined);
      return;
    }

    void listBusinessPartners(companyId, {
      page: 1,
      pageSize: 20,
      ...(text ? { search: text } : {}),
    })
      .then((result) =>
        update(() => {
          customers.value = result.items.filter((p) => p.isCustomer && p.isActive);
        }),
      )
      .catch(() => update(() => (customers.value = [])));
  }

  return { customers, filter, label: (p: BusinessPartnerResponse) => `${p.code} – ${p.name}` };
}

/** Active goods and services for a q-select with server-side search (Core's articles). */
export function useSellableArticlePicker() {
  const auth = useAuthStore();
  const articles = ref<ArticleResponse[]>([]);
  function filter(text: string, update: Update) {
    const companyId = auth.companyId;
    if (!companyId) {
      update(() => undefined);
      return;
    }

    void listArticles(companyId, { page: 1, pageSize: 20, ...(text ? { search: text } : {}) })
      .then((result) =>
        update(() => {
          articles.value = result.items.filter((a) => a.isActive);
        }),
      )
      .catch(() => update(() => (articles.value = [])));
  }

  return {
    articles,
    filter,
    label: (a: ArticleResponse) => `${a.code} – ${a.name}`,
    isService: (a: ArticleResponse) => a.type === ArticleKind.Service,
  };
}

/** Confirmation questions, notifications and error texts shared by the sales document pages. */
export function useSalesFeedback() {
  const { t } = useI18n();
  const $q = useQuasar();

  const ask = (question: string, action: () => Promise<void>) =>
    $q.dialog({ message: question, cancel: true, persistent: true }).onOk(() => void action());

  const notify = (message: string) => $q.notify({ type: 'positive', message });

  function failureMessage(error: unknown, fallback: string): string {
    switch (problemCode(error)) {
      case 'NumberSeries.NotConfigured':
        return t('sales.noNumberSeries');
      case 'Offer.NotDraft':
        return t('sales.offers.notDraft');
      case 'Offer.NotSent':
        return t('sales.offers.notSent');
      case 'SalesOrder.HasDeliveries':
        return t('sales.orders.hasDeliveries');
      case 'SalesOrder.NotOpen':
        return t('sales.orders.notOpen');
      case 'DeliveryNote.Invoiced':
        return t('sales.deliveries.invoiced');
      case 'SalesInvoice.Credited':
        return t('sales.invoices.credited');
      case 'SalesInvoice.NotOpen':
        return t('sales.invoices.notOpen');
      case 'SerialNumber.AlreadyInStock':
        return t('warehouse.document.serialInStock');
      default:
        return fallback;
    }
  }

  return { ask, notify, failureMessage };
}
