import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { productsApi, SearchParams } from '../services/productsApi';

export function useProducts(params: SearchParams) {
  return useQuery({
    queryKey: ['products', params],
    queryFn: () => productsApi.search(params),
    placeholderData: keepPreviousData,
  });
}

export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: () => productsApi.categories(),
    staleTime: 5 * 60_000,
  });
}
