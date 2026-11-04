import type {
  AuthResponse,
  LogoutRequest,
  RefreshRequest,
  RefreshTokenDelivery,
} from '@/api/generated/model';

/**
 * Where the refresh token lives. The access token never goes here: it stays in memory (auth store).
 * Web: the API's httpOnly cookie, which script cannot read. Desktop: OS-encrypted storage in the
 * Electron main process.
 */
export interface TokenStorage {
  /** How login and refresh ask the API to deliver the refresh token. */
  readonly delivery: RefreshTokenDelivery;

  /** Keeps the refresh token from a login or refresh response. */
  save(response: AuthResponse): Promise<void>;

  /** The body for POST /api/auth/refresh, or null when no session is stored (no call needed). */
  refreshRequest(): Promise<RefreshRequest | null>;

  /** The body for POST /api/auth/logout. */
  logoutRequest(): Promise<LogoutRequest>;

  clear(): Promise<void>;
}

/** Everything platform-specific. Only code in src/platform may know which platform it runs on. */
export interface Platform {
  readonly name: 'web' | 'electron';
  readonly tokens: TokenStorage;
}
