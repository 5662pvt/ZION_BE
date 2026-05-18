import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useLocation } from 'react-router-dom';
import { authApi } from '../services/authApi';
import { useAuth } from '@/hooks/useAuth';

export function useLogin() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) => authApi.login(email, password),
    onSuccess: (token) => {
      login({
        user: { id: token.userId, email: token.email, roles: token.roles },
        accessToken: token.accessToken,
        refreshToken: token.refreshToken,
      });
      qc.invalidateQueries({ queryKey: ['cart'] });
      const from = (location.state as { from?: string } | null)?.from ?? '/products';
      navigate(from, { replace: true });
    },
  });
}
