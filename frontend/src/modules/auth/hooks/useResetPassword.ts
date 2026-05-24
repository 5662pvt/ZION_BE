import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../services/authApi';

export function useResetPassword() {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: ({ email, code, newPassword }: { email: string; code: string; newPassword: string }) =>
      authApi.resetPassword(email, code, newPassword),
    onSuccess: () => navigate('/login', { replace: true, state: { passwordReset: true } }),
  });
}
