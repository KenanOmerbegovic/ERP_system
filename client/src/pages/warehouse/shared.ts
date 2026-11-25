import { onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';

import { listWarehouses } from '@/api/generated/erp-api';
import type { WarehouseResponse } from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

/**
 * Formats a quantity or an amount from the API, which may arrive as a string (.NET decimals),
 * in the current UI language. Amounts in KM use two decimals; quantities up to four.
 */
export function useNumberFormat() {
  const { locale } = useI18n();
  const format = (value: number | string | null | undefined, minimum: number, maximum: number) =>
    value === null || value === undefined || value === ''
      ? ''
      : new Intl.NumberFormat(locale.value, {
          minimumFractionDigits: minimum,
          maximumFractionDigits: maximum,
        }).format(Number(value));
  return {
    amount: (value: number | string | null | undefined) => format(value, 2, 2),
    price: (value: number | string | null | undefined) => format(value, 2, 4),
    quantity: (value: number | string | null | undefined) => format(value, 0, 4),
    date: (value: string | null | undefined) =>
      value ? new Intl.DateTimeFormat(locale.value).format(new Date(`${value}T00:00:00`)) : '',
  };
}

/** Today in the browser's time zone as yyyy-MM-dd (the API rejects dates after its business date). */
export function todayIso(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** The company's warehouses, reloaded when the company changes. Used for pickers and code lookups. */
export function useWarehouses() {
  const auth = useAuthStore();
  const warehouses = ref<WarehouseResponse[]>([]);
  const failed = ref(false);

  async function load() {
    const companyId = auth.companyId;
    if (!companyId) {
      return;
    }

    failed.value = false;
    try {
      warehouses.value = await listWarehouses(companyId);
    } catch {
      failed.value = true;
    }
  }

  const codeOf = (id: string | null | undefined) =>
    warehouses.value.find((warehouse) => warehouse.id === id)?.code ?? '';

  onMounted(load);
  watch(() => auth.companyId, load);
  return { warehouses, failed, reload: load, codeOf };
}
