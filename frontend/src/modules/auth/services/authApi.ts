import { apiClient, postAndUnwrap, requestAndUnwrap } from '@/services/apiClient';
import type { ApiResponse } from '@/shared/types/api';

export interface AuthToken {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  userId: string;
  email: string;
  roles: string[];
}

export interface RegisterPending {
  email: string;
  requiresVerification: boolean;
}

export interface MessageResponse {
  message: string;
}

export interface MeResponse {
  id: string;
  email: string;
  displayName: string | null;
  roles: string[];
}

export const authApi = {
  login: (email: string, password: string) =>
    requestAndUnwrap<AuthToken>(() => apiClient.post('/auth/login', { email, password })),
  register: (email: string, password: string, displayName?: string) =>
    requestAndUnwrap<RegisterPending>(() => apiClient.post('/auth/register', { email, password, displayName })),
  verifyEmail: (email: string, code: string) =>
    requestAndUnwrap<AuthToken>(() => apiClient.post('/auth/verify-email', { email, code })),
  resendVerification: (email: string) =>
    requestAndUnwrap<MessageResponse>(() => apiClient.post('/auth/resend-verification', { email })),
  forgotPassword: (email: string) =>
    requestAndUnwrap<MessageResponse>(() => apiClient.post('/auth/forgot-password', { email })),
  resetPassword: (email: string, code: string, newPassword: string) =>
    postAndUnwrap<MessageResponse>('/auth/reset-password', { email, code, newPassword }),
  me: () => requestAndUnwrap<MeResponse>(() => apiClient.get('/auth/me')),
  revoke: async (refreshToken: string) => {
    const res = await apiClient.post<ApiResponse<unknown>>('/auth/revoke', { refreshToken });
    if (!res.data.success) throw new Error(res.data.message || 'Revoke failed');
  },
};
