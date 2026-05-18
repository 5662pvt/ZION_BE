import { ACCESS_TOKEN_KEY, CART_TOKEN_KEY, REFRESH_TOKEN_KEY } from '@/shared/constants';

export const tokenStorage = {
  getAccess: () => localStorage.getItem(ACCESS_TOKEN_KEY),
  getRefresh: () => localStorage.getItem(REFRESH_TOKEN_KEY),
  set: (access: string, refresh: string) => {
    localStorage.setItem(ACCESS_TOKEN_KEY, access);
    localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
  },
  clear: () => {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  },
  getCartToken: () => localStorage.getItem(CART_TOKEN_KEY),
  setCartToken: (token: string) => localStorage.setItem(CART_TOKEN_KEY, token),
};
