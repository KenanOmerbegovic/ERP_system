<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('sales.invoices.title')"
        :to="{ name: 'sales-invoices' }"
      />
      <div class="text-h6">{{ title }}</div>
      <sales-status-badges
        v-if="invoice"
        :status="invoice.status"
        :reverses="invoice.reversesInvoiceId"
        :reversed-by="invoice.reversedByInvoiceId"
      />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="invoice"
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
        v-if="invoice && isDraft && can(SalesPermissions.invoicesConfirm)"
        color="primary"
        icon="check"
        :label="t('sales.invoices.issue')"
        :loading="busy"
        @click="confirm"
      />
      <template v-if="isOpen">
        <q-btn
          v-if="can(SalesPermissions.creditNotesWrite)"
          outline
          color="primary"
          icon="assignment_return"
          :label="t('sales.invoices.newCreditNote')"
          :to="{ name: 'credit-note', params: { id: 'new' }, query: { invoiceId: invoice!.id } }"
        />
        <q-btn
          v-if="can(SalesPermissions.invoicesConfirm) && Number(invoice!.creditedAmount) === 0"
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
      <q-banner v-if="invoice?.reversesInvoiceId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.stornoOf') }}
        <router-link :to="{ name: 'sales-invoice', params: { id: invoice.reversesInvoiceId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="invoice?.reversedByInvoiceId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.reversedBy') }}
        <router-link :to="{ name: 'sales-invoice', params: { id: invoice.reversedByInvoiceId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner
        v-if="invoice && Number(invoice.creditedAmount) > 0"
        dense
        rounded
        class="bg-blue-1 q-mb-md"
      >
        {{ t('sales.invoices.creditedInfo', { amount: format.amount(invoice.creditedAmount) }) }}
        <router-link :to="{ name: 'credit-notes' }">↗</router-link>
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-if="!invoice"
          :model-value="form.salesOrderId"
          class="col-12 col-md-4"
          :options="orderOptions"
          :label="t('sales.order')"
          :hint="t('sales.invoices.orderHint')"
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
            <router-link :to="{ name: 'sales-order', params: { id: invoice.salesOrderId } }"
              >↗</router-link
            >
          </template>
        </q-input>
        <q-input
          v-model="form.invoiceDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.invoices.invoiceDate')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-model="form.dueDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.invoices.dueDate')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-if="invoice"
          class="col-12 col-md-4"
          :model-value="
            [
              invoice.customerTaxId && `JIB ${invoice.customerTaxId}`,
              invoice.customerVatNumber && `PDV ${invoice.customerVatNumber}`,
            ]
              .filter(Boolean)
              .join(' · ')
          "
          :label="t('sales.customer') + ': ' + invoice.customerName"
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
      <q-card v-for="(line, index) in form.lines" :key="index" flat bordered class="q-mb-sm">
        <q-card-section class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-4 text-body2 q-pt-sm">
            {{ line.articleLabel }}
            <q-badge v-if="line.isService" outline color="secondary" class="q-ml-xs">{{
              t('sales.service')
            }}</q-badge>
            <div class="text-caption text-grey-7">
              <span v-if="line.sourceLabel"
                >{{ t('sales.invoices.fromDelivery', { number: line.sourceLabel }) }} ·
              </span>
              <span v-if="editable">{{
                t('sales.invoices.remaining', { quantity: format.quantity(line.remainingQuantity) })
              }}</span>
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
            class="col-6 col-md-2"
            :model-value="format.price(line.unitPrice)"
            :label="t('sales.unitPrice')"
            readonly
            outlined
            dense
          />
          <q-input
            class="col-6 col-md-1"
            :model-value="line.vatCode"
            :label="t('sales.vatCode')"
            readonly
            outlined
            dense
          />
          <q-input
            class="col-6 col-md-2"
            :model-value="format.amount(lineAmount(line.quantity, line.unitPrice))"
            :label="t('sales.net')"
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
      <q-btn
        v-if="editable && form.salesOrderId"
        flat
        color="primary"
        icon="playlist_add"
        :label="t('sales.invoices.addInvoiceable')"
        @click="addInvoiceable"
      />

      <q-markup-table
        flat
        bordered
        dense
        class="q-mt-md"
        style="max-width: 520px; margin-left: auto"
      >
        <thead>
          <tr>
            <th class="text-left">{{ t('sales.vatCode') }}</th>
            <th class="text-right">{{ t('sales.invoices.rate') }}</th>
            <th class="text-right">{{ t('sales.invoices.base') }}</th>
            <th class="text-right">{{ t('sales.vat') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in totals.rows" :key="row.vatCode">
            <td>{{ row.vatCode }}</td>
            <td class="text-right">
              {{ row.rate === null ? '—' : `${format.quantity(row.rate * 100)} %` }}
            </td>
            <td class="text-right">{{ format.amount(row.base) }}</td>
            <td class="text-right">{{ row.vat === null ? '—' : format.amount(row.vat) }}</td>
          </tr>
          <tr class="text-weight-bold">
            <td colspan="2">{{ t('sales.gross') }}</td>
            <td class="text-right">{{ format.amount(totals.net) }}</td>
            <td class="text-right">{{ totals.vat === null ? '—' : format.amount(totals.vat) }}</td>
          </tr>
          <tr class="text-weight-bold">
            <td colspan="3">{{ t('sales.invoices.toPay') }}</td>
            <td class="text-right">
              {{ totals.gross === null ? '—' : format.amount(totals.gross) }}
            </td>
          </tr>
        </tbody>
      </q-markup-table>
      <div v-if="editable" class="text-caption text-grey-7 text-right q-mt-xs">
        {{ t('sales.invoices.pdvAfterSave') }}
      </div>

      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? invoiceProblems(form) : []" :key="problem">
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
  confirmSalesInvoice,
  createSalesInvoice,
  deleteSalesInvoice,
  getSalesInvoice,
  getSalesOrder,
  listInvoiceableLines,
  listSalesOrders,
  stornoSalesInvoice,
  updateSalesInvoice,
} from '@/api/generated/erp-api';
import {
  SalesDocumentState,
  type SalesInvoiceResponse,
  type SalesOrderSummaryResponse,
} from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat } from '../warehouse/shared';
import {
  invoiceFromResponse,
  invoiceLinesFrom,
  invoiceProblems,
  lineAmount,
  toInvoiceRequest,
  vatSummary,
  type InvoiceForm,
} from './sales-form';
import SalesStatusBadges from './SalesStatusBadges.vue';
import { useSalesFeedback, useVatRates } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const vatRates = useVatRates();
const { ask, notify, failureMessage } = useSalesFeedback();
const can = (permission: string) => auth.can(permission);

const invoice = ref<SalesInvoiceResponse | null>(null);
const form = ref<InvoiceForm | null>(null);
const openOrders = ref<SalesOrderSummaryResponse[]>([]);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const showProblems = ref(false);

const isDraft = computed(() => !invoice.value || invoice.value.status === SalesDocumentState.Draft);
const isOpen = computed(
  () =>
    invoice.value?.status === SalesDocumentState.Confirmed &&
    !invoice.value.reversesInvoiceId &&
    !invoice.value.reversedByInvoiceId,
);
const editable = computed(() => isDraft.value && can(SalesPermissions.invoicesWrite));
const title = computed(() =>
  invoice.value?.number
    ? `${t('sales.kinds.SalesInvoice')} ${invoice.value.number}`
    : t('sales.invoices.newTitle'),
);
const orderOptions = computed(() =>
  openOrders.value.map((o) => ({ label: `${o.number ?? ''} – ${o.customerName}`, value: o.id })),
);

/** The saved invoice's PDV once it is not a draft; a preview by the server's rule while it is. */
const totals = computed(() => {
  if (invoice.value && !isDraft.value) {
    return {
      rows: invoice.value.vatTotals.map((row) => ({
        vatCode: row.vatCode,
        rate: Number(row.rate),
        base: Number(row.base),
        vat: Number(row.vatAmount),
      })),
      net: Number(invoice.value.netAmount),
      vat: Number(invoice.value.vatAmount),
      gross: Number(invoice.value.grossAmount),
    };
  }

  return vatSummary(
    form.value?.lines ?? [],
    vatRates.rates.value,
    form.value?.invoiceDate ?? todayIso(),
  );
});

async function chooseOrder(orderId: string) {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  const order = await getSalesOrder(companyId, orderId);
  form.value = {
    salesOrderId: order.id,
    orderLabel: `${order.number ?? ''} – ${order.customerName}`,
    invoiceDate: todayIso(),
    dueDate: todayIso(),
    note: '',
    lines: invoiceLinesFrom(await listInvoiceableLines(companyId, { salesOrderId: order.id })),
  };
}

/** Adds what is left to invoice and is not on the draft yet. */
async function addInvoiceable() {
  const companyId = auth.companyId;
  if (!companyId || !form.value?.salesOrderId) {
    return;
  }

  const lines = invoiceLinesFrom(
    await listInvoiceableLines(companyId, { salesOrderId: form.value.salesOrderId }),
  );
  const present = new Set(form.value.lines.map((l) => l.deliveryLineId ?? l.orderLineId));
  form.value.lines.push(...lines.filter((l) => !present.has(l.deliveryLineId ?? l.orderLineId)));
}

async function load() {
  message.value = null;
  showProblems.value = false;
  void vatRates.load();
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  loadFailed.value = false;
  if (props.id === 'new') {
    invoice.value = null;
    form.value = {
      salesOrderId: null,
      orderLabel: '',
      invoiceDate: todayIso(),
      dueDate: todayIso(),
      note: '',
      lines: [],
    };
    const result = await listSalesOrders(companyId, { openOnly: true, page: 1, pageSize: 100 });
    openOrders.value = result.items.filter((o) => !o.fullyInvoiced);
    if (typeof route.query.orderId === 'string') {
      await chooseOrder(route.query.orderId);
    }

    return;
  }

  try {
    invoice.value = await getSalesInvoice(companyId, props.id);
    form.value = invoiceFromResponse(invoice.value);
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
  if (invoiceProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('sales.saveFailed');
    return null;
  }

  try {
    const request = toInvoiceRequest(form.value);
    if (invoice.value) {
      await updateSalesInvoice(companyId, invoice.value.id, request);
      return invoice.value.id;
    }

    return (await createSalesInvoice(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('sales.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'sales-invoice', params: { id } });
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
  ask(t('sales.invoices.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !invoice.value) {
      return;
    }

    busy.value = true;
    try {
      const id = editable.value ? await saveDraft() : invoice.value.id;
      if (!id) {
        return;
      }

      await confirmSalesInvoice(companyId, id);
      notify(t('sales.invoices.issued'));
      await openDocument(id);
    } catch (error) {
      message.value = failureMessage(error, t('sales.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('sales.invoices.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !invoice.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    try {
      const created = await stornoSalesInvoice(companyId, invoice.value.id);
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
  ask(t('sales.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !invoice.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteSalesInvoice(companyId, invoice.value.id);
      notify(t('sales.deleted'));
      await router.replace({ name: 'sales-invoices' });
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
