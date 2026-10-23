import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { AuthResponse, AuthTokens } from '../models';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const api = axios.create({ baseURL: API_BASE_URL });

/**
 * Bridge between the Axios layer and AuthContext:
 *  - get(): current tokens (attached to every request)
 *  - set(): store rotated tokens after a successful refresh
 *  - onSessionExpired(): clear auth state when the refresh token is dead
 */
interface TokenAccessor {
  get: () => AuthTokens | null;
  set: (tokens: AuthTokens) => void;
  onSessionExpired: () => void;
}

let tokenAccessor: TokenAccessor | null = null;

export function configureApi(accessor: TokenAccessor): void {
  tokenAccessor = accessor;
}

// Attach the access token to every request.
api.interceptors.request.use((config) => {
  const tokens = tokenAccessor?.get();
  if (tokens?.accessToken) {
    config.headers.Authorization = `Bearer ${tokens.accessToken}`;
  }
  return config;
});

/**
 * Single-flight refresh: if several requests receive 401 at the same time,
 * only ONE refresh call is made; the other requests wait for it and are then
 * retried with the new access token.
 */
let refreshPromise: Promise<boolean> | null = null;

async function performRefresh(): Promise<boolean> {
  const tokens = tokenAccessor?.get();
  if (!tokens?.refreshToken) return false;
  try {
    // Use raw axios (not the `api` instance) so this call is not intercepted.
    const response = await axios.post<AuthResponse>(`${API_BASE_URL}/auth/refresh`, {
      refreshToken: tokens.refreshToken
    });
    tokenAccessor?.set({
      accessToken: response.data.accessToken,
      refreshToken: response.data.refreshToken
    });
    return true;
  } catch {
    return false;
  }
}

type RetriableConfig = InternalAxiosRequestConfig & { _retry?: boolean };

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetriableConfig | undefined;
    const status = error.response?.status;
    const isAuthCall =
      original?.url?.includes('/auth/refresh') || original?.url?.includes('/auth/login');

    // Retry at most once, only for 401s that are not the auth calls themselves.
    if (!original || status !== 401 || original._retry || isAuthCall) {
      return Promise.reject(error);
    }
    original._retry = true;

    if (!refreshPromise) {
      refreshPromise = performRefresh().finally(() => {
        refreshPromise = null;
      });
    }

    const refreshed = await refreshPromise;
    if (!refreshed) {
      tokenAccessor?.onSessionExpired();
      return Promise.reject(error);
    }

    return api.request(original);
  }
);
