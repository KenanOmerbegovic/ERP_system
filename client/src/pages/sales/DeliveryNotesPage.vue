<template>
  <sales-document-list
    :title="t('sales.deliveries.title')"
    :add-label="auth.can(SalesPermissions.deliveriesWrite) ? t('sales.deliveries.add') : undefined"
    :columns="columns"
    :status-options="statusOptions"
    :fetch="fetch"
    :badges="
      (row: DeliveryNoteSummaryResponse) => ({
        status: row.status,
        reverses: row.reversesDeliveryNoteId,
        reversedBy: row.reversedByDeliveryNoteId,
      })
    "
    @open="(id: string) => router.push({ name: 'delivery-note', params: { id } })"
  />
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { listDeliveryNotes } from '@/api/generated/erp-api';
import { SalesDocumentState, type DeliveryNoteSummaryResponse } from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { useNumberFormat, useWarehouses } from '../warehouse/shared';
import SalesDocumentList from './SalesDocumentList.vue';
import type { ListQuery } from './shared';

const { t } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const format = useNumberFormat();
const { warehouses } = useWarehouses();

const statusOptions = computed(() => [
  { label: t('common.all'), value: null },
  ...Object.values(SalesDocumentState).map((value) => ({
    label: t(`sales.statuses.${value}`),
    value,
  })),
]);

const columns = computed<QTableColumn<DeliveryNoteSummaryResponse>[]>(() => [
  { name: 'number', label: t('sales.number'), field: (row) => row.number ?? '—', align: 'left' },
  {
    name: 'date',
    label: t('sales.date'),
    field: (row) => format.date(row.deliveryDate),
    align: 'left',
  },
  { name: 'order', label: t('sales.order'), field: (row) => row.orderNumber ?? '—', align: 'left' },
  { name: 'customer', label: t('sales.customer'), field: 'customerName', align: 'left' },
  {
    name: 'warehouse',
    label: t('sales.warehouse'),
    field: (row) => warehouses.value.find((w) => w.id === row.warehouseId)?.code ?? '',
    align: 'left',
  },
  {
    name: 'cost',
    label: t('sales.deliveries.cost'),
    field: (row) => format.amount(row.costValue),
    align: 'right',
  },
  { name: 'status', label: t('sales.status'), field: 'status', align: 'left' },
]);

const fetch = (companyId: string, { status, ...query }: ListQuery) =>
  listDeliveryNotes(companyId, {
    ...query,
    ...(status ? { status: status as SalesDocumentState } : {}),
  });
</script>
