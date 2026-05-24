import { useMutation } from '@tanstack/react-query';
import { authApi } from '../services/authApi';

export function useRegister() {
  return useMutation({
    mutationFn: ({ email, password, displayName }: { email: string; password: string; displayName?: string }) =>
      authApi.register(email, password, displayName),
  });
}
