import { isAxiosError } from 'axios';
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

import {
  getCurrentUser,
  login as loginRequest,
  logout as logoutRequest,
  refreshSession,
} from '@/api/generated/erp-api';
import type {
  AuthResponse,
  CurrentUserCompanyResponse,
  CurrentUserResponse,
} from '@/api/generated/model';
import { platform } from '@/platform';

/** One refresh at a time across all tabs, so a tab never sends a refresh token another tab just rotated. */
const refreshLockName = 'erp-session-refresh';

/**
 * The signed-in session. The access token lives only in this store (memory); the refresh token
 * is in platform storage (src/platform). Reloading the app restores the session with a refresh.
 */
export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null);
  const user = ref<CurrentUserResponse | null>(null);
  const companyId = ref<string | null>(null);

  let restoring: Promise<void> | null = null;
  let refreshing: Promise<boolean> | null = null;

  const isSignedIn = computed(() => accessToken.value !== null && user.value !== null);
  const company = computed<CurrentUserCompanyResponse | undefined>(() =>
    user.value?.companies.find((membership) => membership.companyId === companyId.value),
  );

  /** Whether the user holds the permission in the current company. */
  function can(permission: string): boolean {
    return company.value?.permissions.includes(permission) ?? false;
  }

  async function startSession(response: AuthResponse): Promise<void> {
    await platform.tokens.save(response);
    accessToken.value = response.accessToken;
  }

  async function loadUser(): Promise<void> {
    const me = await getCurrentUser();
    user.value = me;
    if (!me.companies.some((membership) => membership.companyId === companyId.value)) {
      companyId.value = me.companies[0]?.companyId ?? null;
    }
  }

  async function login(email: string, password: string): Promise<void> {
    await startSession(
      await loginRequest({ email, password, tokenDelivery: platform.tokens.delivery }),
    );
    await loadUser();
  }

  async function refreshOnce(): Promise<boolean> {
    try {
      const request = await platform.tokens.refreshRequest();
      if (!request) {
        return false;
      }

      await startSession(await refreshSession(request));
      return true;
    } catch (error) {
      // 401: the refresh token is expired, revoked or reused. Anything else (API unreachable) keeps
      // the stored token, so the next start can still restore the session.
      if (isAxiosError(error) && error.response?.status === 401) {
        await endSession();
      } else {
        accessToken.value = null;
      }

      return false;
    }
  }

  /** New access token from the refresh token; false when the session is over. Concurrent calls share one refresh. */
  function refresh(): Promise<boolean> {
    refreshing ??= (
      typeof navigator !== 'undefined' && 'locks' in navigator
        ? navigator.locks.request(refreshLockName, refreshOnce)
        : refreshOnce()
    ).finally(() => {
      refreshing = null;
    });
    return refreshing;
  }

  /** On app start: sign back in from the stored refresh token, if there is one. Runs once. */
  function restore(): Promise<void> {
    restoring ??= (async () => {
      if (await refresh()) {
        try {
          await loadUser();
        } catch {
          await endSession();
        }
      }
    })();
    return restoring;
  }

  async function endSession(): Promise<void> {
    accessToken.value = null;
    user.value = null;
    await platform.tokens.clear();
  }

  async function logout(): Promise<void> {
    try {
      await logoutRequest(await platform.tokens.logoutRequest());
    } catch {
      // Signed out locally either way; the refresh token expires on its own.
    }

    await endSession();
  }

  function selectCompany(id: string): void {
    if (user.value?.companies.some((membership) => membership.companyId === id)) {
      companyId.value = id;
    }
  }

  return {
    accessToken,
    user,
    companyId,
    company,
    isSignedIn,
    can,
    login,
    refresh,
    restore,
    logout,
    selectCompany,
  };
});
