import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Pagination } from '@/shared/types/api';
import type { CategoryNode, ProductDetail, ProductSummary } from '../types';

export interface SearchParams {
  page: number;
  pageSize: number;
  keyword?: string;
  categoryId?: string;
  sort?: string;
}

export interface PagedProducts {
  items: ProductSummary[];
  pagination: Pagination;
}

export const productsApi = {
  search: async (params: SearchParams): Promise<PagedProducts> => {
    const res = await apiClient.get<ApiResponse<ProductSummary[]>>('/products', { params });
    return {
      items: res.data.data ?? [],
      pagination: res.data.pagination ?? { page: 1, pageSize: params.pageSize, totalCount: 0, totalPages: 0 },
    };
  },
  getBySlug: async (slug: string): Promise<ProductDetail> => {
    const res = await apiClient.get<ApiResponse<ProductDetail>>(`/products/by-slug/${encodeURIComponent(slug)}`);
    if (!res.data.success || !res.data.data) throw new Error(res.data.message || 'Product not found');
    return res.data.data;
  },
  categories: async (): Promise<CategoryNode[]> => {
    const res = await apiClient.get<ApiResponse<CategoryNode[]>>('/categories', { params: { tree: true } });
    return res.data.data ?? [];
  },
};
