import { useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { authLoggedIn, authLoggedOut } from '@/store/authSlice';
import type { AuthUser } from '@/store/authSlice';
import { authApi } from '@/modules/auth/services/authApi';
import { tokenStorage } from '@/services/tokenStorage';

export function useAuth() {
  const dispatch = useAppDispatch();
  const user = useAppSelector((s) => s.auth.user);
  const isAuthenticated = user !== null;

  const logout = useCallback(() => {
    const refresh = tokenStorage.getRefresh();
    if (refresh) {
      void authApi.revoke(refresh).catch(() => undefined);
    }
    dispatch(authLoggedOut());
  }, [dispatch]);

  return {
    user,
    isAuthenticated,
    login: (payload: { user: AuthUser; accessToken: string; refreshToken: string }) =>
      dispatch(authLoggedIn(payload)),
    logout,
  };
}
