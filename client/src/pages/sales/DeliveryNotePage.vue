<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('sales.deliveries.title')"
        :to="{ name: 'delivery-notes' }"
      />
      <div class="text-h6">{{ title }}</div>
      <sales-status-badges
        v-if="note"
        :status="note.status"
        :reverses="note.reversesDeliveryNoteId"
        :reversed-by="note.reversedByDeliveryNoteId"
      />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="note"
          flat
          color="negative"
          icon="delete"
          :label="t('common.delete')"
          @click="remove"
        />
        <q-btn
          outline
          color="primary"
          icon="save"
          :label="t('common.save')"
          :loading="busy"
          @click="save"
        />
      </template>
      <q-btn
        v-if="
          note &&
          note.status !== SalesDocumentState.Confirmed &&
          can(SalesPermissions.deliveriesConfirm)
        "
        color="primary"
        icon="check"
        :label="
          note.status === SalesDocumentState.Confirming
            ? t('sales.completeConfirmation')
            : t('sales.confirm')
        "
        :loading="busy"
        @click="confirm"
      />
      <q-btn
        v-if="canReverse && can(SalesPermissions.deliveriesConfirm)"
        outline
        color="negative"
        icon="undo"
        :label="t('sales.doStorno')"
        :loading="busy"
        @click="storno"
      />
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">{{
      t('sales.notFound')
    }}</q-banner>

    <template v-else-if="form">
      <q-banner
        v-if="note?.status === SalesDocumentState.Confirming"
        dense
        rounded
        class="bg-orange-1 q-mb-md"
      >
        {{ t('sales.deliveries.confirming') }}
      </q-banner>
      <q-banner v-if="note?.reversesDeliveryNoteId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.stornoOf') }}
        <router-link :to="{ name: 'delivery-note', params: { id: note.reversesDeliveryNoteId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="note?.reversedByDeliveryNoteId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.reversedBy') }}
        <router-link :to="{ name: 'delivery-note', params: { id: note.reversedByDeliveryNoteId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-if="!note"
          :model-value="form.salesOrderId"
          class="col-12 col-md-4"
          :options="orderOptions"
          :label="t('sales.order')"
          :hint="t('sales.deliveries.orderHint')"
          emit-value
          map-options
          outlined
          @update:model-value="chooseOrder"
        />
        <q-input
          v-else
          class="col-12 col-md-4"
          :model-value="form.orderLabel"
          :label="t('sales.order')"
          readonly
          outlined
        >
          <template #append>
            <router-link :to="{ name: 'sales-order', params: { id: note.salesOrderId } }"
              >↗</router-link
            >
          </template>
        </q-input>
        <q-select
          v-model="form.warehouseId"
          class="col-6 col-md-3"
          :options="warehouses.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))"
          :label="t('sales.warehouse')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-input
          v-model="form.deliveryDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-if="note?.stockDocumentNumber"
          class="col-6 col-md-3"
          :model-value="note.stockDocumentNumber"
          :label="t('sales.stockDocument')"
          readonly
          outlined
        />
        <q-input
          v-model="form.note"
          class="col-12"
          :label="t('sales.note')"
          :readonly="!editable"
          maxlength="500"
          outlined
          autogrow
        />
      </div>

      <div class="text-subtitle1 q-mt-lg q-mb-sm">{{ t('sales.lines') }}</div>
      <q-card
        v-for="(line, index) in form.lines"
        :key="line.orderLineId + index"
        flat
        bordered
        class="q-mb-sm"
      >
        <q-card-section class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-4 text-body2 q-pt-sm">
            {{ line.articleLabel }}
            <div v-if="editable" class="text-caption text-grey-7">
              {{ t('sales.deliveries.open', { quantity: format.quantity(line.openQuantity) }) }}
            </div>
          </div>
          <q-input
            v-model.number="line.quantity"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="any"
            :label="t('sales.quantity')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            v-if="line.tracking === ArticleTracking.Lot"
            v-model="line.lotNumber"
            class="col-6 col-md-2"
            :label="t('sales.lot')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            v-if="line.tracking === ArticleTracking.Serial"
            v-model="line.serialNumbers"
            class="col-12 col-md-4"
            type="textarea"
            :label="t('sales.serials')"
            :hint="t('sales.serialsHint')"
            :readonly="!editable"
            autogrow
            outlined
            dense
          />
          <q-input
            v-if="
              note?.lines[index]?.costValue !== null && note?.lines[index]?.costValue !== undefined
            "
            class="col-6 col-md-2"
            :model-value="format.amount(note.lines[index].costValue)"
            :label="t('sales.deliveries.cost')"
            readonly
            outlined
            dense
          />
          <div v-if="editable" class="col-auto">
            <q-btn
              flat
              round
              dense
              icon="close"
              :aria-label="t('sales.removeLine')"
              @click="form.lines.splice(index, 1)"
            />
          </div>
        </q-card-section>
      </q-card>
      <div
        v-if="note?.costValue !== null && note?.costValue !== undefined"
        class="text-right text-subtitle1 q-mt-md"
      >
        {{ t('sales.deliveries.cost') }}: {{ format.amount(note.costValue) }}
        <div class="text-caption text-grey-7">{{ t('sales.deliveries.costHint') }}</div>
      </div>

      <q-banner v-if="shortages" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div class="text-weight-bold">{{ t('sales.deliveries.shortagesTitle') }}</div>
        <div v-for="(shortage, index) in shortages" :key="index">
          {{
            t(
              shortage.serialNumber
                ? 'warehouse.shortages.serial'
                : shortage.lotNumber
                  ? 'warehouse.shortages.lot'
                  : 'warehouse.shortages.line',
              {
                article: shortage.articleCode,
                lot: shortage.lotNumber ?? '',
                serial: shortage.serialNumber ?? '',
                requested: format.quantity(shortage.requested),
                available: format.quantity(shortage.available),
              },
            )
          }}
        </div>
      </q-banner>
      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? deliveryProblems(form) : []" :key="problem">
          {{ t(`sales.problems.${problem}`) }}
        </div>
      </q-banner>
    </template>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';

import {
  confirmDeliveryNote,
  createDeliveryNote,
  deleteDeliveryNote,
  getDeliveryNote,
  getSalesOrder,
  listSalesOrders,
  stornoDeliveryNote,
  updateDeliveryNote,
} from '@/api/generated/erp-api';
import {
  ArticleTracking,
  SalesDocumentState,
  type DeliveryNoteResponse,
  type SalesOrderSummaryResponse,
  type StockShortageResponse,
} from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from '../warehouse/shared';
import { shortagesOf } from '../warehouse/stock-document-form';
import {
  deliveryFromOrder,
  deliveryFromResponse,
  deliveryProblems,
  toDeliveryRequest,
  type DeliveryForm,
} from './sales-form';
import SalesStatusBadges from './SalesStatusBadges.vue';
import { useSalesFeedback } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();
const { ask, notify, failureMessage } = useSalesFeedback();
const can = (permission: string) => auth.can(permission);

const note = ref<DeliveryNoteResponse | null>(null);
const form = ref<DeliveryForm | null>(null);
const openOrders = ref<SalesOrderSummaryResponse[]>([]);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const shortages = ref<StockShortageResponse[] | null>(null);
const showProblems = ref(false);

const editable = computed(
  () =>
    (!note.value || note.value.status === SalesDocumentState.Draft) &&
    can(SalesPermissions.deliveriesWrite),
);
const canReverse = computed(
  () =>
    note.value?.status === SalesDocumentState.Confirmed &&
    !note.value.reversesDeliveryNoteId &&
    !note.value.reversedByDeliveryNoteId,
);
const title = computed(() =>
  note.value?.number
    ? `${t('sales.kinds.DeliveryNote')} ${note.value.number}`
    : t('sales.deliveries.newTitle'),
);
const orderOptions = computed(() =>
  openOrders.value.map((o) => ({ label: `${o.number ?? ''} – ${o.customerName}`, value: o.id })),
);

async function chooseOrder(orderId: string) {
  const companyId = auth.companyId;
  if (companyId) {
    form.value = deliveryFromOrder(await getSalesOrder(companyId, orderId), todayIso());
  }
}

async function load() {
  message.value = null;
  shortages.value = null;
  showProblems.value = false;
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  loadFailed.value = false;
  if (props.id === 'new') {
    note.value = null;
    form.value = {
      salesOrderId: null,
      orderLabel: '',
      warehouseId: null,
      deliveryDate: todayIso(),
      note: '',
      lines: [],
    };
    const result = await listSalesOrders(companyId, { openOnly: true, page: 1, pageSize: 100 });
    openOrders.value = result.items.filter((o) => !o.fullyDelivered);
    if (typeof route.query.orderId === 'string') {
      await chooseOrder(route.query.orderId);
    }

    return;
  }

  try {
    note.value = await getDeliveryNote(companyId, props.id);
    const order = await getSalesOrder(companyId, note.value.salesOrderId).catch(() => null);
    form.value = deliveryFromResponse(note.value, order);
  } catch {
    loadFailed.value = true;
  }
}

async function saveDraft(): Promise<string | null> {
  const companyId = auth.companyId;
  if (!companyId || !form.value) {
    return null;
  }

  message.value = null;
  shortages.value = null;
  if (deliveryProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('sales.saveFailed');
    return null;
  }

  try {
    const request = toDeliveryRequest(form.value);
    if (note.value) {
      await updateDeliveryNote(companyId, note.value.id, request);
      return note.value.id;
    }

    return (await createDeliveryNote(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('sales.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'delivery-note', params: { id } });
  }
}

async function save() {
  busy.value = true;
  try {
    const id = await saveDraft();
    if (id) {
      notify(t('sales.saved'));
      await openDocument(id);
    }
  } finally {
    busy.value = false;
  }
}

function confirm() {
  ask(t('sales.deliveries.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !note.value) {
      return;
    }

    busy.value = true;
    try {
      const id = editable.value ? await saveDraft() : note.value.id;
      if (!id) {
        return;
      }

      await confirmDeliveryNote(companyId, id);
      notify(t('sales.confirmed'));
      await openDocument(id);
    } catch (error) {
      const found = shortagesOf(error);
      const text = found ? null : failureMessage(error, t('sales.confirmFailed'));
      // FR-WH-03: the note is a draft again (or, if the answer was lost, still confirming): show its current state.
      await load().catch(() => undefined);
      shortages.value = found;
      message.value = text;
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('sales.deliveries.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !note.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    shortages.value = null;
    try {
      const created = await stornoDeliveryNote(companyId, note.value.id);
      notify(t('sales.stornoDone'));
      await openDocument(created.id);
    } catch (error) {
      shortages.value = shortagesOf(error);
      message.value = shortages.value ? null : failureMessage(error, t('sales.stornoFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(t('sales.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !note.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteDeliveryNote(companyId, note.value.id);
      notify(t('sales.deleted'));
      await router.replace({ name: 'delivery-notes' });
    } catch (error) {
      message.value = failureMessage(error, t('sales.saveFailed'));
    } finally {
      busy.value = false;
    }
  });
}

onMounted(load);
watch(() => props.id, load);
</script>
