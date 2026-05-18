import { useMutation, useQueryClient } from '@tanstack/react-query';
import { cartApi } from '../services/cartApi';

export function useAddToCart() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, quantity }: { productId: string; quantity: number }) =>
      cartApi.addItem(productId, quantity),
    onSuccess: (cart) => qc.setQueryData(['cart'], cart),
  });
}
