<template>
  <div class="fullscreen bg-grey-2 text-center q-pa-md flex flex-center">
    <div>
      <div class="text-h1 text-grey-6">404</div>
      <div class="text-h6 q-mt-md">{{ t('errors.notFound') }}</div>
      <q-btn
        class="q-mt-xl"
        color="primary"
        unelevated
        to="/"
        :label="t('errors.goHome')"
        no-caps
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
</script>
