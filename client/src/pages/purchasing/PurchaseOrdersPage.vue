<template>
  <q-page padding>
    <q-table
      v-model:pagination="pagination"
      :title="t('purchasing.orders.title')"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[10, 25, 50, 100]"
      :rows-per-page-label="t('warehouse.rowsPerPage')"
      :pagination-label="paginationLabel"
      :no-data-label="t('purchasing.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      @request="load"
      @row-click="(_event: Event, row: PurchaseOrderSummaryResponse) => open(row.id)"
    >
      <template #top-right>
        <div class="row q-col-gutter-sm items-center">
          <q-select
            v-model="status"
            :options="statusOptions"
            :label="t('purchasing.status')"
            emit-value
            map-options
            dense
            outlined
            style="min-width: 150px"
          />
          <q-toggle v-model="openOnly" :label="t('purchasing.orders.openOnly')" />
          <q-input
            v-model="search"
            :label="t('common.search')"
            :hint="t('purchasing.searchHint')"
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
            :label="t('purchasing.orders.add')"
            @click="open('new')"
          />
        </div>
      </template>
      <template #body-cell-status="props">
        <q-td :props="props">
          <document-status-badges
            :row="props.row"
            :reverses="props.row.reversesOrderId"
            :reversed-by="props.row.reversedByOrderId"
          />
          <q-badge
            v-if="props.row.fullyReceived && props.row.status === 'Confirmed'"
            outline
            color="positive"
            class="q-ml-xs"
          >
            {{ t('purchasing.orders.fullyReceived') }}
          </q-badge>
        </q-td>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('purchasing.loadFailed') }}
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

import { listPurchaseOrders } from '@/api/generated/erp-api';
import { PurchaseDocumentState, type PurchaseOrderSummaryResponse } from '@/api/generated/model';
import { PurchasingPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { useNumberFormat } from '../warehouse/shared';
import DocumentStatusBadges from './DocumentStatusBadges.vue';

type RequestProps = Parameters<NonNullable<QTableProps['onRequest']>>[0];

const { t } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const format = useNumberFormat();

const rows = ref<PurchaseOrderSummaryResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const search = ref<string | null>('');
const status = ref<PurchaseDocumentState | null>(null);
const openOnly = ref(false);
const pagination = ref({ sortBy: '', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 });
const canWrite = computed(() => auth.can(PurchasingPermissions.ordersWrite));

const statusOptions = computed(() => [
  { label: t('common.all'), value: null },
  { label: t('purchasing.statuses.Draft'), value: PurchaseDocumentState.Draft },
  { label: t('purchasing.statuses.Confirmed'), value: PurchaseDocumentState.Confirmed },
]);

const columns = computed<QTableColumn<PurchaseOrderSummaryResponse>[]>(() => [
  {
    name: 'number',
    label: t('purchasing.number'),
    field: (row) => row.number ?? '—',
    align: 'left',
  },
  {
    name: 'date',
    label: t('purchasing.date'),
    field: (row) => format.date(row.orderDate),
    align: 'left',
  },
  { name: 'supplier', label: t('purchasing.supplier'), field: 'supplierName', align: 'left' },
  {
    name: 'net',
    label: t('purchasing.net'),
    field: (row) => format.amount(row.netAmount),
    align: 'right',
  },
  { name: 'status', label: t('purchasing.status'), field: 'status', align: 'left' },
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
    const result = await listPurchaseOrders(companyId, {
      page,
      pageSize: rowsPerPage,
      openOnly: openOnly.value,
      ...(search.value ? { search: search.value } : {}),
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
  void router.push({ name: 'purchase-order', params: { id } });
}

onMounted(reload);
watch([search, status, openOnly, () => auth.companyId], reload);
</script>
