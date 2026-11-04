import { isAxiosError, type AxiosInstance } from 'axios';

declare module 'axios' {
  interface InternalAxiosRequestConfig {
    /** Set on the one retry after a refresh, so a second 401 is final. */
    retriedAfterRefresh?: boolean;
  }
}

export interface SessionHooks {
  accessToken(): string | null;
  /** Gets a new access token; false when the session is over. */
  refresh(): Promise<boolean>;
  onSessionLost(): void;
}

/** Login, refresh and logout answer 401 for their own reasons; refreshing would not help. */
const isAuthCall = (url: string | undefined) => url?.startsWith('/api/auth/') ?? false;

/**
 * Sends the in-memory access token with every request. On a 401 it refreshes the session
 * and retries the request once; if refreshing fails, the session is over.
 */
export function installAuthInterceptor(http: AxiosInstance, session: SessionHooks): void {
  http.interceptors.request.use((config) => {
    const token = session.accessToken();
    if (token && !isAuthCall(config.url)) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  });

  http.interceptors.response.use(undefined, async (error: unknown) => {
    const config = isAxiosError(error) ? error.config : undefined;
    if (
      !isAxiosError(error) ||
      error.response?.status !== 401 ||
      !config ||
      config.retriedAfterRefresh ||
      isAuthCall(config.url)
    ) {
      throw error;
    }

    if (!(await session.refresh())) {
      session.onSessionLost();
      throw error;
    }

    // The request interceptor runs again and attaches the new token.
    config.retriedAfterRefresh = true;
    return http.request(config);
  });
}
