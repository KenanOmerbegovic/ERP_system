<template>
  <q-page padding>
    <q-table
      :title="t('purchasing.numberSeries.title')"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[0]"
      :no-data-label="t('warehouse.numberSeries.empty')"
      :loading-label="t('common.loading')"
      flat
      bordered
      hide-pagination
    >
      <template #top-right>
        <q-btn
          v-if="canWrite"
          color="primary"
          icon="add"
          :label="t('warehouse.numberSeries.add')"
          @click="openEditor"
        />
      </template>
    </q-table>
    <p class="text-caption text-grey-7 q-mt-sm">{{ t('warehouse.numberSeries.hint') }}</p>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('warehouse.numberSeries.loadFailed') }}
      <template #action>
        <q-btn flat color="negative" :label="t('common.retry')" @click="load" />
      </template>
    </q-banner>

    <q-dialog v-model="editorOpen">
      <q-card style="width: 420px; max-width: 95vw">
        <q-form @submit="save">
          <q-card-section class="text-h6">{{ t('warehouse.numberSeries.add') }}</q-card-section>
          <q-card-section class="q-gutter-md">
            <q-select
              v-model="form.documentType"
              :options="typeOptions"
              :label="t('warehouse.numberSeries.documentType')"
              emit-value
              map-options
              outlined
            />
            <q-input
              v-model="form.validFrom"
              type="date"
              :label="t('warehouse.numberSeries.validFrom')"
              stack-label
              outlined
            />
            <q-input
              v-model="form.prefix"
              :label="t('warehouse.numberSeries.prefix')"
              :rules="[(value: string) => value.trim() !== '' || t('validation.required')]"
              maxlength="20"
              outlined
            />
            <q-input
              v-model.number="form.padding"
              type="number"
              min="1"
              max="10"
              :label="t('warehouse.numberSeries.padding')"
              outlined
            />
            <div class="text-caption">
              {{ t('warehouse.numberSeries.example') }}: {{ example(form.prefix, form.padding) }}
            </div>
            <div v-if="saveError" class="text-negative" role="alert">{{ saveError }}</div>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn v-close-popup flat :label="t('common.cancel')" />
            <q-btn type="submit" color="primary" :label="t('common.save')" :loading="saving" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import type { QTableColumn } from 'quasar';
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';

import { createPurchasingNumberSeries, listPurchasingNumberSeries } from '@/api/generated/erp-api';
import { PurchasingDocumentKind, type PurchasingNumberSeriesResponse } from '@/api/generated/model';
import { PurchasingPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat } from '../warehouse/shared';
import { problemCode } from '../warehouse/stock-document-form';

const { t } = useI18n();
const auth = useAuthStore();
const format = useNumberFormat();

const rows = ref<PurchasingNumberSeriesResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const canWrite = computed(() => auth.can(PurchasingPermissions.settingsWrite));

const typeOptions = computed(() =>
  Object.values(PurchasingDocumentKind).map((kind) => ({
    label: t(`purchasing.kinds.${kind}`),
    value: kind,
  })),
);

const example = (prefix: string, padding: number | string) =>
  `${prefix}${todayIso().slice(0, 4)}-${'1'.padStart(Number(padding) || 1, '0')}`;

const columns = computed<QTableColumn<PurchasingNumberSeriesResponse>[]>(() => [
  {
    name: 'documentType',
    label: t('warehouse.numberSeries.documentType'),
    field: (row) => t(`purchasing.kinds.${row.documentType}`),
    align: 'left',
  },
  {
    name: 'validFrom',
    label: t('warehouse.numberSeries.validFrom'),
    field: (row) => format.date(row.validFrom),
    align: 'left',
  },
  { name: 'prefix', label: t('warehouse.numberSeries.prefix'), field: 'prefix', align: 'left' },
  {
    name: 'example',
    label: t('warehouse.numberSeries.example'),
    field: (row) => example(row.prefix, row.padding),
    align: 'left',
  },
]);

async function load() {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  loading.value = true;
  failed.value = false;
  try {
    rows.value = await listPurchasingNumberSeries(companyId);
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

const editorOpen = ref(false);
const saving = ref(false);
const saveError = ref<string | null>(null);
const form = ref<{
  documentType: PurchasingDocumentKind;
  validFrom: string;
  prefix: string;
  padding: number;
}>({
  documentType: PurchasingDocumentKind.PurchaseOrder,
  validFrom: `${todayIso().slice(0, 4)}-01-01`,
  prefix: '',
  padding: 5,
});

function openEditor() {
  saveError.value = null;
  form.value = { ...form.value, prefix: '' };
  editorOpen.value = true;
}

async function save() {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  saving.value = true;
  saveError.value = null;
  try {
    await createPurchasingNumberSeries(companyId, {
      ...form.value,
      padding: Number(form.value.padding),
    });
    editorOpen.value = false;
    await load();
  } catch (error) {
    saveError.value =
      problemCode(error) === 'NumberSeries.Duplicate'
        ? t('warehouse.numberSeries.duplicate')
        : t('warehouse.numberSeries.saveFailed');
  } finally {
    saving.value = false;
  }
}

onMounted(load);
watch(() => auth.companyId, load);
</script>
