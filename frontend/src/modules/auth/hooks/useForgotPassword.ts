import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../services/authApi';

export function useForgotPassword() {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (email: string) => authApi.forgotPassword(email),
    onSuccess: (_data, email) => {
      navigate(`/reset-password?email=${encodeURIComponent(email)}`, { replace: true });
    },
  });
}
