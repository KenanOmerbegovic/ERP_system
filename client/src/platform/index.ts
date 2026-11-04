import { createElectronPlatform } from './electron';
import type { Platform } from './platform';
import { createWebPlatform } from './web';

export type { Platform, TokenStorage } from './platform';

/** Chosen at build time: `quasar dev` / `build` is web, `-m electron` is desktop. */
export const platform: Platform =
  import.meta.env.QUASAR_MODE === 'electron' ? createElectronPlatform() : createWebPlatform();
