<template>
  <sales-document-list
    :title="t('sales.orders.title')"
    :add-label="auth.can(SalesPermissions.ordersWrite) ? t('sales.orders.add') : undefined"
    :columns="columns"
    :status-options="statusOptions"
    :fetch="fetch"
    :badges="
      (row: SalesOrderSummaryResponse) => ({
        status: row.status,
        reverses: row.reversesOrderId,
        reversedBy: row.reversedByOrderId,
      })
    "
    @open="(id: string) => router.push({ name: 'sales-order', params: { id } })"
  />
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { listSalesOrders } from '@/api/generated/erp-api';
import { SalesDocumentState, type SalesOrderSummaryResponse } from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { useNumberFormat } from '../warehouse/shared';
import SalesDocumentList from './SalesDocumentList.vue';
import type { ListQuery } from './shared';

const { t } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const format = useNumberFormat();

const statusOptions = computed(() => [
  { label: t('common.all'), value: null },
  { label: t('sales.statuses.Draft'), value: SalesDocumentState.Draft },
  { label: t('sales.statuses.Confirmed'), value: SalesDocumentState.Confirmed },
]);

const columns = computed<QTableColumn<SalesOrderSummaryResponse>[]>(() => [
  { name: 'number', label: t('sales.number'), field: (row) => row.number ?? '—', align: 'left' },
  {
    name: 'date',
    label: t('sales.date'),
    field: (row) => format.date(row.orderDate),
    align: 'left',
  },
  { name: 'customer', label: t('sales.customer'), field: 'customerName', align: 'left' },
  {
    name: 'net',
    label: t('sales.net'),
    field: (row) => format.amount(row.netAmount),
    align: 'right',
  },
  {
    name: 'progress',
    label: t('sales.orders.progress'),
    field: (row) =>
      [
        row.fullyDelivered ? t('sales.orders.fullyDelivered') : '',
        row.fullyInvoiced ? t('sales.orders.fullyInvoiced') : '',
      ]
        .filter(Boolean)
        .join(' · '),
    align: 'left',
  },
  { name: 'status', label: t('sales.status'), field: 'status', align: 'left' },
]);

const fetch = (companyId: string, { status, ...query }: ListQuery) =>
  listSalesOrders(companyId, {
    ...query,
    ...(status ? { status: status as SalesDocumentState } : {}),
  });
</script>
