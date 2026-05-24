import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../services/authApi';
import { useAuth } from '@/hooks/useAuth';
import { mergeGuestCartAfterAuth } from '@/modules/cart/services/mergeGuestCartAfterAuth';

export function useVerifyEmail() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ email, code }: { email: string; code: string }) => authApi.verifyEmail(email, code),
    onSuccess: async (token) => {
      login({
        user: { id: token.userId, email: token.email, roles: token.roles },
        accessToken: token.accessToken,
        refreshToken: token.refreshToken,
      });
      await mergeGuestCartAfterAuth();
      qc.invalidateQueries({ queryKey: ['cart'] });
      navigate('/products', { replace: true });
    },
  });
}
