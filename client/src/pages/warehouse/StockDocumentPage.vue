<template>
  <q-page padding>
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-btn
        flat
        round
        icon="arrow_back"
        :aria-label="t('warehouse.documents.title')"
        :to="{ name: 'stock-documents' }"
      />
      <div class="text-h6">
        {{ title }}
      </div>
      <q-badge v-if="document" :color="isDraft ? 'grey-6' : 'positive'">
        {{ t(`warehouse.statuses.${document.status}`) }}
      </q-badge>
      <q-space />
      <template v-if="isDraft && canWrite">
        <q-btn
          v-if="document"
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
        v-if="document && isDraft && canConfirm"
        color="primary"
        icon="check"
        :label="t('warehouse.document.confirm')"
        :loading="busy"
        @click="confirm"
      />
      <q-btn
        v-if="
          document &&
          !isDraft &&
          !document.reversesDocumentId &&
          !document.reversedByDocumentId &&
          canConfirm
        "
        outline
        color="negative"
        icon="undo"
        :label="t('warehouse.document.storno')"
        :loading="busy"
        @click="storno"
      />
    </div>

    <q-banner v-if="loadFailed" rounded class="bg-red-1 text-negative" role="alert">
      {{ t('warehouse.document.notFound') }}
    </q-banner>

    <template v-else-if="form">
      <q-banner v-if="isDraft" dense rounded class="bg-blue-1 q-mb-md">{{
        t('warehouse.document.draft')
      }}</q-banner>
      <q-banner v-if="document?.reversesDocumentId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('warehouse.document.stornoOf') }}
        <router-link :to="{ name: 'stock-document', params: { id: document.reversesDocumentId } }"
          >↗</router-link
        >
      </q-banner>
      <q-banner v-if="document?.reversedByDocumentId" dense rounded class="bg-orange-1 q-mb-md">
        {{ t('warehouse.document.reversedBy') }}
        <router-link :to="{ name: 'stock-document', params: { id: document.reversedByDocumentId } }"
          >↗</router-link
        >
      </q-banner>

      <div class="row q-col-gutter-md">
        <q-select
          v-model="form.type"
          class="col-12 col-sm-6 col-md-3"
          :options="typeOptions"
          :label="t('warehouse.documents.type')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-input
          v-model="form.documentDate"
          class="col-12 col-sm-6 col-md-3"
          type="date"
          :label="t('warehouse.document.date')"
          :readonly="!editable"
          stack-label
          outlined
        />
        <q-select
          v-if="needsSource(form.type)"
          v-model="form.sourceWarehouseId"
          class="col-12 col-sm-6 col-md-3"
          :options="warehouseOptions"
          :label="t('warehouse.document.source')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-select
          v-if="needsTarget(form.type)"
          v-model="form.targetWarehouseId"
          class="col-12 col-sm-6 col-md-3"
          :options="warehouseOptions"
          :label="t('warehouse.document.target')"
          :readonly="!editable"
          emit-value
          map-options
          outlined
        />
        <q-input
          v-model="form.note"
          class="col-12"
          :label="t('warehouse.document.note')"
          :readonly="!editable"
          maxlength="500"
          outlined
          autogrow
        />
      </div>

      <div class="text-subtitle1 q-mt-lg q-mb-sm">{{ t('warehouse.document.lines') }}</div>
      <q-card v-for="(line, index) in form.lines" :key="index" flat bordered class="q-mb-sm">
        <q-card-section class="row q-col-gutter-sm items-start">
          <q-select
            :model-value="line.articleId"
            class="col-12 col-md-4"
            :options="articleOptions"
            :label="t('warehouse.document.article')"
            :hint="editable ? t('warehouse.document.articleHint') : undefined"
            :display-value="line.articleLabel"
            :readonly="!editable"
            use-input
            input-debounce="300"
            emit-value
            map-options
            outlined
            dense
            @filter="filterArticles"
            @update:model-value="(id: string) => chooseArticle(line, id)"
          />
          <q-input
            v-model.number="line.quantity"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="any"
            :label="t('warehouse.document.quantity')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            v-if="hasUnitCost(form.type)"
            v-model.number="line.unitCost"
            class="col-6 col-md-2"
            type="number"
            min="0"
            step="any"
            :label="t('warehouse.document.unitCost')"
            :readonly="!editable"
            outlined
            dense
          />
          <q-input
            v-if="document && !isDraft"
            class="col-6 col-md-2"
            :model-value="format.amount(document.lines[index]?.value)"
            :label="t('warehouse.document.value')"
            readonly
            outlined
            dense
          />
          <template v-if="line.tracking === ArticleTracking.Lot">
            <q-input
              v-model="line.lotNumber"
              class="col-6 col-md-2"
              :label="t('warehouse.document.lot')"
              :readonly="!editable"
              maxlength="50"
              outlined
              dense
            />
            <q-input
              v-if="hasUnitCost(form.type)"
              v-model="line.lotExpiryDate"
              class="col-6 col-md-2"
              type="date"
              :label="t('warehouse.document.expiry')"
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
            :label="t('warehouse.document.serials')"
            :hint="editable ? t('warehouse.document.serialsHint') : undefined"
            :readonly="!editable"
            outlined
            dense
          />
          <div v-if="editable" class="col-auto">
            <q-btn
              flat
              round
              dense
              icon="close"
              :aria-label="t('warehouse.document.removeLine')"
              @click="form.lines.splice(index, 1)"
            />
          </div>
          <div
            v-for="problem in editable && showProblems ? lineProblems(line, form.type) : []"
            :key="problem"
            class="col-12 text-negative text-caption"
          >
            {{ t(`warehouse.problems.${problem}`) }}
          </div>
        </q-card-section>
      </q-card>
      <q-btn
        v-if="editable"
        flat
        color="primary"
        icon="add"
        :label="t('warehouse.document.addLine')"
        @click="form.lines.push(emptyLine())"
      />
      <div v-if="document && !isDraft" class="text-right text-subtitle1 q-mt-md">
        {{ t('warehouse.document.total') }}: {{ format.amount(document.totalValue) }}
      </div>

      <q-banner v-if="shortages" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div class="text-weight-bold">{{ t('warehouse.shortages.title') }}</div>
        <div v-for="(shortage, index) in shortages" :key="index">
          {{ describeShortage(shortage) }}
        </div>
      </q-banner>
      <q-banner v-if="message" rounded class="bg-red-1 text-negative q-mt-md" role="alert">
        <div>{{ message }}</div>
        <div v-for="problem in showProblems ? documentProblems(form) : []" :key="problem">
          {{ t(`warehouse.problems.${problem}`) }}
        </div>
      </q-banner>
    </template>
  </q-page>
</template>

<script setup lang="ts">
import { useQuasar } from 'quasar';
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import {
  confirmStockDocument,
  createStockDocument,
  deleteStockDocument,
  getStockDocument,
  listArticles,
  stornoStockDocument,
  updateStockDocument,
} from '@/api/generated/erp-api';
import {
  ArticleKind,
  ArticleTracking,
  StockDocumentKind,
  StockDocumentState,
  type ArticleResponse,
  type StockDocumentResponse,
  type StockShortageResponse,
} from '@/api/generated/model';
import { WarehousePermissions } from '@/router/permissions';
import { useAuthStore } from '@/stores/auth';

import { todayIso, useNumberFormat, useWarehouses } from './shared';
import {
  documentProblems,
  emptyDocument,
  emptyLine,
  fromResponse,
  hasUnitCost,
  lineProblems,
  needsSource,
  needsTarget,
  problemCode,
  shortagesOf,
  toRequest,
  type DocumentForm,
  type LineForm,
} from './stock-document-form';

const props = defineProps<{ id: string }>();

const { t } = useI18n();
const $q = useQuasar();
const router = useRouter();
const auth = useAuthStore();
const format = useNumberFormat();
const { warehouses } = useWarehouses();

const document = ref<StockDocumentResponse | null>(null);
const form = ref<DocumentForm | null>(null);
const loadFailed = ref(false);
const busy = ref(false);
const message = ref<string | null>(null);
const shortages = ref<StockShortageResponse[] | null>(null);
const showProblems = ref(false);

const canWrite = computed(() => auth.can(WarehousePermissions.stockDocumentsWrite));
const canConfirm = computed(() => auth.can(WarehousePermissions.stockDocumentsConfirm));
const isDraft = computed(
  () => !document.value || document.value.status === StockDocumentState.Draft,
);
const editable = computed(() => isDraft.value && canWrite.value);

const title = computed(() =>
  document.value
    ? `${t(`warehouse.types.${document.value.type}`)} ${document.value.number ?? ''}`
    : t('warehouse.document.newTitle'),
);

const typeOptions = computed(() =>
  Object.values(StockDocumentKind).map((kind) => ({
    label: t(`warehouse.types.${kind}`),
    value: kind,
  })),
);
const warehouseOptions = computed(() =>
  warehouses.value
    .filter(
      (w) =>
        w.isActive ||
        w.id === form.value?.sourceWarehouseId ||
        w.id === form.value?.targetWarehouseId,
    )
    .map((w) => ({ label: `${w.code} – ${w.name}`, value: w.id })),
);

const articles = ref<ArticleResponse[]>([]);
const articleOptions = computed(() =>
  articles.value.map((a) => ({ label: `${a.code} – ${a.name}`, value: a.id })),
);

function filterArticles(text: string, update: (callback: () => void) => void) {
  const companyId = auth.companyId;
  if (!companyId) {
    update(() => undefined);
    return;
  }

  void listArticles(companyId, { page: 1, pageSize: 20, ...(text ? { search: text } : {}) })
    .then((result) =>
      update(() => {
        articles.value = result.items.filter((a) => a.type === ArticleKind.Goods && a.isActive);
      }),
    )
    .catch(() => update(() => (articles.value = [])));
}

function chooseArticle(line: LineForm, id: string) {
  const article = articles.value.find((a) => a.id === id);
  if (!article) {
    return;
  }

  line.articleId = article.id;
  line.articleLabel = `${article.code} – ${article.name}`;
  line.tracking = article.tracking;
}

const articleCodes = computed(
  () => new Map(document.value?.lines.map((line) => [line.articleId, line.articleCode]) ?? []),
);

function describeShortage(shortage: StockShortageResponse) {
  const values = {
    article: shortage.articleCode || articleCodes.value.get(shortage.articleId) || '',
    lot: shortage.lotNumber ?? '',
    serial: shortage.serialNumber ?? '',
    requested: format.quantity(shortage.requested),
    available: format.quantity(shortage.available),
  };
  if (shortage.serialNumber) {
    return t('warehouse.shortages.serial', values);
  }

  return shortage.lotNumber
    ? t('warehouse.shortages.lot', values)
    : t('warehouse.shortages.line', values);
}

function failureMessage(error: unknown, fallback: string) {
  switch (problemCode(error)) {
    case 'NumberSeries.NotConfigured':
      return t('warehouse.document.noNumberSeries');
    case 'StockDocument.NotDraft':
      return t('warehouse.document.notDraft');
    case 'SerialNumber.AlreadyInStock':
      return t('warehouse.document.serialInStock');
    case 'Lot.ExpiryMismatch':
      return t('warehouse.document.lotExpiryMismatch');
    default:
      return fallback;
  }
}

async function load() {
  const companyId = auth.companyId;
  message.value = null;
  shortages.value = null;
  showProblems.value = false;
  if (props.id === 'new') {
    document.value = null;
    form.value = emptyDocument(todayIso());
    return;
  }

  if (!companyId) {
    return;
  }

  loadFailed.value = false;
  try {
    document.value = await getStockDocument(companyId, props.id);
    form.value = fromResponse(document.value);
  } catch {
    loadFailed.value = true;
  }
}

/** Saves the draft; returns its id, or null when it was not saved. */
async function saveDraft(): Promise<string | null> {
  const companyId = auth.companyId;
  if (!companyId || !form.value) {
    return null;
  }

  message.value = null;
  shortages.value = null;
  if (documentProblems(form.value).length > 0) {
    showProblems.value = true;
    message.value = t('warehouse.document.saveFailed');
    return null;
  }

  try {
    const request = toRequest(form.value);
    if (document.value) {
      await updateStockDocument(companyId, document.value.id, request);
      return document.value.id;
    }

    const created = await createStockDocument(companyId, request);
    return created.id;
  } catch (error) {
    message.value = failureMessage(error, t('warehouse.document.saveFailed'));
    return null;
  }
}

async function save() {
  busy.value = true;
  try {
    const id = await saveDraft();
    if (id) {
      $q.notify({ type: 'positive', message: t('warehouse.document.saved') });
      await openDocument(id);
    }
  } finally {
    busy.value = false;
  }
}

async function openDocument(id: string) {
  if (id === props.id) {
    await load();
  } else {
    await router.replace({ name: 'stock-document', params: { id } });
  }
}

function ask(question: string, action: () => Promise<void>) {
  $q.dialog({ message: question, cancel: true, persistent: true }).onOk(() => void action());
}

function confirm() {
  ask(t('warehouse.document.confirmQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId) {
      return;
    }

    busy.value = true;
    try {
      const id = canWrite.value ? await saveDraft() : (document.value?.id ?? null);
      if (!id) {
        return;
      }

      await confirmStockDocument(companyId, id);
      $q.notify({ type: 'positive', message: t('warehouse.document.confirmed') });
      await openDocument(id);
    } catch (error) {
      shortages.value = shortagesOf(error);
      message.value = shortages.value
        ? null
        : failureMessage(error, t('warehouse.document.confirmFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function storno() {
  ask(t('warehouse.document.stornoQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !document.value) {
      return;
    }

    busy.value = true;
    message.value = null;
    try {
      const created = await stornoStockDocument(companyId, document.value.id);
      $q.notify({ type: 'positive', message: t('warehouse.document.stornoDone') });
      await openDocument(created.id);
    } catch (error) {
      shortages.value = shortagesOf(error);
      message.value = shortages.value
        ? null
        : failureMessage(error, t('warehouse.document.stornoFailed'));
    } finally {
      busy.value = false;
    }
  });
}

function remove() {
  ask(t('warehouse.document.deleteQuestion'), async () => {
    const companyId = auth.companyId;
    if (!companyId || !document.value) {
      return;
    }

    busy.value = true;
    try {
      await deleteStockDocument(companyId, document.value.id);
      $q.notify({ type: 'positive', message: t('warehouse.document.deleted') });
      await router.replace({ name: 'stock-documents' });
    } catch (error) {
      message.value = failureMessage(error, t('warehouse.document.saveFailed'));
    } finally {
      busy.value = false;
    }
  });
}

onMounted(load);
watch(() => props.id, load);
</script>
