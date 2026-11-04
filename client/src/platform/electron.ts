import { RefreshTokenDelivery } from '@/api/generated/model';

import type { DesktopApi } from '../../src-electron/desktop-api';
import type { Platform } from './platform';

declare global {
  interface Window {
    /** Exposed by src-electron/electron-preload.ts; undefined outside Electron. */
    readonly erpDesktop?: DesktopApi;
  }
}

function desktop(): DesktopApi {
  if (!window.erpDesktop) {
    throw new Error('The Electron preload API is missing.');
  }

  return window.erpDesktop;
}

/** The API returns the refresh token in the body; the main process stores it encrypted (safeStorage). */
export function createElectronPlatform(): Platform {
  return {
    name: 'electron',
    tokens: {
      delivery: RefreshTokenDelivery.Body,
      save: async (response) => {
        if (response.refreshToken) {
          await desktop().refreshToken.set(response.refreshToken);
        }
      },
      refreshRequest: async () => {
        const refreshToken = await desktop().refreshToken.get();
        return refreshToken ? { refreshToken, tokenDelivery: RefreshTokenDelivery.Body } : null;
      },
      logoutRequest: async () => ({ refreshToken: await desktop().refreshToken.get() }),
      clear: () => desktop().refreshToken.clear(),
    },
  };
}
