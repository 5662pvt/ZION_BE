import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Provider } from 'react-redux';
import { RouterProvider } from 'react-router-dom';
import { apiClient, setOnUnauthorized, unwrap } from '@/services/apiClient';
import { router } from '@/routes/routes';
import { store } from '@/store/store';
import { authUserUpdated, authLoggedOut } from '@/store/authSlice';
import { tokenStorage } from '@/services/tokenStorage';
import type { ApiResponse } from '@/shared/types/api';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 30_000 },
  },
});

export function App() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setOnUnauthorized(() => {
      store.dispatch(authLoggedOut());
      queryClient.clear();
    });
    const access = tokenStorage.getAccess();
    if (!access) {
      setReady(true);
      return;
    }
    apiClient
      .get<ApiResponse<{ id: string; email: string; displayName: string | null; roles: string[] }>>('/auth/me')
      .then((res) => {
        const user = unwrap(res);
        store.dispatch(authUserUpdated({ id: user.id, email: user.email, displayName: user.displayName, roles: user.roles }));
      })
      .catch(() => {
        tokenStorage.clear();
      })
      .finally(() => setReady(true));
  }, []);

  if (!ready) {
    return <div className="min-h-screen flex items-center justify-center text-slate-500">Loading ZIONShop…</div>;
  }

  return (
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </Provider>
  );
}
