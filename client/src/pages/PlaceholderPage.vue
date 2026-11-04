<template>
  <q-page padding>
    <div class="text-h5">{{ route.meta.titleKey ? t(route.meta.titleKey) : '' }}</div>
    <q-banner rounded class="bg-grey-2 q-mt-md">
      <template #avatar><q-icon name="construction" color="grey-7" /></template>
      {{ t('placeholder.description') }}
    </q-banner>
  </q-page>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { useRoute } from 'vue-router';

const { t } = useI18n();
const route = useRoute();
</script>
