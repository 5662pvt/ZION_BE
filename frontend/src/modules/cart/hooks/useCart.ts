import { useQuery } from '@tanstack/react-query';
import { cartApi } from '../services/cartApi';

export function useCart() {
  return useQuery({
    queryKey: ['cart'],
    queryFn: () => cartApi.get(),
    staleTime: 10_000,
  });
}
