import { apiClient, unwrap } from '@/services/apiClient';
import type { ApiResponse } from '@/shared/types/api';

export interface ProfileDto {
  id: string;
  authUserId: string;
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  addresses: AddressDto[];
}

export interface AddressDto {
  id: string;
  line1: string;
  line2: string | null;
  city: string;
  state: string | null;
  country: string;
  postalCode: string;
  isDefault: boolean;
}

export interface UpdateProfilePayload {
  fullName?: string | null;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
}

export interface AddAddressPayload {
  line1: string;
  line2?: string | null;
  city: string;
  state?: string | null;
  country: string;
  postalCode: string;
  isDefault: boolean;
}

export const accountApi = {
  getProfile: async (): Promise<ProfileDto> => {
    const res = await apiClient.get<ApiResponse<ProfileDto>>('/users/me');
    return unwrap(res);
  },
  updateProfile: async (payload: UpdateProfilePayload): Promise<ProfileDto> => {
    const res = await apiClient.put<ApiResponse<ProfileDto>>('/users/me', payload);
    return unwrap(res);
  },
  addAddress: async (payload: AddAddressPayload): Promise<AddressDto> => {
    const res = await apiClient.post<ApiResponse<AddressDto>>('/users/me/addresses', payload);
    return unwrap(res);
  },
};
