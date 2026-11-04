import { RefreshTokenDelivery } from '@/api/generated/model';

import type { Platform } from './platform';

/**
 * The browser never sees the refresh token: the API sets it as an httpOnly, SameSite=Strict
 * cookie on /api/auth, and the browser sends it back on refresh and logout.
 */
export function createWebPlatform(): Platform {
  return {
    name: 'web',
    tokens: {
      delivery: RefreshTokenDelivery.Cookie,
      save: () => Promise.resolve(),
      // Whether a cookie exists is unknown to script; the API answers 401 when there is none.
      refreshRequest: () => Promise.resolve({ tokenDelivery: RefreshTokenDelivery.Cookie }),
      logoutRequest: () => Promise.resolve({}),
      clear: () => Promise.resolve(),
    },
  };
}
