<template>
  <q-page padding>
    <q-table
      v-model:pagination="pagination"
      :title="t('warehouse.stock.title')"
      :rows="rows"
      :columns="columns"
      :row-key="(row: StockBalanceResponse) => `${row.warehouseId}/${row.articleId}`"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[10, 25, 50, 100]"
      :rows-per-page-label="t('warehouse.rowsPerPage')"
      :pagination-label="paginationLabel"
      :no-data-label="t('warehouse.stock.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      @request="load"
    >
      <template #top-right>
        <div class="row q-col-gutter-sm items-center">
          <q-select
            v-model="warehouseId"
            :options="warehouseOptions"
            :label="t('warehouse.stock.warehouse')"
            emit-value
            map-options
            dense
            outlined
            style="min-width: 200px"
          />
          <q-input
            v-model="search"
            :label="t('common.search')"
            :hint="t('warehouse.stock.searchHint')"
            debounce="300"
            dense
            outlined
            clearable
          >
            <template #append><q-icon name="search" /></template>
          </q-input>
          <q-toggle v-model="includeEmpty" :label="t('warehouse.stock.includeEmpty')" />
        </div>
      </template>
      <template #body-cell-lots="props">
        <q-td :props="props">
          <div v-for="lot in props.row.lots" :key="lot.lotNumber" class="text-caption">
            {{ lot.lotNumber }}: {{ format.quantity(lot.quantity) }}
            <span v-if="lot.expiryDate" class="text-grey-7">
              ({{ t('warehouse.stock.expiry', { date: format.date(lot.expiryDate) }) }})
            </span>
          </div>
        </q-td>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('warehouse.stock.loadFailed') }}
      <template #action>
        <q-btn flat color="negative" :label="t('common.retry')" @click="reload" />
      </template>
    </q-banner>
  </q-page>
</template>

<script setup lang="ts">
import type { QTableColumn, QTableProps } from 'quasar';
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';

import { listStockBalances } from '@/api/generated/erp-api';
import type { StockBalanceResponse } from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

import { useNumberFormat, useWarehouses } from './shared';

type RequestProps = Parameters<NonNullable<QTableProps['onRequest']>>[0];

const { t } = useI18n();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();

const rows = ref<StockBalanceResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const search = ref<string | null>('');
const warehouseId = ref<string | null>(null);
const includeEmpty = ref(false);
const pagination = ref({ sortBy: '', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 });

const warehouseOptions = computed(() => [
  { label: t('warehouse.stock.allWarehouses'), value: null },
  ...warehouses.value.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id })),
]);

const columns = computed<QTableColumn<StockBalanceResponse>[]>(() => [
  {
    name: 'articleCode',
    label: t('warehouse.stock.articleCode'),
    field: 'articleCode',
    align: 'left',
  },
  { name: 'article', label: t('warehouse.stock.article'), field: 'articleName', align: 'left' },
  {
    name: 'warehouse',
    label: t('warehouse.stock.warehouse'),
    field: 'warehouseCode',
    align: 'left',
  },
  {
    name: 'quantity',
    label: t('warehouse.stock.quantity'),
    field: 'quantity',
    format: (value: number | string) => format.quantity(value),
    align: 'right',
  },
  {
    name: 'averageCost',
    label: t('warehouse.stock.averageCost'),
    field: 'averageCost',
    format: (value: number | string) => format.price(value),
    align: 'right',
  },
  {
    name: 'value',
    label: t('warehouse.stock.value'),
    field: 'value',
    format: (value: number | string) => format.amount(value),
    align: 'right',
  },
  { name: 'lots', label: t('warehouse.stock.lots'), field: 'lots', align: 'left' },
]);

const paginationLabel = (first: number, last: number, total: number) =>
  t('warehouse.pagination', { first, last, total });

async function load(request: Pick<RequestProps, 'pagination'>) {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  const { page = 1, rowsPerPage = pagination.value.rowsPerPage } = request.pagination;
  loading.value = true;
  failed.value = false;
  try {
    const result = await listStockBalances(companyId, {
      page,
      pageSize: rowsPerPage,
      includeEmpty: includeEmpty.value,
      ...(search.value ? { search: search.value } : {}),
      ...(warehouseId.value ? { warehouseId: warehouseId.value } : {}),
    });
    rows.value = result.items;
    pagination.value = {
      ...pagination.value,
      page: Number(result.page),
      rowsPerPage: Number(result.pageSize),
      rowsNumber: Number(result.totalCount),
    };
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

function reload() {
  void load({ pagination: { ...pagination.value, page: 1 } });
}

onMounted(reload);
watch([search, warehouseId, includeEmpty, () => auth.companyId], reload);
</script>
