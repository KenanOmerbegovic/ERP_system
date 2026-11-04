<template>
  <q-list padding>
    <q-item :to="{ name: 'home' }" exact clickable>
      <q-item-section avatar><q-icon name="home" /></q-item-section>
      <q-item-section>{{ t('layout.home') }}</q-item-section>
    </q-item>

    <template v-for="module in modules" :key="module.key">
      <q-expansion-item
        v-if="module.items.length > 0"
        :icon="module.icon"
        :label="t(`modules.${module.key}.title`)"
        default-opened
      >
        <q-item
          v-for="item in module.items"
          :key="item.route"
          :to="{ name: item.route }"
          :inset-level="0.5"
          clickable
        >
          <q-item-section avatar><q-icon :name="item.icon" /></q-item-section>
          <q-item-section>{{ t(item.labelKey) }}</q-item-section>
        </q-item>
      </q-expansion-item>

      <q-item v-else :to="{ name: `module-${module.key}` }" clickable>
        <q-item-section avatar><q-icon :name="module.icon" color="grey-6" /></q-item-section>
        <q-item-section>
          <q-item-label class="text-grey-7">{{ t(`modules.${module.key}.title`) }}</q-item-label>
          <q-item-label caption>{{ t('common.comingSoon') }}</q-item-label>
        </q-item-section>
      </q-item>
    </template>
  </q-list>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';

import { navigation } from '@/router/navigation';
import { useAuthStore } from '@/stores/auth';

const { t } = useI18n();
const auth = useAuthStore();

/**
 * Items the user may open in the current company. A built module the user has no permission
 * in disappears completely; modules not built yet stay visible as placeholders.
 */
const modules = computed(() =>
  navigation
    .map((module) => ({
      ...module,
      items: module.items.filter((item) => auth.can(item.permission)),
      built: module.items.length > 0,
    }))
    .filter((module) => !module.built || module.items.length > 0),
);
</script>
