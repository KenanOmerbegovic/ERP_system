<template>
  <q-page padding>
    <q-table
      v-model:pagination="pagination"
      :title="t('warehouse.documents.title')"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[10, 25, 50, 100]"
      :rows-per-page-label="t('warehouse.rowsPerPage')"
      :pagination-label="paginationLabel"
      :no-data-label="t('warehouse.documents.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      @request="load"
      @row-click="(_event: Event, row: StockDocumentSummaryResponse) => open(row.id)"
    >
      <template #top-right>
        <div class="row q-col-gutter-sm items-center">
          <q-select
            v-model="type"
            :options="typeOptions"
            :label="t('warehouse.documents.type')"
            emit-value
            map-options
            dense
            outlined
            style="min-width: 170px"
          />
          <q-select
            v-model="status"
            :options="statusOptions"
            :label="t('warehouse.documents.status')"
            emit-value
            map-options
            dense
            outlined
            style="min-width: 140px"
          />
          <q-input
            v-model="search"
            :label="t('common.search')"
            :hint="t('warehouse.documents.searchHint')"
            debounce="300"
            dense
            outlined
            clearable
          >
            <template #append><q-icon name="search" /></template>
          </q-input>
          <q-btn
            v-if="canWrite"
            color="primary"
            icon="add"
            :label="t('warehouse.documents.add')"
            @click="open('new')"
          />
        </div>
      </template>
      <template #body-cell-status="props">
        <q-td :props="props">
          <q-badge :color="props.row.status === 'Confirmed' ? 'positive' : 'grey-6'">
            {{ t(`warehouse.statuses.${props.row.status}`) }}
          </q-badge>
          <q-badge v-if="props.row.reversesDocumentId" color="negative" class="q-ml-xs">
            {{ t('warehouse.documents.storno') }}
          </q-badge>
          <q-badge v-if="props.row.reversedByDocumentId" outline color="negative" class="q-ml-xs">
            {{ t('warehouse.documents.reversed') }}
          </q-badge>
        </q-td>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('warehouse.documents.loadFailed') }}
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
import { useRouter } from 'vue-router';

import { listStockDocuments } from '@/api/generated/erp-api';
import {
  StockDocumentKind,
  StockDocumentState,
  type StockDocumentSummaryResponse,
} from '@/api/generated/model';
import { WarehousePermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { useNumberFormat, useWarehouses } from './shared';

type RequestProps = Parameters<NonNullable<QTableProps['onRequest']>>[0];

const { t } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const format = useNumberFormat();
const { codeOf } = useWarehouses();

const rows = ref<StockDocumentSummaryResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const search = ref<string | null>('');
const type = ref<StockDocumentKind | null>(null);
const status = ref<StockDocumentState | null>(null);
const pagination = ref({ sortBy: '', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 });

const canWrite = computed(() => auth.can(WarehousePermissions.stockDocumentsWrite));

const typeOptions = computed(() => [
  { label: t('common.all'), value: null },
  ...Object.values(StockDocumentKind).map((kind) => ({
    label: t(`warehouse.types.${kind}`),
    value: kind,
  })),
]);
const statusOptions = computed(() => [
  { label: t('common.all'), value: null },
  ...Object.values(StockDocumentState).map((state) => ({
    label: t(`warehouse.statuses.${state}`),
    value: state,
  })),
]);

const columns = computed<QTableColumn<StockDocumentSummaryResponse>[]>(() => [
  {
    name: 'number',
    label: t('warehouse.documents.number'),
    field: (row) => row.number ?? '—',
    align: 'left',
  },
  {
    name: 'type',
    label: t('warehouse.documents.type'),
    field: (row) => t(`warehouse.types.${row.type}`),
    align: 'left',
  },
  {
    name: 'date',
    label: t('warehouse.documents.date'),
    field: (row) => format.date(row.documentDate),
    align: 'left',
  },
  {
    name: 'source',
    label: t('warehouse.documents.source'),
    field: (row) => codeOf(row.sourceWarehouseId),
    align: 'left',
  },
  {
    name: 'target',
    label: t('warehouse.documents.target'),
    field: (row) => codeOf(row.targetWarehouseId),
    align: 'left',
  },
  { name: 'lines', label: t('warehouse.documents.lines'), field: 'lineCount', align: 'right' },
  {
    name: 'value',
    label: t('warehouse.documents.value'),
    field: (row) => format.amount(row.totalValue),
    align: 'right',
  },
  { name: 'status', label: t('warehouse.documents.status'), field: 'status', align: 'left' },
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
    const result = await listStockDocuments(companyId, {
      page,
      pageSize: rowsPerPage,
      ...(search.value ? { search: search.value } : {}),
      ...(type.value ? { type: type.value } : {}),
      ...(status.value ? { status: status.value } : {}),
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

function open(id: string) {
  void router.push({ name: 'stock-document', params: { id } });
}

onMounted(reload);
watch([search, type, status, () => auth.companyId], reload);
</script>
