<template>
  <q-page padding>
    <q-table
      v-model:pagination="pagination"
      :title="t('partners.title')"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :filter="search"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[10, 25, 50, 100]"
      :rows-per-page-label="t('partners.rowsPerPage')"
      :pagination-label="paginationLabel"
      :no-data-label="t('partners.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      @request="load"
    >
      <template #top-right>
        <q-input
          v-model="search"
          :label="t('common.search')"
          :hint="t('partners.searchHint')"
          debounce="300"
          dense
          outlined
          clearable
        >
          <template #append><q-icon name="search" /></template>
        </q-input>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('partners.loadFailed') }}
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

import { listBusinessPartners } from '@/api/generated/erp-api';
import type { BusinessPartnerResponse } from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

type RequestProps = Parameters<NonNullable<QTableProps['onRequest']>>[0];

const { t } = useI18n();
const auth = useAuthStore();

const rows = ref<BusinessPartnerResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const search = ref<string | null>('');
const pagination = ref({ sortBy: '', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 });

// Search and paging happen on the server, so no column is sortable here.
const columns = computed<QTableColumn<BusinessPartnerResponse>[]>(() => [
  { name: 'code', label: t('partners.columns.code'), field: 'code', align: 'left' },
  { name: 'name', label: t('partners.columns.name'), field: 'name', align: 'left' },
  {
    name: 'taxId',
    label: t('partners.columns.taxId'),
    field: (row) => row.taxId ?? '',
    align: 'left',
  },
  {
    name: 'vatNumber',
    label: t('partners.columns.vatNumber'),
    field: (row) => row.vatNumber ?? '',
    align: 'left',
  },
  {
    name: 'city',
    label: t('partners.columns.city'),
    field: (row) => row.city ?? '',
    align: 'left',
  },
  {
    name: 'type',
    label: t('partners.columns.type'),
    field: (row) =>
      [
        row.isCustomer ? t('partners.customer') : null,
        row.isSupplier ? t('partners.supplier') : null,
      ]
        .filter((kind) => kind !== null)
        .join(', '),
    align: 'left',
  },
  {
    name: 'status',
    label: t('partners.columns.status'),
    field: (row) => (row.isActive ? t('partners.active') : t('partners.inactive')),
    align: 'left',
  },
]);

const paginationLabel = (first: number, last: number, total: number) =>
  t('partners.pagination', { first, last, total });

async function load(request: Pick<RequestProps, 'pagination'>) {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  const { page = 1, rowsPerPage = pagination.value.rowsPerPage } = request.pagination;
  loading.value = true;
  failed.value = false;
  try {
    const result = await listBusinessPartners(companyId, {
      page,
      pageSize: rowsPerPage,
      ...(search.value ? { search: search.value } : {}),
    });
    rows.value = result.items;
    // The OpenAPI document types integers as number | string (.NET allows numeric strings).
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
  void load({ pagination: { ...pagination.value } });
}

onMounted(reload);

// Another company's partners: start again on page one.
watch(
  () => auth.companyId,
  () => {
    pagination.value.page = 1;
    reload();
  },
);
</script>
