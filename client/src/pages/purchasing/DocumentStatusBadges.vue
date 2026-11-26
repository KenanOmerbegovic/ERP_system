<template>
  <q-badge :color="color">{{ t(`purchasing.statuses.${row.status}`) }}</q-badge>
  <q-badge v-if="reverses" color="negative" class="q-ml-xs">{{ t('purchasing.storno') }}</q-badge>
  <q-badge v-if="reversedBy" outline color="negative" class="q-ml-xs">{{
    t('purchasing.reversed')
  }}</q-badge>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

import { PurchaseDocumentState } from '@/api/generated/model';

/** Status of a purchasing document, and whether it is a storno or was reversed. */
const props = defineProps<{
  row: { status: PurchaseDocumentState };
  reverses?: string | null;
  reversedBy?: string | null;
}>();

const { t } = useI18n();

const color = computed(() =>
  props.row.status === PurchaseDocumentState.Confirmed
    ? 'positive'
    : props.row.status === PurchaseDocumentState.Confirming
      ? 'warning'
      : 'grey-6',
);
</script>
