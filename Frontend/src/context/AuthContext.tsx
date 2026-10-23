import { createContext, useCallback, useContext, useEffect, useMemo, useState, ReactNode } from 'react';
import { AuthResponse, AuthTokens, UserInfo } from '../models';
import { authService } from '../services/authService';
import { configureApi } from '../services/api';

const AUTH_STORAGE_KEY = 'oi_auth';

interface StoredAuth {
  user: UserInfo;
  tokens: AuthTokens;
}

interface AuthContextValue {
  user: UserInfo | null;
  tokens: AuthTokens | null;
  login: (email: string, password: string) => Promise<UserInfo>;
  register: (fullName: string, email: string, password: string) => Promise<UserInfo>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function readStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredAuth) : null;
  } catch {
    return null;
  }
}

/**
 * NOTE ON TOKEN STORAGE: tokens are kept in localStorage for simplicity in
 * this machine-test project. This is practical but NOT fully secure against
 * XSS - a successful script injection could read the tokens. Trade-offs are
 * documented in README.md ("Authentication & token storage trade-offs").
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [stored, setStored] = useState<StoredAuth | null>(readStoredAuth);

  const persist = useCallback((value: StoredAuth | null) => {
    setStored(value);
    if (value) {
      localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(value));
    } else {
      localStorage.removeItem(AUTH_STORAGE_KEY);
    }
  }, []);

  // Wire the Axios layer to the auth state (token attachment + refresh).
  useEffect(() => {
    configureApi({
      get: () => stored?.tokens ?? null,
      set: (tokens: AuthTokens) => {
        setStored((current) => {
          if (!current) return current;
          const next = { user: current.user, tokens };
          localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(next));
          return next;
        });
      },
      onSessionExpired: () => {
        setStored((current) => {
          if (current) localStorage.removeItem(AUTH_STORAGE_KEY);
          return null;
        });
      }
    });
  }, [stored]);

  const login = useCallback(
    async (email: string, password: string) => {
      const response: AuthResponse = await authService.login(email, password);
      const value: StoredAuth = {
        user: response.user,
        tokens: { accessToken: response.accessToken, refreshToken: response.refreshToken }
      };
      persist(value);
      return response.user;
    },
    [persist]
  );

  const register = useCallback(
    async (fullName: string, email: string, password: string) => {
      const response = await authService.register(fullName, email, password);
      const value: StoredAuth = {
        user: response.user,
        tokens: { accessToken: response.accessToken, refreshToken: response.refreshToken }
      };
      persist(value);
      return response.user;
    },
    [persist]
  );

  const logout = useCallback(() => persist(null), [persist]);

  const value = useMemo(
    () => ({ user: stored?.user ?? null, tokens: stored?.tokens ?? null, login, register, logout }),
    [stored, login, register, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
