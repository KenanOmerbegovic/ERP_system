<template>
  <q-btn flat round dense icon="translate" :aria-label="t('layout.language')">
    <q-tooltip>{{ t('layout.language') }}</q-tooltip>
    <q-menu auto-close>
      <q-list style="min-width: 160px">
        <q-item
          v-for="language in languages"
          :key="language"
          clickable
          :active="locale === language"
          @click="setLanguage(language)"
        >
          <q-item-section>{{ t(`languages.${language}`) }}</q-item-section>
        </q-item>
      </q-list>
    </q-menu>
  </q-btn>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';

import { languages, setLanguage } from '@/boot/i18n';

const { t, locale } = useI18n();
</script>
