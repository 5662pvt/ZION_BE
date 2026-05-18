import { useMutation, useQueryClient } from '@tanstack/react-query';
import { cartApi } from '../services/cartApi';

export function useUpdateCartItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) =>
      cartApi.updateItem(itemId, quantity),
    onSuccess: (cart) => qc.setQueryData(['cart'], cart),
  });
}
