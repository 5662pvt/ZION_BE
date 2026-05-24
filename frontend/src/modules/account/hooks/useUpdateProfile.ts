import { useMutation, useQueryClient } from '@tanstack/react-query';
import { accountApi, type UpdateProfilePayload } from '../services/accountApi';

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdateProfilePayload) => accountApi.updateProfile(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['profile-me'] }),
  });
}
