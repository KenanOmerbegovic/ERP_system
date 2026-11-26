<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('sales.creditNotes.title')"
        :to="{ name: 'credit-notes' }"
      />
      <div class="text-h6">{{ title }}</div>
      <sales-status-badges
        v-if="creditNote"
        :status="creditNote.status"
        :reverses="creditNote.reversesCreditNoteId"
        :reversed-by="creditNote.reversedByCreditNoteId"
      />
      <q-space />
      <template v-if="editable">
        <q-btn
          v-if="creditNote"
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
          creditNote &&
          creditNote.status !== SalesDocumentState.Confirmed &&
          can(SalesPermissions.creditNotesConfirm)
        "
        color="primary"
        icon="check"
        :label="
          creditNote.status === SalesDocumentState.Confirming
            ? t('sales.completeConfirmation')
            : t('sales.confirm')
        "
        :loading="busy"
        @click="confirm"
      />
      <q-btn
        v-if="canReverse && can(SalesPermissions.creditNotesConfirm)"
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
        v-if="creditNote?.status === SalesDocumentState.Confirming"
        dense
        rounded
        class="bg-orange-1 q-mb-md"
      >
        {{ t('sales.creditNotes.confirming') }}
      </q-banner>
      <q-banner v-if="creditNote?.reversesCreditNoteId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.stornoOf') }}
        <router-link :to="{ name: 'credit-note', params: { id: creditNote.reversesCreditNoteId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="creditNote?.reversedByCreditNoteId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('sales.reversedBy') }}
        <router-link
          :to="{ name: 'credit-note', params: { id: creditNote.reversedByCreditNoteId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-if="!creditNote"
          :model-value="form.invoiceId"
          class="col-12 col-md-4"
          :options="invoiceOptions"
          :label="t('sales.creditNotes.invoice')"
          :hint="t('sales.creditNotes.invoiceHint')"
          emit-value
          map-options
          outlined
          @update:model-value="chooseInvoice"
        />
        <q-input
          v-else
          class="col-12 col-md-4"
          :model-value="form.invoiceLabel"
          :label="t('sales.creditNotes.invoice')"
          readonly
          outlined
        >
          <template #append>
            <router-link :to="{ name: 'sales-invoice', params: { id: creditNote.invoiceId } }"
              >↗</router-link
            >
          </template>
        </q-input>
        <q-input
          v-model="form.creditNoteDate"
          class="col-6 col-md-2"
          type="date"
          :label="t('sales.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-select
          v-if="hasReturns"
          v-model="form.warehouseId"
          class="col-6 col-md-3"
          :options="warehouses.map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id }))"
          :label="t('sales.creditNotes.returnWarehouse')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-input
          v-if="creditNote?.stockDocumentNumber"
          class="col-6 col-md-3"
          :model-value="creditNote.stockDocumentNumber"
          :label="t('sales.stockDocument')"
          readonly
          outlined
        />
        <q-input
          v-model="form.note"
          class="col-12"
          :label="t('sales.creditNotes.reason')"
          :readonly="!editable"
          maxlength="500"
          outlined
          autogrow
        />
      </div>

      <div class="text-subtitle1 q-mt-lg q-mb-sm">{{ t('sales.lines') }}</div>
      <div v-if="editable" class="text-caption text-grey-7 q-mb-sm">
        {{ t('sales.creditNotes.linesHint') }}
      </div>
      <q-card
        v-for="(line, index) in form.lines"
        :key="line.invoiceLineId + index"
        flat
        bordered
        class="q-mb-sm"
      >
        <q-card-section class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-3 text-body2 q-pt-sm">
            {{ line.articleLabel }}
            <div v-if="editable" class="text-caption text-grey-7">
              {{
                t('sales.creditNotes.left', {
                  quantity: format.quantity(line.returnableQuantity),
                  amount: format.amount(line.creditableAmount),
                })
              }}
            </div>
          </div>
          <q-btn-toggle
            v-model="line.kind"
            class="col-12 col-md-3"
            :options="kindOptions(line)"
            :disable="!editable"
            no-caps
            unelevated
            toggle-color="primary"
          />
          <template v-if="line.kind === CreditNoteLineKind.Return">
            <q-input
              v-model.number="line.quantity"
              class="col-6 col-md-2"
              type="number"
              min="0"
              step="any"
              :label="t('sales.creditNotes.returnedQuantity')"
              :readonly="!editable"
              outlined
              dense
            />
            <q-input
              v-if="line.tracking === ArticleTracking.Serial"
              v-model="line.serialNumbers"
              class="col-12 col-md-3"
              type="textarea"
              :label="t('sales.serials')"
              :hint="t('sales.creditNotes.serialsHint')"
              :readonly="!editable"
              autogrow
              outlined
              dense
            />
          </template>
          <q-input
            v-else
            v-model.number="line.netAmount"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="0.01"
            :label="t('sales.creditNotes.reduction')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            class="col-6 col-md-2"
            :model-value="format.amount(creditAmount(line))"
            :label="t('sales.net')"
            readonly
            outlined
            dense
          />
        </q-card-section>
      </q-card>

      <div class="text-right text-subtitle1 q-mt-md">
        <template v-if="creditNote && creditNote.status !== SalesDocumentState.Draft">
          {{ t('sales.net') }}: {{ format.amount(creditNote.netAmount) }} · {{ t('sales.vat') }}:
          {{ format.amount(creditNote.vatAmount) }} · {{ t('sales.gross') }}:
          {{ format.amount(creditNote.grossAmount) }}
          <div
            v-if="creditNote.returnedCostValue !== null && Number(creditNote.returnedCostValue) > 0"
            class="text-caption text-grey-7"
          >
            {{
              t('sales.creditNotes.returnedCost', {
                amount: format.amount(creditNote.returnedCostValue),
              })
            }}
          </div>
        </template>
        <template v-else>
          {{ t('sales.net') }}:
          {{ format.amount(activeCreditLines(form).reduce((sum, l) => sum + creditAmount(l), 0)) }}
          <div class="text-caption text-grey-7">{{ t('sales.creditNotes.pdvHint') }}</div>
        </template>
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
        <div v-for="problem in showProblems ? creditNoteProblems(form) : []" :key="problem">
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
  confirmCreditNote,
  createCreditNote,
  deleteCreditNote,
  getCreditNote,
  getSalesInvoice,
  getSalesOrder,
  listSalesInvoices,
  stornoCreditNote,
  updateCreditNote,
} from '@/api/generated/erp-api';
import {
  ArticleTracking,
  CreditNoteLineKind,
  SalesDocumentState,
  type CreditNoteResponse,
  type SalesInvoiceSummaryResponse,
  type StockShortageResponse,
} from '@/api/generated/model';
import { SalesPermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from '../warehouse/shared';
import { shortagesOf } from '../warehouse/stock-document-form';
import {
  activeCreditLines,
  creditAmount,
  creditLinesFrom,
  creditNoteFromResponse,
  creditNoteProblems,
  toCreditNoteRequest,
  type CreditLineForm,
  type CreditNoteForm,
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

const creditNote = ref<CreditNoteResponse | null>(null);
const form = ref<CreditNoteForm | null>(null);
const openInvoices = ref<SalesInvoiceSummaryResponse[]>([]);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const shortages = ref<StockShortageResponse[] | null>(null);
const showProblems = ref(false);

const editable = computed(
  () =>
    (!creditNote.value || creditNote.value.status === SalesDocumentState.Draft) &&
    can(SalesPermissions.creditNotesWrite),
);
const canReverse = computed(
  () =>
    creditNote.value?.status === SalesDocumentState.Confirmed &&
    !creditNote.value.reversesCreditNoteId &&
    !creditNote.value.reversedByCreditNoteId,
);
const hasReturns = computed(() =>
  (form.value ? activeCreditLines(form.value) : []).some(
    (l) => l.kind === CreditNoteLineKind.Return,
  ),
);
const title = computed(() =>
  creditNote.value?.number
    ? `${t('sales.kinds.CreditNote')} ${creditNote.value.number}`
    : t('sales.creditNotes.newTitle'),
);
const invoiceOptions = computed(() =>
  openInvoices.value.map((i) => ({ label: `${i.number ?? ''} – ${i.customerName}`, value: i.id })),
);

const kindOptions = (line: CreditLineForm) => [
  ...(line.isService
    ? []
    : [{ label: t('sales.creditNotes.kinds.Return'), value: CreditNoteLineKind.Return }]),
  { label: t('sales.creditNotes.kinds.ValueReduction'), value: CreditNoteLineKind.ValueReduction },
];

/** The invoice and the tracking type of its articles (from its order), for the credit note's lines. */
async function invoiceWithTracking(companyId: string, invoiceId: string) {
  const invoice = await getSalesInvoice(companyId, invoiceId);
  const order = await getSalesOrder(companyId, invoice.salesOrderId).catch(() => null);
  const tracking = new Map(order?.lines.map((line) => [line.articleId, line.tracking]) ?? []);
  return {
    invoice,
    tracking: (articleId: string) => tracking.get(articleId) ?? ArticleTracking.None,
  };
}

async function chooseInvoice(invoiceId: string) {
  const companyId = auth.companyId;
  if (!companyId) {
    return;
  }

  const { invoice, tracking } = await invoiceWithTracking(companyId, invoiceId);
  form.value = {
    invoiceId: invoice.id,
    invoiceLabel: `${invoice.number ?? ''} – ${invoice.customerName}`,
    creditNoteDate: todayIso(),
    warehouseId: null,
    note: '',
    lines: creditLinesFrom(invoice, tracking),
  };
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
    creditNote.value = null;
    form.value = {
      invoiceId: null,
      invoiceLabel: '',
      creditNoteDate: todayIso(),
      warehouseId: null,
      note: '',
      lines: [],
    };
    openInvoices.value = (
      await listSalesInvoices(companyId, { openOnly: true, page: 1, pageSize: 100 })
    ).items;
    if (typeof route.query.invoiceId === 'string') {
      await chooseInvoice(route.query.invoiceId);
    }

    return;
  }

  try {
    creditNote.value = await getCreditNote(companyId, props.id);
    const { invoice, tracking } = await invoiceWithTracking(companyId, creditNote.value.invoiceId);
    const loaded = creditNoteFromResponse(creditNote.value, invoice);
    loaded.lines = loaded.lines.map((line, index) => ({
      ...line,
      tracking: tracking(creditNote.value!.lines[index]!.articleId),
    }));
    form.value = loaded;
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
  if (creditNoteProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('sales.saveFailed');
    return null;
  }

  try {
    const request = toCreditNoteRequest(form.value);
    if (creditNote.value) {
      await updateCreditNote(companyId, creditNote.value.id, request);
      return creditNote.value.id;
    }

    return (await createCreditNote(companyId, request)).id;
  } catch (error) {
    message.value = failureMessage(error, t('sales.saveFailed'));
    return null;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'credit-note', params: { id } });
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
  ask(t('sales.creditNotes.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !creditNote.value) {
      return;
    }

    busy.value = true;
    try {
      const id = editable.value ? await saveDraft() : creditNote.value.id;
      if (!id) {
        return;
      }

      await confirmCreditNote(companyId, id);
      notify(t('sales.confirmed'));
      await openDocument(id);
    } catch (error) {
      const found = shortagesOf(error);
      const text = found ? null : failureMessage(error, t('sales.confirmFailed'));
      await load().catch(() => undefined);
      shortages.value = found;
      message.value = text;
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('sales.creditNotes.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !creditNote.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    shortages.value = null;
    try {
      const created = await stornoCreditNote(companyId, creditNote.value.id);
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
    if (!companyId || !creditNote.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteCreditNote(companyId, creditNote.value.id);
      notify(t('sales.deleted'));
      await router.replace({ name: 'credit-notes' });
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
