import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../services/authApi';
import { useAuth } from '@/hooks/useAuth';

export function useRegister() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ email, password, displayName }: { email: string; password: string; displayName?: string }) =>
      authApi.register(email, password, displayName),
    onSuccess: (token) => {
      login({
        user: { id: token.userId, email: token.email, roles: token.roles },
        accessToken: token.accessToken,
        refreshToken: token.refreshToken,
      });
      qc.invalidateQueries({ queryKey: ['cart'] });
      navigate('/products', { replace: true });
    },
  });
}
