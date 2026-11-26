<template>
  <div>
    <div class="text-subtitle1 q-mt-lg q-mb-sm">{{ t('sales.lines') }}</div>
    <q-card v-for="(line, index) in lines" :key="index" flat bordered class="q-mb-sm">
      <q-card-section class="row q-col-gutter-sm items-start">
        <q-select
          :model-value="line.articleId"
          class="col-12 col-md-4"
          :options="picker.articles.value.map((a) => ({ label: picker.label(a), value: a.id }))"
          :label="t('sales.article')"
          :hint="editable ? t('sales.articleHint') : undefined"
          :display-value="line.articleLabel"
          :readonly="!editable"
          use-input
          input-debounce="300"
          emit-value
          map-options
          outlined
          dense
          @filter="picker.filter"
          @update:model-value="(id: string) => choose(line, id)"
        >
          <template #after>
            <q-badge v-if="line.isService" outline color="secondary">{{
              t('sales.service')
            }}</q-badge>
          </template>
        </q-select>
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
          v-model.number="line.unitPrice"
          class="col-6 col-md-2"
          type="number"
          min="0"
          step="any"
          :label="t('sales.unitPrice')"
          :readonly="!editable"
          outlined
          dense
        />
        <q-select
          v-model="line.vatCode"
          class="col-6 col-md-2"
          :options="vatCodes"
          :label="t('sales.vatCode')"
          :readonly="!editable"
          outlined
          dense
        />
        <q-input
          class="col-6 col-md-1"
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
            @click="lines.splice(index, 1)"
          />
        </div>
        <div v-if="progress?.[index]" class="col-12 text-caption text-grey-8">
          {{ progress[index] }}
        </div>
      </q-card-section>
    </q-card>
    <q-btn
      v-if="editable"
      flat
      color="primary"
      icon="add"
      :label="t('sales.addLine')"
      @click="lines.push(emptySalesLine())"
    />
    <div class="text-right text-subtitle1 q-mt-md">
      {{ t('sales.net') }}:
      {{ format.amount(lines.reduce((sum, l) => sum + lineAmount(l.quantity, l.unitPrice), 0)) }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';

import { useNumberFormat } from '../warehouse/shared';
import { emptySalesLine, lineAmount, type SalesLineForm } from './sales-form';
import { useSellableArticlePicker } from './shared';

/** The goods and service lines of an offer or a sales order. */
defineProps<{
  editable: boolean;
  vatCodes: string[];
  /** Per line, e.g. delivered and invoiced quantities of a confirmed order. */
  progress?: string[] | undefined;
}>();

const lines = defineModel<SalesLineForm[]>({ required: true });

const { t } = useI18n();
const format = useNumberFormat();
const picker = useSellableArticlePicker();

function choose(line: SalesLineForm, id: string) {
  const article = picker.articles.value.find((a) => a.id === id);
  if (article) {
    line.articleId = article.id;
    line.articleLabel = picker.label(article);
    line.isService = picker.isService(article);
    line.vatCode = article.defaultVatCode ?? '';
  }
}
</script>
