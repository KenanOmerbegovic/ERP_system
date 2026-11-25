<template>
  <q-page padding>
    <q-table
      :title="t('warehouse.warehouses.title')"
      :rows="warehouses"
      :columns="columns"
      row-key="id"
      :grid="$q.screen.lt.sm"
      :rows-per-page-options="[0]"
      :no-data-label="t('warehouse.warehouses.empty')"
      flat
      bordered
      hide-pagination
    >
      <template #top-right>
        <q-btn
          v-if="canWrite"
          color="primary"
          icon="add"
          :label="t('warehouse.warehouses.add')"
          @click="openEditor(null)"
        />
      </template>
      <template #body-cell-actions="props">
        <q-td :props="props" auto-width>
          <q-btn
            v-if="canWrite"
            flat
            round
            dense
            icon="edit"
            :aria-label="t('common.edit')"
            @click="openEditor(props.row)"
          />
        </q-td>
      </template>
    </q-table>

    <q-banner v-if="failed" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
      {{ t('warehouse.warehouses.loadFailed') }}
      <template #action>
        <q-btn flat color="negative" :label="t('common.retry')" @click="reload" />
      </template>
    </q-banner>

    <q-dialog v-model="editorOpen">
      <q-card style="width: 420px; max-width: 95vw">
        <q-form @submit="save">
          <q-card-section class="text-h6">
            {{ editing ? t('warehouse.warehouses.edit') : t('warehouse.warehouses.add') }}
          </q-card-section>
          <q-card-section class="q-gutter-md">
            <q-input
              v-model="form.code"
              :label="t('warehouse.warehouses.code')"
              :rules="[required]"
              maxlength="50"
              outlined
              autofocus
            />
            <q-input
              v-model="form.name"
              :label="t('warehouse.warehouses.name')"
              :rules="[required]"
              maxlength="200"
              outlined
            />
            <q-toggle v-model="form.isActive" :label="t('warehouse.warehouses.active')" />
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
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';

import { createWarehouse, updateWarehouse } from '@/api/generated/erp-api';
import type { WarehouseResponse } from '@/api/generated/model';
import { WarehousePermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { useWarehouses } from './shared';
import { problemCode } from './stock-document-form';

const { t } = useI18n();
const auth = useAuthStore();
const { warehouses, failed, reload } = useWarehouses();

const canWrite = computed(() => auth.can(WarehousePermissions.warehousesWrite));

const columns = computed<QTableColumn<WarehouseResponse>[]>(() => [
  { name: 'code', label: t('warehouse.warehouses.code'), field: 'code', align: 'left' },
  { name: 'name', label: t('warehouse.warehouses.name'), field: 'name', align: 'left' },
  {
    name: 'status',
    label: t('warehouse.warehouses.status'),
    field: (row) =>
      row.isActive ? t('warehouse.warehouses.active') : t('warehouse.warehouses.inactive'),
    align: 'left',
  },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]);

const editorOpen = ref(false);
const editing = ref<WarehouseResponse | null>(null);
const form = ref({ code: '', name: '', isActive: true });
const saving = ref(false);
const saveError = ref<string | null>(null);

const required = (value: string) => value.trim() !== '' || t('validation.required');

function openEditor(warehouse: WarehouseResponse | null) {
  editing.value = warehouse;
  form.value = warehouse
    ? { code: warehouse.code, name: warehouse.name, isActive: warehouse.isActive }
    : { code: '', name: '', isActive: true };
  saveError.value = null;
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
    if (editing.value) {
      await updateWarehouse(companyId, editing.value.id, form.value);
    } else {
      await createWarehouse(companyId, form.value);
    }

    editorOpen.value = false;
    await reload();
  } catch (error) {
    saveError.value =
      problemCode(error) === 'Warehouse.DuplicateCode'
        ? t('warehouse.warehouses.duplicateCode')
        : t('warehouse.warehouses.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>
