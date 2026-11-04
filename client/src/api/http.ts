import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';

/**
 * The one axios instance every generated call uses. In development the base URL is empty:
 * the Quasar dev server proxies /api to Erp.Api (quasar.config.ts), so the browser sees a
 * single origin and the refresh cookie stays first-party.
 */
export const http = axios.create({
  baseURL: import.meta.env.API_BASE_URL,
  // Sends the httpOnly refresh cookie when the API is on another origin (CORS with credentials).
  withCredentials: true,
});

/** orval mutator: the generated functions call this and get the response body back. */
export const apiRequest = async <T>(
  config: AxiosRequestConfig,
  options?: AxiosRequestConfig,
): Promise<T> => {
  const response = await http.request<T>({ ...config, ...options });
  return response.data;
};

export type ErrorType<Error> = AxiosError<Error>;

export type BodyType<Body> = Body;
