import { useQuery } from '@tanstack/react-query';
import { productsApi } from '../services/productsApi';

export function useProductBySlug(slug: string | undefined) {
  return useQuery({
    queryKey: ['product', slug],
    queryFn: () => productsApi.getBySlug(slug!),
    enabled: !!slug,
  });
}
