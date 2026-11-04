<template>
  <q-btn v-if="auth.user" flat round dense icon="account_circle" :aria-label="t('layout.userMenu')">
    <q-menu>
      <q-list style="min-width: 240px">
        <q-item>
          <q-item-section>
            <q-item-label>{{ auth.user.displayName }}</q-item-label>
            <q-item-label caption>{{ auth.user.email }}</q-item-label>
          </q-item-section>
        </q-item>

        <template v-if="auth.user.companies.length > 1">
          <q-separator />
          <q-item-label header>{{ t('layout.company') }}</q-item-label>
          <q-item
            v-for="membership in auth.user.companies"
            :key="membership.companyId"
            v-close-popup
            clickable
            :active="membership.companyId === auth.companyId"
            @click="auth.selectCompany(membership.companyId)"
          >
            <q-item-section>{{ membership.name }}</q-item-section>
          </q-item>
        </template>

        <q-separator />
        <q-item v-close-popup clickable @click="signOut">
          <q-item-section avatar><q-icon name="logout" /></q-item-section>
          <q-item-section>{{ t('auth.signOut') }}</q-item-section>
        </q-item>
      </q-list>
    </q-menu>
  </q-btn>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';

import { useAuthStore } from '@/stores/auth';

const { t } = useI18n();
const auth = useAuthStore();
const router = useRouter();

async function signOut() {
  await auth.logout();
  await router.replace({ name: 'login' });
}
</script>
