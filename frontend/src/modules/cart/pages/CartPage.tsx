import { Link } from 'react-router-dom';
import { useCart } from '../hooks/useCart';
import { CartItemRow } from '../components/CartItemRow';
import { CartSummary } from '../components/CartSummary';

export function CartPage() {
  const { data, isLoading, isError, error } = useCart();

  if (isLoading) return <div className="py-10 text-center text-slate-500">Loading cart…</div>;
  if (isError) return <div className="py-10 text-center text-red-600">{(error as Error).message}</div>;
  if (!data) return null;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Your cart</h1>
      {data.items.length === 0 ? (
        <div className="card p-6 text-center text-slate-500">
          Your cart is empty. <Link to="/products" className="text-brand-700 hover:underline">Browse products</Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div className="lg:col-span-2 space-y-3">
            {data.items.map((item) => <CartItemRow key={item.id} item={item} />)}
          </div>
          <div>
            <CartSummary cart={data} />
          </div>
        </div>
      )}
    </div>
  );
}
