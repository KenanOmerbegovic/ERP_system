<template>
  <q-layout view="hHh Lpr lFf">
    <q-header elevated>
      <q-toolbar>
        <q-btn
          flat
          dense
          round
          icon="menu"
          :aria-label="t('layout.toggleNavigation')"
          @click="drawerOpen = !drawerOpen"
        />

        <q-toolbar-title>
          {{ t('app.name') }}
          <span v-if="auth.company" class="text-subtitle2 q-ml-sm gt-xs">{{
            auth.company.name
          }}</span>
        </q-toolbar-title>

        <LanguageSwitch />
        <UserMenu />
      </q-toolbar>
    </q-header>

    <q-drawer v-model="drawerOpen" show-if-above bordered :width="260">
      <ModuleNavigation />
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';

import LanguageSwitch from '@/components/LanguageSwitch.vue';
import ModuleNavigation from '@/components/ModuleNavigation.vue';
import UserMenu from '@/components/UserMenu.vue';
import { useAuthStore } from '@/stores/auth';

const { t } = useI18n();
const auth = useAuthStore();

// Opens by itself on wide screens (show-if-above); on phones it overlays the page.
const drawerOpen = ref(false);
</script>
