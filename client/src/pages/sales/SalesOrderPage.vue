<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('sales.orders.title')"
        :to="{ name: 'sales-orders' }"
      />
      <div class="text-h6">{{ title }}</div>
      <sales-status-badges
        v-if="order"
        :status="order.status"
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
        v-if="order && isDraft && can(SalesPermissions.ordersConfirm)"
        color="primary"
        icon="check"
        :label="t('sales.confirm')"
        :loading="busy"
        @click="confirm"
      />
      <template v-if="order && isOpen">
        <q-btn
          v-if="
            can(SalesPermissions.deliveriesWrite) &&
            !order.lines.every((l) => Number(l.openQuantity) === 0)
          "
          outline
          color="primary"
          icon="local_shipping"
          :label="t('sales.orders.newDelivery')"
          :to="{ name: 'delivery-note', params: { id: 'new' }, query: { orderId: order.id } }"
        />
        <q-btn
          v-if="can(SalesPermissions.invoicesWrite)"
          outline
          color="primary"
          icon="receipt"
          :label="t('sales.orders.newInvoice')"
          :to="{ name: 'sales-invoice', params: { id: 'new' }, query: { orderId: order.id } }"
        />
        <q-btn
          v-if="can(SalesPermissions.ordersConfirm)"
          outline
          color="negative"
          icon="undo"
          :label="t('sales.doStorno')"
          :loading="busy"
          @click="storno"
        />
      </template>
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">{{
      t('sales.notFound')
    }}</q-banner>

    <template v-else-if="form">
      <q-banner v-if="order?.offerId" dense rounded class="bg-blue-1 q-mb-md">
        {{ t('sales.orders.fromOffer', { number: order.offerNumber ?? '' }) }}
        <router-link :to="{ name: 'offer', params: { id: order.offerId } }">↗</router-link>
      </q-banner>
      <q-banner v-if="order?.reversesOrderId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.stornoOf') }}
        <router-link :to="{ name: 'sales-order', params: { id: order.reversesOrderId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="order?.reversedByOrderId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.reversedBy') }}
        <router-link :to="{ name: 'sales-order', params: { id: order.reversedByOrderId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          :model-value="form.customerId"
          class="col-12 col-md-4"
          :options="
            customerPicker.customers.value.map((p) => ({
              label: customerPicker.label(p),
              value: p.id,
            }))
          "
          :label="t('sales.customer')"
          :hint="editable ? t('sales.customerHint') : undefined"
          :display-value="form.customerLabel"
          :readonly="!editable"
          use-input
          input-debounce="300"
          emit-value
          map-options
          outlined
          @filter="customerPicker.filter"
          @update:model-value="chooseCustomer"
        />
        <q-input
          v-model="form.orderDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-select
          v-model="form.warehouseId"
          class="col-6 col-md-3"
          :options="warehouses.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))"
          :label="t('sales.warehouse')"
          :hint="t('sales.orders.warehouseHint')"
          :readonly="!editable"
          emit-value
          map-options
          clearable
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

      <sales-lines-editor
        v-model="form.lines"
        :editable="editable"
        :vat-codes="vatRates.codes()"
        :progress="progress"
      />

      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? orderProblems(form) : []" :key="problem">
          {{ t(`sales.problems.${problem}`) }}
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
  confirmSalesOrder,
  createSalesOrder,
  deleteSalesOrder,
  getSalesOrder,
  stornoSalesOrder,
  updateSalesOrder,
} from '@/api/generated/erp-api';
import { ArticleKind, SalesDocumentState, type SalesOrderResponse } from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from '../warehouse/shared';
import {
  emptyOrder,
  orderFromResponse,
  orderProblems,
  toOrderRequest,
  type OrderForm,
} from './sales-form';
import SalesLinesEditor from './SalesLinesEditor.vue';
import SalesStatusBadges from './SalesStatusBadges.vue';
import { useCustomerPicker, useSalesFeedback, useVatRates } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();
const customerPicker = useCustomerPicker();
const vatRates = useVatRates();
const { ask, notify, failureMessage } = useSalesFeedback();
const can = (permission: string) => auth.can(permission);

const order = ref<SalesOrderResponse | null>(null);
const form = ref<OrderForm | null>(null);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const showProblems = ref(false);

const isDraft = computed(() => !order.value || order.value.status === SalesDocumentState.Draft);
const isOpen = computed(
  () =>
    order.value?.status === SalesDocumentState.Confirmed &&
    !order.value.reversesOrderId &&
    !order.value.reversedByOrderId,
);
const editable = computed(() => isDraft.value && can(SalesPermissions.ordersWrite));
const title = computed(() =>
  order.value?.number
    ? `${t('sales.kinds.SalesOrder')} ${order.value.number}`
    : t('sales.orders.newTitle'),
);

/** Delivered and invoiced per line of a confirmed order. */
const progress = computed(() =>
  order.value && !isDraft.value
    ? order.value.lines.map((line) =>
        line.kind === ArticleKind.Service
          ? `${t('sales.orders.invoiced')}: ${format.quantity(line.invoicedQuantity)}`
          : `${t('sales.orders.delivered')}: ${format.quantity(line.deliveredQuantity)} · ${t('sales.orders.invoiced')}: ${format.quantity(line.invoicedQuantity)} · ${t('sales.orders.open')}: ${format.quantity(line.openQuantity)}`,
      )
    : undefined,
);

function chooseCustomer(id: string) {
  const customer = customerPicker.customers.value.find((p) => p.id === id);
  if (form.value && customer) {
    form.value.customerId = customer.id;
    form.value.customerLabel = customerPicker.label(customer);
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
    order.value = await getSalesOrder(companyId, props.id);
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
    message.value = t('sales.saveFailed');
    return null;
  }

  try {
    const request = toOrderRequest(form.value);
    if (order.value) {
      await updateSalesOrder(companyId, order.value.id, request);
      return order.value.id;
    }

    return (await createSalesOrder(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('sales.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'sales-order', params: { id } });
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
  ask(t('sales.orders.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId) {
      return;
    }

    busy.value = true;
    try {
      const id = can(SalesPermissions.ordersWrite) ? await saveDraft() : (order.value?.id ?? null);
      if (!id) {
        return;
      }

      await confirmSalesOrder(companyId, id);
      notify(t('sales.confirmed'));
      await openDocument(id);
    } catch (error) {
      message.value = failureMessage(error, t('sales.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('sales.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !order.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    try {
      const created = await stornoSalesOrder(companyId, order.value.id);
      notify(t('sales.stornoDone'));
      await openDocument(created.id);
    } catch (error) {
      message.value = failureMessage(error, t('sales.stornoFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(
    order.value?.offerId ? t('sales.orders.deleteFromOfferQuestion') : t('sales.deleteQuestion'),
    async () => {
      const companyId = auth.companyId;
      if (!companyId || !order.value) {
        return;
      }

      busy.value = true;
      try {
        await deleteSalesOrder(companyId, order.value.id);
        notify(t('sales.deleted'));
        await router.replace({ name: 'sales-orders' });
      } catch (error) {
        message.value = failureMessage(error, t('sales.saveFailed'));
      } finally {
        busy.value = false;
      }
    },
  );
}

onMounted(load);
watch(() => props.id, load);
</script>
