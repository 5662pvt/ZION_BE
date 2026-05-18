import { apiClient, unwrap } from '@/services/apiClient';
import type { ApiResponse } from '@/shared/types/api';

export interface AuthToken {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  userId: string;
  email: string;
  roles: string[];
}

export interface MeResponse {
  id: string;
  email: string;
  displayName: string | null;
  roles: string[];
}

export const authApi = {
  login: async (email: string, password: string) => {
    const res = await apiClient.post<ApiResponse<AuthToken>>('/auth/login', { email, password });
    return unwrap(res);
  },
  register: async (email: string, password: string, displayName?: string) => {
    const res = await apiClient.post<ApiResponse<AuthToken>>('/auth/register', { email, password, displayName });
    return unwrap(res);
  },
  me: async () => {
    const res = await apiClient.get<ApiResponse<MeResponse>>('/auth/me');
    return unwrap(res);
  },
};
