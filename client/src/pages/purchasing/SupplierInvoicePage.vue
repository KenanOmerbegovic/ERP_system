<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('purchasing.invoices.title')"
        :to="{ name: 'supplier-invoices' }"
      />
      <div class="text-h6">{{ title }}</div>
      <document-status-badges
        v-if="invoice"
        :row="invoice"
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
        v-if="invoice && isDraft && can(PurchasingPermissions.invoicesConfirm)"
        color="primary"
        icon="check"
        :label="t('purchasing.confirm')"
        :loading="busy"
        @click="confirm"
      />
      <q-btn
        v-if="canReverse && can(PurchasingPermissions.invoicesConfirm)"
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
      <q-banner v-if="invoice?.reversesInvoiceId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.stornoOf') }}
        <router-link :to="{ name: 'supplier-invoice', params: { id: invoice.reversesInvoiceId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="invoice?.reversedByInvoiceId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('purchasing.reversedBy') }}
        <router-link :to="{ name: 'supplier-invoice', params: { id: invoice.reversedByInvoiceId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-if="!invoice"
          :model-value="form.purchaseOrderId"
          class="col-12 col-md-4"
          :options="
            openOrders.map((o) => ({ label: `${o.number ?? ''} – ${o.supplierName}`, value: o.id }))
          "
          :label="t('purchasing.invoices.order')"
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
          :label="t('purchasing.invoices.order')"
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
        <q-input
          v-model="form.supplierInvoiceNumber"
          class="col-12 col-md-3"
          :label="t('purchasing.invoices.supplierInvoiceNumber')"
          :readonly="!editable"
          maxlength="50"
          outlined
        />
        <q-input
          v-model="form.invoiceDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('purchasing.invoices.invoiceDate')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-input
          v-model="form.dueDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('purchasing.invoices.dueDate')"
          :readonly="!editable"
          stack-label
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

      <div class="row items-center q-mt-lg q-mb-sm">
        <div class="text-subtitle1">{{ t('purchasing.lines') }}</div>
        <q-space />
        <q-btn
          v-if="editable && form.purchaseOrderId"
          flat
          color="primary"
          icon="playlist_add"
          :label="t('purchasing.invoices.addReceiptLines')"
          @click="addReceiptLines"
        />
      </div>
      <q-card v-for="(line, index) in form.lines" :key="index" flat bordered class="q-mb-sm">
        <q-card-section class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-3 text-body2">
            {{ line.articleLabel }}
            <div class="text-caption text-grey-7">
              {{ line.receiptLabel }}
              <span v-if="editable"
                >·
                {{
                  t('purchasing.invoices.remaining', {
                    quantity: format.quantity(line.remainingQuantity),
                  })
                }}</span
              >
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
            class="col-6 col-md-2"
            :model-value="format.amount(priceDifference(line))"
            :label="t('purchasing.invoices.priceDifference')"
            :input-class="priceDifference(line) !== 0 ? 'text-warning text-weight-bold' : ''"
            :hint="
              t('purchasing.invoices.receivedCost') + ': ' + format.price(line.receivedUnitCost)
            "
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
      <p v-if="totalPriceDifference !== 0" class="text-caption text-warning q-mt-sm">
        {{ t('purchasing.invoices.priceDifference') }}: {{ format.amount(totalPriceDifference) }} —
        {{ t('purchasing.invoices.priceDifferenceHint') }}
      </p>

      <q-card flat bordered class="q-mt-md">
        <q-card-section>
          <div class="text-subtitle2 q-mb-sm">{{ t('purchasing.invoices.vatSummary') }}</div>
          <q-markup-table flat dense>
            <thead>
              <tr>
                <th class="text-left">{{ t('purchasing.vatCode') }}</th>
                <th class="text-right">{{ t('purchasing.invoices.rate') }}</th>
                <th class="text-right">{{ t('purchasing.invoices.base') }}</th>
                <th class="text-right">{{ t('purchasing.vat') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in summary.rows" :key="row.vatCode">
                <td>{{ row.vatCode }}</td>
                <td class="text-right">
                  {{ row.rate === null ? '—' : `${format.quantity(row.rate * 100)} %` }}
                </td>
                <td class="text-right">{{ format.amount(row.base) }}</td>
                <td class="text-right">{{ row.vat === null ? '—' : format.amount(row.vat) }}</td>
              </tr>
            </tbody>
          </q-markup-table>
          <div class="text-right q-mt-sm">
            <div>{{ t('purchasing.net') }}: {{ format.amount(summary.net) }}</div>
            <div>
              {{ t('purchasing.vat') }}:
              {{ summary.vat === null ? '—' : format.amount(summary.vat) }}
            </div>
            <div class="text-subtitle1">
              {{ t('purchasing.gross') }}:
              {{ summary.gross === null ? '—' : format.amount(summary.gross) }}
            </div>
          </div>
          <div v-if="editable" class="text-caption text-grey-7">
            {{ t('purchasing.invoices.pdvAfterSave') }}
          </div>
        </q-card-section>
      </q-card>

      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? invoiceProblems(form) : []" :key="problem">
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
  confirmSupplierInvoice,
  createSupplierInvoice,
  deleteSupplierInvoice,
  getSupplierInvoice,
  listInvoiceableReceiptLines,
  listPurchaseOrders,
  stornoSupplierInvoice,
  updateSupplierInvoice,
} from '@/api/generated/erp-api';
import {
  PurchaseDocumentState,
  type PurchaseOrderSummaryResponse,
  type SupplierInvoiceResponse,
} from '@/api/generated/model';
import { PurchasingPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat } from '../warehouse/shared';
import DocumentStatusBadges from './DocumentStatusBadges.vue';
import {
  invoiceFromResponse,
  invoiceLinesFrom,
  invoiceProblems,
  priceDifference,
  roundKm,
  toInvoiceRequest,
  vatSummary,
  type InvoiceForm,
} from './purchasing-form';
import { useDocumentFeedback, useVatRates } from './shared';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const vatRates = useVatRates();
const { ask, notify, failureMessage } = useDocumentFeedback();
const can = (permission: string) => auth.can(permission);

const invoice = ref<SupplierInvoiceResponse | null>(null);
const form = ref<InvoiceForm | null>(null);
const openOrders = ref<PurchaseOrderSummaryResponse[]>([]);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const showProblems = ref(false);

const isDraft = computed(
  () => !invoice.value || invoice.value.status === PurchaseDocumentState.Draft,
);
const editable = computed(() => isDraft.value && can(PurchasingPermissions.invoicesWrite));
const canReverse = computed(
  () =>
    invoice.value?.status === PurchaseDocumentState.Confirmed &&
    !invoice.value.reversesInvoiceId &&
    !invoice.value.reversedByInvoiceId,
);
const title = computed(() =>
  invoice.value?.number
    ? `${t('purchasing.kinds.SupplierInvoice')} ${invoice.value.number}`
    : t('purchasing.invoices.newTitle'),
);

/** While editing, a preview by the same rule as the server; once registered, the server's own amounts. */
const summary = computed(() => {
  if (invoice.value && !editable.value) {
    return {
      rows: invoice.value.vatTotals.map((total) => ({
        vatCode: total.vatCode,
        rate: Number(total.rate),
        base: Number(total.base),
        vat: Number(total.vatAmount),
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
const totalPriceDifference = computed(() =>
  roundKm((form.value?.lines ?? []).reduce((sum, line) => sum + priceDifference(line), 0)),
);

async function chooseOrder(orderId: string) {
  const order = openOrders.value.find((o) => o.id === orderId);
  if (!form.value) {
    return;
  }

  form.value.purchaseOrderId = orderId;
  form.value.orderLabel = order ? `${order.number ?? ''} – ${order.supplierName}` : '';
  form.value.lines = [];
  await addReceiptLines();
}

/** Adds the order's receipt lines that still have quantity to invoice and are not on the invoice yet. */
async function addReceiptLines() {
  const companyId = auth.companyId;
  if (!companyId || !form.value?.purchaseOrderId) {
    return;
  }

  const lines = await listInvoiceableReceiptLines(companyId, {
    purchaseOrderId: form.value.purchaseOrderId,
  });
  const present = new Set(form.value.lines.map((line) => line.receiptLineId));
  form.value.lines.push(
    ...invoiceLinesFrom(lines.filter((line) => !present.has(line.receiptLineId))),
  );
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
    const today = todayIso();
    form.value = {
      purchaseOrderId: null,
      orderLabel: '',
      supplierInvoiceNumber: '',
      invoiceDate: today,
      dueDate: today,
      note: '',
      lines: [],
    };
    const result = await listPurchaseOrders(companyId, { openOnly: true, page: 1, pageSize: 100 });
    openOrders.value = result.items;
    if (typeof route.query.orderId === 'string') {
      await chooseOrder(route.query.orderId);
    }

    return;
  }

  try {
    invoice.value = await getSupplierInvoice(companyId, props.id);
    form.value = invoiceFromResponse(invoice.value);
    if (isDraft.value) {
      // A draft reserves nothing: what it may keep is its own quantity plus what is still uninvoiced.
      const open = await listInvoiceableReceiptLines(companyId, {
        purchaseOrderId: invoice.value.purchaseOrderId,
      });
      for (const line of form.value.lines) {
        const receiptLine = open.find((o) => o.receiptLineId === line.receiptLineId);
        if (receiptLine) {
          line.receiptLabel = receiptLine.receiptNumber;
          line.remainingQuantity =
            Number(receiptLine.receivedQuantity) - Number(receiptLine.invoicedQuantity);
        }
      }
    }
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
    message.value = t('purchasing.saveFailed');
    return null;
  }

  try {
    const request = toInvoiceRequest(form.value);
    if (invoice.value) {
      await updateSupplierInvoice(companyId, invoice.value.id, request);
      return invoice.value.id;
    }

    return (await createSupplierInvoice(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('purchasing.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'supplier-invoice', params: { id } });
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
  ask(t('purchasing.invoices.confirmQuestion'), async () => {
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

      await confirmSupplierInvoice(companyId, id);
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
    if (!companyId || !invoice.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    try {
      const created = await stornoSupplierInvoice(companyId, invoice.value.id);
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
    if (!companyId || !invoice.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteSupplierInvoice(companyId, invoice.value.id);
      notify(t('purchasing.deleted'));
      await router.replace({ name: 'supplier-invoices' });
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
