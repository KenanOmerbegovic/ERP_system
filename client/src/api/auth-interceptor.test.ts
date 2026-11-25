import axios, { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { describe, expect, it } from 'vitest';

import { installAuthInterceptor, type SessionHooks } from './auth-interceptor';

/** An axios instance whose adapter answers 401 until the request carries the expected token. */
function server(validToken: string) {
  const seen: (string | undefined)[] = [];
  const http = axios.create({
    adapter: (config: InternalAxiosRequestConfig) => {
      const authorization = config.headers.get('Authorization') as string | undefined;
      seen.push(authorization);
      const response = { data: 'ok', status: 200, statusText: 'OK', headers: {}, config };
      if (authorization === `Bearer ${validToken}` || config.url?.startsWith('/api/auth/')) {
        return Promise.resolve(response);
      }

      return Promise.reject(
        new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, undefined, {
          ...response,
          status: 401,
          headers: new AxiosHeaders(),
        }),
      );
    },
  });
  return { http, seen };
}

function session(
  initial: string | null,
  refreshed: string | null,
): SessionHooks & { lost: () => boolean; refreshes: () => number } {
  let token = initial;
  let lost = false;
  let refreshes = 0;
  return {
    accessToken: () => token,
    refresh: () => {
      refreshes++;
      token = refreshed;
      return Promise.resolve(refreshed !== null);
    },
    refreshes: () => refreshes,
    onSessionLost: () => {
      lost = true;
    },
    lost: () => lost,
  };
}

describe('auth interceptor', () => {
  it('sends the access token', async () => {
    const { http, seen } = server('t1');
    installAuthInterceptor(http, session('t1', null));

    await http.get('/api/me');

    expect(seen).toEqual(['Bearer t1']);
  });

  it('refreshes once on a 401 and retries with the new token', async () => {
    const { http, seen } = server('t2');
    const hooks = session('t1', 't2');
    installAuthInterceptor(http, hooks);

    const response = await http.get('/api/me');

    expect(response.data).toBe('ok');
    expect(seen).toEqual(['Bearer t1', 'Bearer t2']);
    expect(hooks.refreshes()).toBe(1);
  });

  it('ends the session when refreshing fails', async () => {
    const { http } = server('t2');
    const hooks = session('t1', null);
    installAuthInterceptor(http, hooks);

    await expect(http.get('/api/me')).rejects.toBeInstanceOf(AxiosError);
    expect(hooks.lost()).toBe(true);
  });

  it('does not send the token to, or refresh for, the auth endpoints', async () => {
    const { http, seen } = server('t2');
    const hooks = session('t1', 't2');
    installAuthInterceptor(http, hooks);

    await http.post('/api/auth/login');

    expect(seen).toEqual([undefined]);
    expect(hooks.refreshes()).toBe(0);
  });
});
