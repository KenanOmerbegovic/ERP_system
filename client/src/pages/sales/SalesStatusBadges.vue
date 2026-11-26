<template>
  <q-badge :color="color">{{ t(`sales.statuses.${status}`) }}</q-badge>
  <q-badge v-if="reverses" color="negative" class="q-ml-xs">{{ t('sales.storno') }}</q-badge>
  <q-badge v-if="reversedBy" outline color="negative" class="q-ml-xs">{{
    t('sales.reversed')
  }}</q-badge>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

import type { OfferState, SalesDocumentState } from '@/api/generated/model';

/** Status of a sales document or offer, and whether it is a storno or was reversed. */
const props = defineProps<{
  status: SalesDocumentState | OfferState;
  reverses?: string | null | undefined;
  reversedBy?: string | null | undefined;
}>();

const { t } = useI18n();

const color = computed(() => {
  switch (props.status) {
    case 'Confirmed':
    case 'Accepted':
      return 'positive';
    case 'Confirming':
    case 'Sent':
      return 'warning';
    case 'Rejected':
      return 'negative';
    default:
      return 'grey-6';
  }
});
</script>
