<template>
  <sales-document-list
    :title="t('sales.invoices.title')"
    :add-label="auth.can(SalesPermissions.invoicesWrite) ? t('sales.invoices.add') : undefined"
    :columns="columns"
    :status-options="statusOptions"
    :fetch="fetch"
    :badges="
      (row: SalesInvoiceSummaryResponse) => ({
        status: row.status,
        reverses: row.reversesInvoiceId,
        reversedBy: row.reversedByInvoiceId,
      })
    "
    @open="(id: string) => router.push({ name: 'sales-invoice', params: { id } })"
  />
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { listSalesInvoices } from '@/api/generated/erp-api';
import { SalesDocumentState, type SalesInvoiceSummaryResponse } from '@/api/generated/model';
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

const columns = computed<QTableColumn<SalesInvoiceSummaryResponse>[]>(() => [
  { name: 'number', label: t('sales.number'), field: (row) => row.number ?? '—', align: 'left' },
  {
    name: 'date',
    label: t('sales.date'),
    field: (row) => format.date(row.invoiceDate),
    align: 'left',
  },
  {
    name: 'due',
    label: t('sales.invoices.dueDate'),
    field: (row) => format.date(row.dueDate),
    align: 'left',
  },
  { name: 'order', label: t('sales.order'), field: (row) => row.orderNumber ?? '—', align: 'left' },
  { name: 'customer', label: t('sales.customer'), field: 'customerName', align: 'left' },
  {
    name: 'gross',
    label: t('sales.gross'),
    field: (row) => format.amount(row.grossAmount),
    align: 'right',
  },
  { name: 'status', label: t('sales.status'), field: 'status', align: 'left' },
]);

const fetch = (companyId: string, { status, ...query }: ListQuery) =>
  listSalesInvoices(companyId, {
    ...query,
    ...(status ? { status: status as SalesDocumentState } : {}),
  });
</script>
