import { useMutation, useQueryClient } from '@tanstack/react-query';
import { accountApi, type AddAddressPayload } from '../services/accountApi';

export function useAddAddress() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: AddAddressPayload) => accountApi.addAddress(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['profile-me'] }),
  });
}
