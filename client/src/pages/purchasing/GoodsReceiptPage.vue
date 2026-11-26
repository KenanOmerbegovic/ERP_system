<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('purchasing.receipts.title')"
        :to="{ name: 'goods-receipts' }"
      />
      <div class="text-h6">{{ title }}</div>
      <document-status-badges
        v-if="receipt"
        :row="receipt"
        :reverses="receipt.reversesReceiptId"
        :reversed-by="receipt.reversedByReceiptId"
      />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="receipt"
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
          receipt && receipt.status !== 'Confirmed' && can(PurchasingPermissions.receiptsConfirm)
        "
        color="primary"
        icon="check"
        :label="
          receipt.status === 'Confirming'
            ? t('purchasing.receipts.completeConfirmation')
            : t('purchasing.confirm')
        "
        :loading="busy"
        @click="confirm"
      />
      <q-btn
        v-if="canReverse && can(PurchasingPermissions.receiptsConfirm)"
        outline
        color="negative"
        icon="undo"
        :label="t('purchasing.doStorno')"
        :loading="busy"
        @click="storno"
      />
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">{{
      t('purchasing.notFound')
    }}</q-banner>

    <template v-else-if="form">
      <q-banner v-if="receipt?.status === 'Confirming'" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.receipts.confirming') }}
      </q-banner>
      <q-banner v-if="receipt?.reversesReceiptId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.stornoOf') }}
        <router-link :to="{ name: 'goods-receipt', params: { id: receipt.reversesReceiptId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="receipt?.reversedByReceiptId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.reversedBy') }}
        <router-link :to="{ name: 'goods-receipt', params: { id: receipt.reversedByReceiptId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-if="!receipt"
          :model-value="form.purchaseOrderId"
          class="col-12 col-md-4"
          :options="orderOptions"
          :label="t('purchasing.receipts.order')"
          :hint="t('purchasing.receipts.orderHint')"
          :display-value="form.orderLabel"
          emit-value
          map-options
          outlined
          @update:model-value="chooseOrder"
        />
        <q-input
          v-else
          class="col-12 col-md-4"
          :model-value="form.orderLabel"
          :label="t('purchasing.receipts.order')"
          readonly
          outlined
        >
          <template #append>
            <q-btn
              flat
              dense
              icon="open_in_new"
              :to="{ name: 'purchase-order', params: { id: form.purchaseOrderId } }"
            />
          </template>
        </q-input>
        <q-select
          v-model="form.warehouseId"
          class="col-12 col-md-3"
          :options="
            warehouses
              .filter((w) => w.isActive || w.id === form?.warehouseId)
              .map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))
          "
          :label="t('purchasing.warehouse')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-input
          v-model="form.receiptDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('purchasing.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-if="receipt?.stockDocumentId"
          class="col-6 col-md-3"
          :model-value="receipt.stockDocumentNumber ?? ''"
          :label="t('purchasing.receipts.stockDocument')"
          readonly
          outlined
        >
          <template #append>
            <q-btn
              flat
              dense
              icon="open_in_new"
              :to="{ name: 'stock-document', params: { id: receipt.stockDocumentId } }"
            />
          </template>
        </q-input>
        <q-input
          v-model="form.note"
          class="col-12"
          :label="t('purchasing.note')"
          :readonly="!editable"
          maxlength="500"
          outlined
          autogrow
        />
      </div>

      <div class="text-subtitle1 q-mt-lg q-mb-sm">{{ t('purchasing.lines') }}</div>
      <q-card v-for="(line, index) in form.lines" :key="index" flat bordered class="q-mb-sm">
        <q-card-section class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-3 text-body2">
            {{ line.articleLabel }}
            <div class="text-caption text-grey-7">
              {{ t('purchasing.orders.open') }}: {{ format.quantity(line.openQuantity) }}
            </div>
          </div>
          <q-input
            v-model.number="line.quantity"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="any"
            :label="t('purchasing.quantity')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            class="col-6 col-md-2"
            :model-value="format.price(line.unitCost)"
            :label="t('purchasing.receipts.unitCost')"
            readonly
            outlined
            dense
          />
          <template v-if="line.tracking === ArticleTracking.Lot">
            <q-input
              v-model="line.lotNumber"
              class="col-6 col-md-2"
              :label="t('purchasing.receipts.lot')"
              :readonly="!editable"
              maxlength="50"
              outlined
              dense
            />
            <q-input
              v-model="line.lotExpiryDate"
              class="col-6 col-md-2"
              type="date"
              :label="t('purchasing.receipts.expiry')"
              :readonly="!editable"
              stack-label
              outlined
              dense
            />
          </template>
          <q-input
            v-if="line.tracking === ArticleTracking.Serial"
            v-model="line.serialNumbers"
            class="col-12 col-md-4"
            type="textarea"
            rows="2"
            :label="t('purchasing.receipts.serials')"
            :hint="editable ? t('purchasing.receipts.serialsHint') : undefined"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            v-if="receipt?.status === 'Confirmed'"
            class="col-6 col-md-2"
            :model-value="format.amount(receipt.lines[index]?.value)"
            :label="t('purchasing.receipts.value')"
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
              :aria-label="t('purchasing.removeLine')"
              @click="form.lines.splice(index, 1)"
            />
          </div>
        </q-card-section>
      </q-card>
      <div
        v-if="receipt?.totalValue !== null && receipt?.totalValue !== undefined"
        class="text-right text-subtitle1 q-mt-md"
      >
        {{ t('purchasing.receipts.value') }}: {{ format.amount(receipt.totalValue) }}
      </div>

      <q-banner v-if="shortages" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div class="text-weight-bold">{{ t('warehouse.shortages.title') }}</div>
        <div v-for="(shortage, index) in shortages" :key="index">
          {{
            t('warehouse.shortages.line', {
              article: shortage.articleCode,
              requested: format.quantity(shortage.requested),
              available: format.quantity(shortage.available),
            })
          }}
        </div>
      </q-banner>
      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? receiptProblems(form) : []" :key="problem">
          {{ t(`purchasing.problems.${problem}`) }}
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
  confirmGoodsReceipt,
  createGoodsReceipt,
  deleteGoodsReceipt,
  getGoodsReceipt,
  getPurchaseOrder,
  listPurchaseOrders,
  stornoGoodsReceipt,
  updateGoodsReceipt,
} from '@/api/generated/erp-api';
import {
  ArticleTracking,
  PurchaseDocumentState,
  type GoodsReceiptResponse,
  type PurchaseOrderSummaryResponse,
  type StockShortageResponse,
} from '@/api/generated/model';
import { PurchasingPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from '../warehouse/shared';
import { shortagesOf } from '../warehouse/stock-document-form';
import DocumentStatusBadges from './DocumentStatusBadges.vue';
import {
  receiptFromOrder,
  receiptFromResponse,
  receiptProblems,
  toReceiptRequest,
  type ReceiptForm,
} from './purchasing-form';
import { useDocumentFeedback } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();
const { ask, notify, failureMessage } = useDocumentFeedback();
const can = (permission: string) => auth.can(permission);

const receipt = ref<GoodsReceiptResponse | null>(null);
const form = ref<ReceiptForm | null>(null);
const openOrders = ref<PurchaseOrderSummaryResponse[]>([]);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const shortages = ref<StockShortageResponse[] | null>(null);
const showProblems = ref(false);

const editable = computed(
  () =>
    (!receipt.value || receipt.value.status === PurchaseDocumentState.Draft) &&
    can(PurchasingPermissions.receiptsWrite),
);
const canReverse = computed(
  () =>
    receipt.value?.status === PurchaseDocumentState.Confirmed &&
    !receipt.value.reversesReceiptId &&
    !receipt.value.reversedByReceiptId,
);
const title = computed(() =>
  receipt.value?.number
    ? `${t('purchasing.kinds.GoodsReceipt')} ${receipt.value.number}`
    : t('purchasing.receipts.newTitle'),
);
const orderOptions = computed(() =>
  openOrders.value.map((o) => ({ label: `${o.number ?? ''} – ${o.supplierName}`, value: o.id })),
);

async function chooseOrder(orderId: string) {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  const order = await getPurchaseOrder(companyId, orderId);
  form.value = receiptFromOrder(order, todayIso());
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
    receipt.value = null;
    form.value = {
      purchaseOrderId: null,
      orderLabel: '',
      warehouseId: null,
      receiptDate: todayIso(),
      note: '',
      lines: [],
    };
    const result = await listPurchaseOrders(companyId, { openOnly: true, page: 1, pageSize: 100 });
    openOrders.value = result.items.filter((o) => !o.fullyReceived);
    if (typeof route.query.orderId === 'string') {
      await chooseOrder(route.query.orderId);
    }

    return;
  }

  try {
    receipt.value = await getGoodsReceipt(companyId, props.id);
    const order = await getPurchaseOrder(companyId, receipt.value.purchaseOrderId).catch(
      () => null,
    );
    form.value = receiptFromResponse(receipt.value, order);
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
  if (receiptProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('purchasing.saveFailed');
    return null;
  }

  try {
    const request = toReceiptRequest(form.value);
    if (receipt.value) {
      await updateGoodsReceipt(companyId, receipt.value.id, request);
      return receipt.value.id;
    }

    return (await createGoodsReceipt(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('purchasing.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'goods-receipt', params: { id } });
  }
}

async function save() {
  busy.value = true;
  try {
    const id = await saveDraft();
    if (id) {
      notify(t('purchasing.saved'));
      await openDocument(id);
    }
  } finally {
    busy.value = false;
  }
}

function confirm() {
  ask(t('purchasing.receipts.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !receipt.value) {
      return;
    }

    busy.value = true;
    try {
      const id = editable.value ? await saveDraft() : receipt.value.id;
      if (!id) {
        return;
      }

      await confirmGoodsReceipt(companyId, id);
      notify(t('purchasing.confirmed'));
      await openDocument(id);
    } catch (error) {
      const found = shortagesOf(error);
      const text = found ? null : failureMessage(error, t('purchasing.confirmFailed'));
      // The receipt is a draft again (or, if the answer was lost, still confirming): show its current state.
      await load().catch(() => undefined);
      shortages.value = found;
      message.value = text;
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('purchasing.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !receipt.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    shortages.value = null;
    try {
      const created = await stornoGoodsReceipt(companyId, receipt.value.id);
      notify(t('purchasing.stornoDone'));
      await openDocument(created.id);
    } catch (error) {
      shortages.value = shortagesOf(error);
      message.value = shortages.value ? null : failureMessage(error, t('purchasing.stornoFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(t('purchasing.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !receipt.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteGoodsReceipt(companyId, receipt.value.id);
      notify(t('purchasing.deleted'));
      await router.replace({ name: 'goods-receipts' });
    } catch (error) {
      message.value = failureMessage(error, t('purchasing.saveFailed'));
    } finally {
      busy.value = false;
    }
  });
}

onMounted(load);
watch(() => props.id, load);
</script>
