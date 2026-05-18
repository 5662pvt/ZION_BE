import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { MainLayout } from '@/layouts/MainLayout';
import { ProtectedRoute } from './ProtectedRoute';

const LoginPage = lazy(() => import('@/modules/auth/pages/LoginPage').then((m) => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => import('@/modules/auth/pages/RegisterPage').then((m) => ({ default: m.RegisterPage })));
const ProductsListPage = lazy(() => import('@/modules/products/pages/ProductsListPage').then((m) => ({ default: m.ProductsListPage })));
const ProductDetailPage = lazy(() => import('@/modules/products/pages/ProductDetailPage').then((m) => ({ default: m.ProductDetailPage })));
const CartPage = lazy(() => import('@/modules/cart/pages/CartPage').then((m) => ({ default: m.CartPage })));
const AccountPage = lazy(() => import('@/modules/account/pages/AccountPage').then((m) => ({ default: m.AccountPage })));

const Fallback = () => <div className="py-10 text-center text-slate-500">Loading…</div>;

export const router = createBrowserRouter([
  {
    element: <MainLayout />,
    children: [
      { path: '/', element: <Navigate to="/products" replace /> },
      { path: '/login', element: <Suspense fallback={<Fallback />}><LoginPage /></Suspense> },
      { path: '/register', element: <Suspense fallback={<Fallback />}><RegisterPage /></Suspense> },
      { path: '/products', element: <Suspense fallback={<Fallback />}><ProductsListPage /></Suspense> },
      { path: '/products/:slug', element: <Suspense fallback={<Fallback />}><ProductDetailPage /></Suspense> },
      { path: '/cart', element: <Suspense fallback={<Fallback />}><CartPage /></Suspense> },
      {
        path: '/account',
        element: (
          <ProtectedRoute>
            <Suspense fallback={<Fallback />}><AccountPage /></Suspense>
          </ProtectedRoute>
        ),
      },
      { path: '*', element: <div className="py-10 text-center text-slate-500">Not found</div> },
    ],
  },
]);
