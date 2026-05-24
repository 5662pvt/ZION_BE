import { useMutation } from '@tanstack/react-query';
import { authApi } from '../services/authApi';

export function useResendVerification() {
  return useMutation({
    mutationFn: (email: string) => authApi.resendVerification(email),
  });
}
