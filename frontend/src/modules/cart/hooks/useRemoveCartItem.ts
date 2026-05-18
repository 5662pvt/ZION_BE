import { useMutation, useQueryClient } from '@tanstack/react-query';
import { cartApi } from '../services/cartApi';

export function useRemoveCartItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (itemId: string) => cartApi.removeItem(itemId),
    onSuccess: (cart) => qc.setQueryData(['cart'], cart),
  });
}

export function useClearCart() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => cartApi.clear(),
    onSuccess: (cart) => qc.setQueryData(['cart'], cart),
  });
}
