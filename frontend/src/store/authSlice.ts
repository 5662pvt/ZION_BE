import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { tokenStorage } from '@/services/tokenStorage';

export interface AuthUser {
  id: string;
  email: string;
  roles: string[];
  displayName?: string | null;
}

interface AuthState {
  user: AuthUser | null;
  status: 'idle' | 'loading';
}

const initialState: AuthState = {
  user: null,
  status: 'idle',
};

const slice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    authLoggedIn(state, action: PayloadAction<{ user: AuthUser; accessToken: string; refreshToken: string }>) {
      state.user = action.payload.user;
      tokenStorage.set(action.payload.accessToken, action.payload.refreshToken);
    },
    authUserUpdated(state, action: PayloadAction<AuthUser>) {
      state.user = action.payload;
    },
    authLoggedOut(state) {
      state.user = null;
      tokenStorage.clear();
    },
  },
});

export const { authLoggedIn, authUserUpdated, authLoggedOut } = slice.actions;
export const authReducer = slice.reducer;
