<template>
  <q-page class="flex flex-center column q-pa-md text-center">
    <q-icon name="block" size="64px" color="grey-6" />
    <div class="text-h6 q-mt-md">{{ t('errors.forbidden') }}</div>
    <q-btn
      class="q-mt-lg"
      color="primary"
      unelevated
      :to="{ name: 'home' }"
      :label="t('errors.goHome')"
    />
  </q-page>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
</script>
