/**
 * Runs sandboxed with contextIsolation: the renderer gets only what is exposed here, never
 * Node.js or ipcRenderer itself. Keep this surface minimal.
 */
import { contextBridge, ipcRenderer } from 'electron';
import { quasarRuntime } from '#q-app/electron/preload';

import { DesktopChannels, type DesktopApi } from './desktop-api';

const desktop: DesktopApi = {
  refreshToken: {
    get: () => ipcRenderer.invoke(DesktopChannels.getRefreshToken) as Promise<string | null>,
    set: (token: string) =>
      ipcRenderer.invoke(DesktopChannels.setRefreshToken, token) as Promise<void>,
    clear: () => ipcRenderer.invoke(DesktopChannels.clearRefreshToken) as Promise<void>,
  },
};

contextBridge.exposeInMainWorld('erpDesktop', desktop);

/** Used by Quasar itself through `window.quasarRuntime`. */
contextBridge.exposeInMainWorld('quasarRuntime', quasarRuntime);
