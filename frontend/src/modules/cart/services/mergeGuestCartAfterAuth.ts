import { CART_TOKEN_HEADER } from '@/shared/constants';
import { tokenStorage } from '@/services/tokenStorage';
import { cartApi } from './cartApi';

export async function mergeGuestCartAfterAuth(): Promise<void> {
  const cartToken = tokenStorage.getCartToken();
  if (!cartToken) return;
  try {
    await cartApi.merge(cartToken);
  } finally {
    tokenStorage.clearCartToken();
  }
}
