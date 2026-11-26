<template>
  <q-page padding>
    <q-table
      v-model:pagination="pagination"
      :title="title"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[10, 25, 50, 100]"
      :rows-per-page-label="t('warehouse.rowsPerPage')"
      :pagination-label="paginationLabel"
      :no-data-label="t('sales.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      @request="load"
      @row-click="(_event: Event, row: T) => emit('open', row.id)"
    >
      <template #top-right>
        <div class="row q-col-gutter-sm items-center">
          <q-select
            v-model="status"
            :options="statusOptions"
            :label="t('sales.status')"
            emit-value
            map-options
            dense
            outlined
            style="min-width: 150px"
          />
          <q-input
            v-model="search"
            :label="t('common.search')"
            :hint="t('sales.searchHint')"
            debounce="300"
            dense
            outlined
            clearable
          >
            <template #append><q-icon name="search" /></template>
          </q-input>
          <q-btn
            v-if="addLabel"
            color="primary"
            icon="add"
            :label="addLabel"
            @click="emit('open', 'new')"
          />
        </div>
      </template>
      <template #body-cell-status="props">
        <q-td :props="props">
          <sales-status-badges v-bind="badges(props.row)" />
        </q-td>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('sales.loadFailed') }}
      <template #action>
        <q-btn flat color="negative" :label="t('common.retry')" @click="reload" />
      </template>
    </q-banner>
  </q-page>
</template>

<script setup lang="ts" generic="T extends { id: string }">
import type { QTableColumn, QTableProps } from 'quasar';
import { onMounted, ref, watch, type Ref } from 'vue';
import { useI18n } from 'vue-i18n';

import type { OfferState, SalesDocumentState } from '@/api/generated/model';
import { useAuthStore } from '@/stores/auth';

import SalesStatusBadges from './SalesStatusBadges.vue';
import type { ListPage, ListQuery } from './shared';

type RequestProps = Parameters<NonNullable<QTableProps['onRequest']>>[0];

/** A paged, searchable list of one kind of sales document; a row click (or "new") emits the id to open. */
const props = defineProps<{
  title: string;
  /** Shown only to users who may create the document. */
  addLabel?: string | undefined;
  columns: QTableColumn<T>[];
  statusOptions: { label: string; value: string | null }[];
  fetch: (companyId: string, query: ListQuery) => Promise<ListPage<T>>;
  badges: (row: T) => {
    status: SalesDocumentState | OfferState;
    reverses?: string | null | undefined;
    reversedBy?: string | null | undefined;
  };
}>();

const emit = defineEmits<{ open: [id: string] }>();

const { t } = useI18n();
const auth = useAuthStore();

const rows = ref([]) as Ref<T[]>;
const loading = ref(false);
const failed = ref(false);
const search = ref<string | null>('');
const status = ref<string | null>(null);
const pagination = ref({ sortBy: '', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 });

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
    const result = await props.fetch(companyId, {
      page,
      pageSize: rowsPerPage,
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

onMounted(reload);
watch([search, status, () => auth.companyId], reload);
</script>
