<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('sales.offers.title')"
        :to="{ name: 'offers' }"
      />
      <div class="text-h6">{{ title }}</div>
      <sales-status-badges v-if="offer" :status="offer.status" />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="offer"
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
        v-if="isDraft && can(SalesPermissions.offersSend)"
        color="primary"
        icon="send"
        :label="t('sales.offers.send')"
        :loading="busy"
        @click="send"
      />
      <template v-if="offer?.status === OfferState.Sent && can(SalesPermissions.offersSend)">
        <q-btn
          color="primary"
          icon="handshake"
          :label="t('sales.offers.accept')"
          @click="acceptOpen = true"
        />
        <q-btn
          outline
          color="negative"
          icon="block"
          :label="t('sales.offers.reject')"
          :loading="busy"
          @click="reject"
        />
      </template>
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">{{
      t('sales.notFound')
    }}</q-banner>

    <template v-else-if="form">
      <q-banner v-if="offer?.salesOrderId" dense rounded class="bg-green-1 q-mb-md">
        {{ t('sales.offers.becameOrder') }}
        <router-link :to="{ name: 'sales-order', params: { id: offer.salesOrderId } }"
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
          v-model="form.offerDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-model="form.validUntil"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.offers.validUntil')"
          :hint="t('sales.offers.validUntilHint')"
          :readonly="!editable"
          stack-label
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

      <sales-lines-editor v-model="form.lines" :editable="editable" :vat-codes="vatRates.codes()" />

      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? offerProblems(form) : []" :key="problem">
          {{ t(`sales.problems.${problem}`) }}
        </div>
      </q-banner>
    </template>

    <q-dialog v-model="acceptOpen">
      <q-card style="width: 420px; max-width: 95vw">
        <q-card-section class="text-h6">{{ t('sales.offers.accept') }}</q-card-section>
        <q-card-section class="q-gutter-md">
          <div class="text-body2">{{ t('sales.offers.acceptHint') }}</div>
          <q-input
            v-model="accept.orderDate"
            type="date"
            :label="t('sales.orders.orderDate')"
            stack-label
            outlined
          />
          <q-select
            v-model="accept.warehouseId"
            :options="warehouses.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))"
            :label="t('sales.warehouse')"
            emit-value
            map-options
            clearable
            outlined
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat :label="t('common.cancel')" />
          <q-btn
            color="primary"
            :label="t('sales.offers.accept')"
            :loading="busy"
            @click="acceptOffer"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import {
  acceptOffer as acceptOfferRequest,
  createOffer,
  deleteOffer,
  getOffer,
  rejectOffer,
  sendOffer,
  updateOffer,
} from '@/api/generated/erp-api';
import { OfferState, type OfferResponse } from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useWarehouses } from '../warehouse/shared';
import {
  emptyOffer,
  offerFromResponse,
  offerProblems,
  toOfferRequest,
  type OfferForm,
} from './sales-form';
import SalesLinesEditor from './SalesLinesEditor.vue';
import SalesStatusBadges from './SalesStatusBadges.vue';
import { useCustomerPicker, useSalesFeedback, useVatRates } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const { warehouses } = useWarehouses();
const customerPicker = useCustomerPicker();
const vatRates = useVatRates();
const { ask, notify, failureMessage } = useSalesFeedback();
const can = (permission: string) => auth.can(permission);

const offer = ref<OfferResponse | null>(null);
const form = ref<OfferForm | null>(null);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const showProblems = ref(false);
const acceptOpen = ref(false);
const accept = ref<{ orderDate: string; warehouseId: string | null }>({
  orderDate: todayIso(),
  warehouseId: null,
});

const isDraft = computed(() => !offer.value || offer.value.status === OfferState.Draft);
const editable = computed(() => isDraft.value && can(SalesPermissions.offersWrite));
const title = computed(() =>
  offer.value?.number
    ? `${t('sales.kinds.Offer')} ${offer.value.number}`
    : t('sales.offers.newTitle'),
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
    offer.value = null;
    form.value = emptyOffer(todayIso());
    return;
  }

  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  loadFailed.value = false;
  try {
    offer.value = await getOffer(companyId, props.id);
    form.value = offerFromResponse(offer.value);
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
  if (offerProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('sales.saveFailed');
    return null;
  }

  try {
    const request = toOfferRequest(form.value);
    if (offer.value) {
      await updateOffer(companyId, offer.value.id, request);
      return offer.value.id;
    }

    return (await createOffer(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('sales.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'offer', params: { id } });
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

function send() {
  ask(t('sales.offers.sendQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId) {
      return;
    }

    busy.value = true;
    try {
      const id = can(SalesPermissions.offersWrite) ? await saveDraft() : (offer.value?.id ?? null);
      if (!id) {
        return;
      }

      await sendOffer(companyId, id);
      notify(t('sales.offers.sent'));
      await openDocument(id);
    } catch (error) {
      message.value = failureMessage(error, t('sales.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

async function acceptOffer() {
  const companyId = auth.companyId;
  if (!companyId || !offer.value) {
    return;
  }

  busy.value = true;
  message.value = null;
  try {
    const order = await acceptOfferRequest(companyId, offer.value.id, accept.value);
    acceptOpen.value = false;
    notify(t('sales.offers.accepted'));
    await router.push({ name: 'sales-order', params: { id: order.id } });
  } catch (error) {
    acceptOpen.value = false;
    message.value = failureMessage(error, t('sales.confirmFailed'));
  } finally {
    busy.value = false;
  }
}

function reject() {
  ask(t('sales.offers.rejectQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !offer.value) {
      return;
    }

    busy.value = true;
    try {
      await rejectOffer(companyId, offer.value.id);
      notify(t('sales.offers.rejected'));
      await load();
    } catch (error) {
      message.value = failureMessage(error, t('sales.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(t('sales.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !offer.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteOffer(companyId, offer.value.id);
      notify(t('sales.deleted'));
      await router.replace({ name: 'offers' });
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
