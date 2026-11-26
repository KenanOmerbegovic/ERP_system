import { useQuasar } from 'quasar';
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';

import { listArticles, listBusinessPartners, listVatRates } from '@/api/generated/erp-api';
import {
  ArticleKind,
  type ArticleResponse,
  type BusinessPartnerResponse,
  type VatRateResponse,
} from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

import { problemCode } from '../warehouse/stock-document-form';

type Update = (callback: () => void) => void;

/** Active suppliers for a q-select with server-side search (Core's business partners). */
export function useSupplierPicker() {
  const auth = useAuthStore();
  const suppliers = ref<BusinessPartnerResponse[]>([]);
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
          suppliers.value = result.items.filter((p) => p.isSupplier && p.isActive);
        }),
      )
      .catch(() => update(() => (suppliers.value = [])));
  }

  return { suppliers, filter, label: (p: BusinessPartnerResponse) => `${p.code} – ${p.name}` };
}

/** Active goods for a q-select with server-side search (Core's articles). */
export function useArticlePicker() {
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
          articles.value = result.items.filter((a) => a.type === ArticleKind.Goods && a.isActive);
        }),
      )
      .catch(() => update(() => (articles.value = [])));
  }

  return { articles, filter, label: (a: ArticleResponse) => `${a.code} – ${a.name}` };
}

/** The company's VAT rates (history included), for VAT code pickers and the PDV preview. */
export function useVatRates() {
  const auth = useAuthStore();
  const rates = ref<VatRateResponse[]>([]);
  async function load() {
    if (auth.companyId) {
      try {
        rates.value = await listVatRates(auth.companyId);
      } catch {
        rates.value = [];
      }
    }
  }

  const codes = () => [...new Set(rates.value.map((rate) => rate.code))];
  return { rates, load, codes };
}

/** Confirmation questions, notifications and error texts shared by the purchasing document pages. */
export function useDocumentFeedback() {
  const { t } = useI18n();
  const $q = useQuasar();

  const ask = (question: string, action: () => Promise<void>) =>
    $q.dialog({ message: question, cancel: true, persistent: true }).onOk(() => void action());

  const notify = (message: string) => $q.notify({ type: 'positive', message });

  function failureMessage(error: unknown, fallback: string): string {
    switch (problemCode(error)) {
      case 'NumberSeries.NotConfigured':
        return t('purchasing.noNumberSeries');
      case 'PurchaseOrder.HasReceipts':
        return t('purchasing.orders.hasReceipts');
      case 'PurchaseOrder.NotOpen':
        return t('purchasing.orders.notOpen');
      case 'GoodsReceipt.Invoiced':
        return t('purchasing.receipts.invoiced');
      case 'SupplierInvoice.DuplicateNumber':
        return t('purchasing.invoices.duplicateNumber');
      case 'SerialNumber.AlreadyInStock':
        return t('warehouse.document.serialInStock');
      case 'Lot.ExpiryMismatch':
        return t('warehouse.document.lotExpiryMismatch');
      default:
        return fallback;
    }
  }

  return { ask, notify, failureMessage };
}
