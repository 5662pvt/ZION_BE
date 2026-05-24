import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { useCart } from '@/modules/cart/hooks/useCart';

export function MainLayout() {
  const { user, isAuthenticated, logout } = useAuth();
  const cartQuery = useCart();
  const navigate = useNavigate();
  const itemCount = cartQuery.data?.totalQuantity ?? 0;

  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto max-w-6xl flex items-center justify-between px-4 py-3">
          <Link to="/" className="text-xl font-semibold text-brand-700">ZIONShop</Link>
          <nav className="flex items-center gap-4 text-sm">
            <NavLink to="/products" className={({ isActive }) => isActive ? 'text-brand-700 font-medium' : 'text-slate-600 hover:text-slate-900'}>Products</NavLink>
            <NavLink to="/cart" className={({ isActive }) => isActive ? 'text-brand-700 font-medium' : 'text-slate-600 hover:text-slate-900'}>
              Cart{itemCount > 0 ? ` (${itemCount})` : ''}
            </NavLink>
            {isAuthenticated ? (
              <div className="flex items-center gap-3">
                <span className="text-slate-500 hidden sm:inline">{user?.email}</span>
                <button className="btn-outline" onClick={() => { logout(); navigate('/'); }}>Logout</button>
              </div>
            ) : (
              <>
                <Link to="/register" className="btn-outline">Register</Link>
                <Link to="/login" className="btn-primary">Login</Link>
              </>
            )}
          </nav>
        </div>
      </header>
      <main className="flex-1">
        <div className="mx-auto max-w-6xl px-4 py-6">
          <Outlet />
        </div>
      </main>
      <footer className="border-t border-slate-200 bg-white py-4 text-center text-xs text-slate-500">
        ZIONShop demo &middot; Phase 1 scaffold
      </footer>
    </div>
  );
}
