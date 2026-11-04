/**
 * The only API the preload script exposes to the renderer (window.erpDesktop).
 * Shared by main, preload and renderer; types and channel names only.
 */
export interface DesktopApi {
  readonly refreshToken: {
    get(): Promise<string | null>;
    set(token: string): Promise<void>;
    clear(): Promise<void>;
  };
}

export const DesktopChannels = {
  getRefreshToken: 'erp:refresh-token:get',
  setRefreshToken: 'erp:refresh-token:set',
  clearRefreshToken: 'erp:refresh-token:clear',
} as const;
