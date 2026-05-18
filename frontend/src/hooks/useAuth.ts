import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { authLoggedIn, authLoggedOut } from '@/store/authSlice';
import type { AuthUser } from '@/store/authSlice';

export function useAuth() {
  const dispatch = useAppDispatch();
  const user = useAppSelector((s) => s.auth.user);
  const isAuthenticated = user !== null;

  return {
    user,
    isAuthenticated,
    login: (payload: { user: AuthUser; accessToken: string; refreshToken: string }) =>
      dispatch(authLoggedIn(payload)),
    logout: () => dispatch(authLoggedOut()),
  };
}
