<template>
  <q-page padding>
    <div class="text-h5">{{ t('home.title', { name: auth.user?.displayName ?? '' }) }}</div>
    <div v-if="auth.company" class="text-body1 text-grey-7 q-mt-sm">{{ t('home.subtitle') }}</div>
    <q-banner v-else rounded class="bg-orange-1 q-mt-md">{{ t('layout.noCompany') }}</q-banner>
  </q-page>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';

import { useAuthStore } from '@/stores/auth';

const { t } = useI18n();
const auth = useAuthStore();
</script>
