<template>
  <q-page padding>
    <q-table
      :title="t('vatRates.title')"
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[0]"
      :no-data-label="t('vatRates.empty')"
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
          :label="t('vatRates.add')"
          @click="openEditor"
        />
      </template>
    </q-table>
    <p class="text-caption text-grey-7 q-mt-sm">{{ t('vatRates.hint') }}</p>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('vatRates.loadFailed') }}
      <template #action>
        <q-btn flat color="negative" :label="t('common.retry')" @click="load" />
      </template>
    </q-banner>

    <q-dialog v-model="editorOpen">
      <q-card style="width: 420px; max-width: 95vw">
        <q-form @submit="save">
          <q-card-section class="text-h6">{{ t('vatRates.add') }}</q-card-section>
          <q-card-section class="q-gutter-md">
            <q-input
              v-model="form.code"
              :label="t('vatRates.code')"
              :rules="[required]"
              maxlength="20"
              outlined
              autofocus
            />
            <q-input
              v-model="form.name"
              :label="t('vatRates.name')"
              :rules="[required]"
              maxlength="50"
              outlined
            />
            <q-input
              v-model.number="form.percent"
              type="number"
              min="0"
              max="100"
              step="any"
              :label="t('vatRates.rate')"
              outlined
            />
            <q-input
              v-model="form.validFrom"
              type="date"
              :label="t('vatRates.validFrom')"
              stack-label
              outlined
            />
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

import { createVatRate, listVatRates } from '@/api/generated/erp-api';
import type { VatRateResponse } from '@/api/generated/model';
import { CorePermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat } from '../warehouse/shared';
import { problemCode } from '../warehouse/stock-document-form';

const { t } = useI18n();
const auth = useAuthStore();
const format = useNumberFormat();

const rows = ref<VatRateResponse[]>([]);
const loading = ref(false);
const failed = ref(false);
const canWrite = computed(() => auth.can(CorePermissions.vatRatesWrite));
const required = (value: string) => value.trim() !== '' || t('validation.required');

// The API stores a fraction (0.17); the user types a percentage (17).
const columns = computed<QTableColumn<VatRateResponse>[]>(() => [
  { name: 'code', label: t('vatRates.code'), field: 'code', align: 'left' },
  { name: 'name', label: t('vatRates.name'), field: 'name', align: 'left' },
  {
    name: 'rate',
    label: t('vatRates.rate'),
    field: (row) => format.quantity(Number(row.rate) * 100),
    align: 'right',
  },
  {
    name: 'validFrom',
    label: t('vatRates.validFrom'),
    field: (row) => format.date(row.validFrom),
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
    rows.value = await listVatRates(companyId);
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

const editorOpen = ref(false);
const saving = ref(false);
const saveError = ref<string | null>(null);
const form = ref({ code: '', name: '', percent: 17, validFrom: todayIso() });

function openEditor() {
  saveError.value = null;
  form.value = { code: '', name: '', percent: 17, validFrom: todayIso() };
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
    await createVatRate(companyId, {
      code: form.value.code,
      name: form.value.name,
      rate: Math.round(Number(form.value.percent) * 100) / 10000,
      validFrom: form.value.validFrom,
    });
    editorOpen.value = false;
    await load();
  } catch (error) {
    saveError.value =
      problemCode(error) === 'VatRate.Duplicate'
        ? t('vatRates.duplicate')
        : t('vatRates.saveFailed');
  } finally {
    saving.value = false;
  }
}

onMounted(load);
watch(() => auth.companyId, load);
</script>
