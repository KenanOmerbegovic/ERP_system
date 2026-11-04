import { defineBoot } from '#q-app';
import { Notify } from 'quasar';

import { installAuthInterceptor } from '@/api/auth-interceptor';
import { http } from '@/api/http';
import { useAuthStore } from '@/stores/auth';

import { i18n } from './i18n';

/** Wires the session into the API client and the router: token on every call, guards on every route. */
export default defineBoot(({ router, store }) => {
  const auth = useAuthStore(store);

  installAuthInterceptor(http, {
    accessToken: () => auth.accessToken,
    refresh: () => auth.refresh(),
    onSessionLost: () => {
      Notify.create({ type: 'warning', message: i18n.global.t('auth.sessionExpired') });
      void router.replace({
        name: 'login',
        query: { redirect: router.currentRoute.value.fullPath },
      });
    },
  });

  router.beforeEach(async (to) => {
    await auth.restore();

    if (to.meta.public) {
      return to.name === 'login' && auth.isSignedIn ? { name: 'home' } : true;
    }

    if (!auth.isSignedIn) {
      return { name: 'login', query: to.fullPath === '/' ? {} : { redirect: to.fullPath } };
    }

    if (to.meta.permission && !auth.can(to.meta.permission)) {
      return { name: 'forbidden' };
    }

    return true;
  });
});
