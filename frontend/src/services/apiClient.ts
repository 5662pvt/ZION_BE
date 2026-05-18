import axios, { AxiosError, AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios';
import { CART_TOKEN_HEADER } from '@/shared/constants';
import type { ApiResponse } from '@/shared/types/api';
import { tokenStorage } from './tokenStorage';

export const apiClient = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
});

let refreshInFlight: Promise<string | null> | null = null;
let onUnauthorized: (() => void) | null = null;

export function setOnUnauthorized(handler: () => void) {
  onUnauthorized = handler;
}

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const access = tokenStorage.getAccess();
  if (access) {
    config.headers.set('Authorization', `Bearer ${access}`);
  }
  const cartToken = tokenStorage.getCartToken();
  if (cartToken && !access) {
    config.headers.set(CART_TOKEN_HEADER, cartToken);
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    const headerToken = response.headers[CART_TOKEN_HEADER.toLowerCase()];
    if (typeof headerToken === 'string' && headerToken.length > 0) {
      tokenStorage.setCartToken(headerToken);
    }
    return response;
  },
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;
    if (error.response?.status === 401 && original && !original._retried && original.url !== '/auth/refresh') {
      original._retried = true;
      try {
        const newAccess = await refreshAccessToken();
        if (newAccess) {
          original.headers = { ...(original.headers ?? {}), Authorization: `Bearer ${newAccess}` };
          return apiClient.request(original);
        }
      } catch {
        // fall through
      }
      tokenStorage.clear();
      onUnauthorized?.();
    }
    return Promise.reject(error);
  },
);

async function refreshAccessToken(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;
  const refresh = tokenStorage.getRefresh();
  if (!refresh) return null;

  refreshInFlight = (async () => {
    try {
      const { data } = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
        '/api/v1/auth/refresh',
        { refreshToken: refresh },
      );
      if (data.success && data.data) {
        tokenStorage.set(data.data.accessToken, data.data.refreshToken);
        return data.data.accessToken;
      }
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

export function unwrap<T>(response: { data: ApiResponse<T> }): T {
  if (!response.data.success || response.data.data === null) {
    throw new Error(response.data.message || 'Request failed');
  }
  return response.data.data;
}
