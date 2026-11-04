<template>
  <q-page class="flex flex-center q-pa-md">
    <q-card flat bordered class="login-card full-width">
      <q-card-section>
        <div class="text-h5">{{ t('auth.title') }}</div>
        <div class="text-body2 text-grey-7">{{ t('auth.subtitle') }}</div>
      </q-card-section>

      <q-form @submit="submit">
        <q-card-section class="q-gutter-md">
          <q-banner v-if="error" dense rounded class="bg-red-1 text-negative" role="alert">{{
            error
          }}</q-banner>

          <q-input
            v-model="email"
            type="email"
            autocomplete="username"
            :label="t('auth.email')"
            :rules="[required]"
            lazy-rules
            outlined
            autofocus
          />
          <q-input
            v-model="password"
            type="password"
            autocomplete="current-password"
            :label="t('auth.password')"
            :rules="[required]"
            lazy-rules
            outlined
          />
        </q-card-section>

        <q-card-actions class="q-px-md q-pb-md">
          <q-btn
            type="submit"
            color="primary"
            class="full-width"
            :label="t('auth.signIn')"
            :loading="submitting"
            unelevated
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-page>
</template>

<script setup lang="ts">
import { isAxiosError } from 'axios';
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';

import { useAuthStore } from '@/stores/auth';

const { t } = useI18n();
const auth = useAuthStore();
const route = useRoute();
const router = useRouter();

const email = ref('');
const password = ref('');
const submitting = ref(false);
const error = ref<string | null>(null);

const required = (value: string) => value.trim().length > 0 || t('validation.required');

/** Turns the API's problem code (Identity's IdentityErrors) or HTTP status into a message. */
function messageFor(failure: unknown): string {
  if (!isAxiosError(failure)) {
    return t('auth.errors.generic');
  }

  if (!failure.response) {
    return t('auth.errors.unavailable');
  }

  if (failure.response.status === 429) {
    return t('auth.errors.tooManyRequests');
  }

  const code: unknown = (failure.response.data as { code?: unknown } | undefined)?.code;
  switch (code) {
    case 'Auth.InvalidCredentials':
      return t('auth.errors.invalidCredentials');
    case 'Auth.LockedOut':
      return t('auth.errors.lockedOut');
    default:
      return t('auth.errors.generic');
  }
}

/** Only paths inside the app, so a crafted ?redirect= cannot send the user elsewhere. */
function redirectTarget(): string {
  const redirect = route.query.redirect;
  return typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')
    ? redirect
    : '/';
}

async function submit() {
  submitting.value = true;
  error.value = null;
  try {
    await auth.login(email.value.trim(), password.value);
    password.value = '';
    await router.replace(redirectTarget());
  } catch (failure) {
    error.value = messageFor(failure);
  } finally {
    submitting.value = false;
  }
}
</script>

<style scoped>
.login-card {
  max-width: 400px;
}
</style>
