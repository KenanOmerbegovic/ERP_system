<template>
  <sales-document-list
    :title="t('sales.offers.title')"
    :add-label="auth.can(SalesPermissions.offersWrite) ? t('sales.offers.add') : undefined"
    :columns="columns"
    :status-options="statusOptions"
    :fetch="fetch"
    :badges="(row: OfferSummaryResponse) => ({ status: row.status })"
    @open="(id: string) => router.push({ name: 'offer', params: { id } })"
  />
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { listOffers } from '@/api/generated/erp-api';
import { OfferState, type OfferSummaryResponse } from '@/api/generated/model';
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
  ...Object.values(OfferState).map((value) => ({ label: t(`sales.statuses.${value}`), value })),
]);

const columns = computed<QTableColumn<OfferSummaryResponse>[]>(() => [
  { name: 'number', label: t('sales.number'), field: (row) => row.number ?? '—', align: 'left' },
  {
    name: 'date',
    label: t('sales.date'),
    field: (row) => format.date(row.offerDate),
    align: 'left',
  },
  {
    name: 'validUntil',
    label: t('sales.offers.validUntil'),
    field: (row) => format.date(row.validUntil),
    align: 'left',
  },
  { name: 'customer', label: t('sales.customer'), field: 'customerName', align: 'left' },
  {
    name: 'net',
    label: t('sales.net'),
    field: (row) => format.amount(row.netAmount),
    align: 'right',
  },
  { name: 'status', label: t('sales.status'), field: 'status', align: 'left' },
]);

const fetch = (companyId: string, { status, ...query }: ListQuery) =>
  listOffers(companyId, { ...query, ...(status ? { status: status as OfferState } : {}) });
</script>
