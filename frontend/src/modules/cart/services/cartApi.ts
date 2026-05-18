import { apiClient, unwrap } from '@/services/apiClient';
import type { ApiResponse } from '@/shared/types/api';
import type { Cart } from '../types';

export const cartApi = {
  get: async (): Promise<Cart> => {
    const res = await apiClient.get<ApiResponse<Cart>>('/cart');
    return unwrap(res);
  },
  addItem: async (productId: string, quantity: number): Promise<Cart> => {
    const res = await apiClient.post<ApiResponse<Cart>>('/cart/items', { productId, quantity });
    return unwrap(res);
  },
  updateItem: async (itemId: string, quantity: number): Promise<Cart> => {
    const res = await apiClient.put<ApiResponse<Cart>>(`/cart/items/${itemId}`, { quantity });
    return unwrap(res);
  },
  removeItem: async (itemId: string): Promise<Cart> => {
    const res = await apiClient.delete<ApiResponse<Cart>>(`/cart/items/${itemId}`);
    return unwrap(res);
  },
  clear: async (): Promise<Cart> => {
    const res = await apiClient.delete<ApiResponse<Cart>>('/cart');
    return unwrap(res);
  },
};
