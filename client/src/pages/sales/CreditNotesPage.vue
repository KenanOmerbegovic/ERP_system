<template>
  <sales-document-list
    :title="t('sales.creditNotes.title')"
    :add-label="
      auth.can(SalesPermissions.creditNotesWrite) ? t('sales.creditNotes.add') : undefined
    "
    :columns="columns"
    :status-options="statusOptions"
    :fetch="fetch"
    :badges="
      (row: CreditNoteSummaryResponse) => ({
        status: row.status,
        reverses: row.reversesCreditNoteId,
        reversedBy: row.reversedByCreditNoteId,
      })
    "
    @open="(id: string) => router.push({ name: 'credit-note', params: { id } })"
  />
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { listCreditNotes } from '@/api/generated/erp-api';
import { SalesDocumentState, type CreditNoteSummaryResponse } from '@/api/generated/model';
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
  ...Object.values(SalesDocumentState).map((value) => ({
    label: t(`sales.statuses.${value}`),
    value,
  })),
]);

const columns = computed<QTableColumn<CreditNoteSummaryResponse>[]>(() => [
  { name: 'number', label: t('sales.number'), field: (row) => row.number ?? '—', align: 'left' },
  {
    name: 'date',
    label: t('sales.date'),
    field: (row) => format.date(row.creditNoteDate),
    align: 'left',
  },
  {
    name: 'invoice',
    label: t('sales.creditNotes.invoice'),
    field: (row) => row.invoiceNumber ?? '—',
    align: 'left',
  },
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
  listCreditNotes(companyId, {
    ...query,
    ...(status ? { status: status as SalesDocumentState } : {}),
  });
</script>
