<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('purchasing.orders.title')"
        :to="{ name: 'purchase-orders' }"
      />
      <div class="text-h6">{{ title }}</div>
      <document-status-badges
        v-if="order"
        :row="order"
        :reverses="order.reversesOrderId"
        :reversed-by="order.reversedByOrderId"
      />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="order"
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
        v-if="order && isDraft && can(PurchasingPermissions.ordersConfirm)"
        color="primary"
        icon="check"
        :label="t('purchasing.confirm')"
        :loading="busy"
        @click="confirm"
      />
      <template v-if="order && isOpen">
        <q-btn
          v-if="can(PurchasingPermissions.receiptsWrite)"
          outline
          color="primary"
          icon="move_to_inbox"
          :label="t('purchasing.orders.newReceipt')"
          :to="{ name: 'goods-receipt', params: { id: 'new' }, query: { orderId: order.id } }"
        />
        <q-btn
          v-if="can(PurchasingPermissions.invoicesWrite)"
          outline
          color="primary"
          icon="request_quote"
          :label="t('purchasing.orders.newInvoice')"
          :to="{ name: 'supplier-invoice', params: { id: 'new' }, query: { orderId: order.id } }"
        />
        <q-btn
          v-if="can(PurchasingPermissions.ordersConfirm)"
          outline
          color="negative"
          icon="undo"
          :label="t('purchasing.doStorno')"
          :loading="busy"
          @click="storno"
        />
      </template>
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">{{
      t('purchasing.notFound')
    }}</q-banner>

    <template v-else-if="form">
      <q-banner v-if="order?.reversesOrderId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.stornoOf') }}
        <router-link :to="{ name: 'purchase-order', params: { id: order.reversesOrderId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="order?.reversedByOrderId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.reversedBy') }}
        <router-link :to="{ name: 'purchase-order', params: { id: order.reversedByOrderId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          :model-value="form.supplierId"
          class="col-12 col-md-4"
          :options="
            supplierPicker.suppliers.value.map((p) => ({
              label: supplierPicker.label(p),
              value: p.id,
            }))
          "
          :label="t('purchasing.supplier')"
          :hint="editable ? t('purchasing.supplierHint') : undefined"
          :display-value="form.supplierLabel"
          :readonly="!editable"
          use-input
          input-debounce="300"
          emit-value
          map-options
          outlined
          @filter="supplierPicker.filter"
          @update:model-value="chooseSupplier"
        />
        <q-input
          v-model="form.orderDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('purchasing.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-model="form.expectedDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('purchasing.orders.expectedDate')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-select
          v-model="form.warehouseId"
          class="col-12 col-md-4"
          :options="warehouses.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))"
          :label="t('purchasing.warehouse')"
          :readonly="!editable"
          emit-value
          map-options
          clearable
          outlined
        />
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
          <q-select
            :model-value="line.articleId"
            class="col-12 col-md-4"
            :options="
              articlePicker.articles.value.map((a) => ({
                label: articlePicker.label(a),
                value: a.id,
              }))
            "
            :label="t('purchasing.article')"
            :hint="editable ? t('purchasing.articleHint') : undefined"
            :display-value="line.articleLabel"
            :readonly="!editable"
            use-input
            input-debounce="300"
            emit-value
            map-options
            outlined
            dense
            @filter="articlePicker.filter"
            @update:model-value="(id: string) => chooseArticle(line, id)"
          />
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
            v-model.number="line.unitPrice"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="any"
            :label="t('purchasing.unitPrice')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-select
            v-model="line.vatCode"
            class="col-6 col-md-2"
            :options="vatRates.codes()"
            :label="t('purchasing.vatCode')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            class="col-6 col-md-1"
            :model-value="format.amount(lineAmount(line.quantity, line.unitPrice))"
            :label="t('purchasing.net')"
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
          <div
            v-if="order && !isDraft && order.lines[index]"
            class="col-12 text-caption text-grey-8"
          >
            {{ t('purchasing.orders.received') }}:
            {{ format.quantity(order.lines[index].receivedQuantity) }} ·
            {{ t('purchasing.orders.invoiced') }}:
            {{ format.quantity(order.lines[index].invoicedQuantity) }} ·
            {{ t('purchasing.orders.open') }}:
            {{ format.quantity(order.lines[index].openQuantity) }}
          </div>
        </q-card-section>
      </q-card>
      <q-btn
        v-if="editable"
        flat
        color="primary"
        icon="add"
        :label="t('purchasing.addLine')"
        @click="form.lines.push(emptyOrderLine())"
      />
      <div class="text-right text-subtitle1 q-mt-md">
        {{ t('purchasing.net') }}:
        {{
          format.amount(form.lines.reduce((sum, l) => sum + lineAmount(l.quantity, l.unitPrice), 0))
        }}
      </div>

      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? orderProblems(form) : []" :key="problem">
          {{ t(`purchasing.problems.${problem}`) }}
        </div>
      </q-banner>
    </template>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import {
  confirmPurchaseOrder,
  createPurchaseOrder,
  deletePurchaseOrder,
  getPurchaseOrder,
  stornoPurchaseOrder,
  updatePurchaseOrder,
} from '@/api/generated/erp-api';
import { PurchaseDocumentState, type PurchaseOrderResponse } from '@/api/generated/model';
import { PurchasingPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from '../warehouse/shared';
import DocumentStatusBadges from './DocumentStatusBadges.vue';
import {
  emptyOrder,
  emptyOrderLine,
  lineAmount,
  orderFromResponse,
  orderProblems,
  toOrderRequest,
  type OrderForm,
  type OrderLineForm,
} from './purchasing-form';
import { useArticlePicker, useDocumentFeedback, useSupplierPicker, useVatRates } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();
const supplierPicker = useSupplierPicker();
const articlePicker = useArticlePicker();
const vatRates = useVatRates();
const { ask, notify, failureMessage } = useDocumentFeedback();
const can = (permission: string) => auth.can(permission);

const order = ref<PurchaseOrderResponse | null>(null);
const form = ref<OrderForm | null>(null);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const showProblems = ref(false);

const isDraft = computed(() => !order.value || order.value.status === PurchaseDocumentState.Draft);
const isOpen = computed(
  () =>
    order.value?.status === PurchaseDocumentState.Confirmed &&
    !order.value.reversesOrderId &&
    !order.value.reversedByOrderId,
);
const editable = computed(() => isDraft.value && can(PurchasingPermissions.ordersWrite));
const title = computed(() =>
  order.value?.number
    ? `${t('purchasing.kinds.PurchaseOrder')} ${order.value.number}`
    : t('purchasing.orders.newTitle'),
);

function chooseSupplier(id: string) {
  const supplier = supplierPicker.suppliers.value.find((p) => p.id === id);
  if (form.value && supplier) {
    form.value.supplierId = supplier.id;
    form.value.supplierLabel = supplierPicker.label(supplier);
  }
}

function chooseArticle(line: OrderLineForm, id: string) {
  const article = articlePicker.articles.value.find((a) => a.id === id);
  if (article) {
    line.articleId = article.id;
    line.articleLabel = articlePicker.label(article);
    line.vatCode = article.defaultVatCode ?? '';
  }
}

async function load() {
  message.value = null;
  showProblems.value = false;
  void vatRates.load();
  if (props.id === 'new') {
    order.value = null;
    form.value = emptyOrder(todayIso());
    return;
  }

  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  loadFailed.value = false;
  try {
    order.value = await getPurchaseOrder(companyId, props.id);
    form.value = orderFromResponse(order.value);
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
  if (orderProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('purchasing.saveFailed');
    return null;
  }

  try {
    const request = toOrderRequest(form.value);
    if (order.value) {
      await updatePurchaseOrder(companyId, order.value.id, request);
      return order.value.id;
    }

    return (await createPurchaseOrder(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('purchasing.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'purchase-order', params: { id } });
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
  ask(t('purchasing.orders.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId) {
      return;
    }

    busy.value = true;
    try {
      const id = can(PurchasingPermissions.ordersWrite)
        ? await saveDraft()
        : (order.value?.id ?? null);
      if (!id) {
        return;
      }

      await confirmPurchaseOrder(companyId, id);
      notify(t('purchasing.confirmed'));
      await openDocument(id);
    } catch (error) {
      message.value = failureMessage(error, t('purchasing.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('purchasing.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !order.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    try {
      const created = await stornoPurchaseOrder(companyId, order.value.id);
      notify(t('purchasing.stornoDone'));
      await openDocument(created.id);
    } catch (error) {
      message.value = failureMessage(error, t('purchasing.stornoFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(t('purchasing.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !order.value) {
      return;
    }

    busy.value = true;
    try {
      await deletePurchaseOrder(companyId, order.value.id);
      notify(t('purchasing.deleted'));
      await router.replace({ name: 'purchase-orders' });
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
