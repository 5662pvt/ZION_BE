import { Link, useNavigate, useParams } from 'react-router-dom';
import { useProductBySlug } from '../hooks/useProduct';
import { useAddToCart } from '@/modules/cart/hooks/useAddToCart';
import { useAuth } from '@/hooks/useAuth';
import { useState } from 'react';

export function ProductDetailPage() {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { data, isLoading, isError, error } = useProductBySlug(slug);
  const addToCart = useAddToCart();
  const [qty, setQty] = useState(1);

  if (isLoading) return <div className="py-10 text-center text-slate-500">Loading…</div>;
  if (isError) return <div className="py-10 text-center text-red-600">{(error as Error).message}</div>;
  if (!data) return null;

  const handleAdd = () => {
    if (!isAuthenticated) {
      navigate('/login', { state: { from: `/products/${data.slug}` } });
      return;
    }
    addToCart.mutate({ productId: data.id, quantity: qty });
  };

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
      <div className="card overflow-hidden">
        <div className="aspect-square bg-slate-100">
          {data.images[0] ? (
            <img src={data.images[0].url} alt={data.images[0].alt ?? data.name} className="w-full h-full object-cover" />
          ) : (
            <div className="flex h-full items-center justify-center text-slate-400 text-sm">No image</div>
          )}
        </div>
      </div>
      <div className="space-y-4">
        <div>
          <Link to="/products" className="text-sm text-slate-500 hover:text-slate-700">← Back to products</Link>
        </div>
        <h1 className="text-2xl font-semibold">{data.name}</h1>
        <div className="text-sm text-slate-500">SKU: {data.sku} {data.brand ? `· ${data.brand}` : ''}</div>
        <div className="text-3xl font-bold text-brand-700">{data.currency} {data.price.toFixed(2)}</div>
        {data.description && <p className="text-slate-700 leading-relaxed">{data.description}</p>}
        <div className="flex items-center gap-3">
          <label className="text-sm font-medium">Quantity</label>
          <input className="input w-20" type="number" min={1} max={100} value={qty}
            onChange={(e) => setQty(Math.max(1, Number(e.target.value) || 1))} />
        </div>
        <button className="btn-primary" onClick={handleAdd} disabled={addToCart.isPending}>
          {addToCart.isPending ? 'Adding…' : 'Add to cart'}
        </button>
        {addToCart.isError && (
          <div className="text-red-600 text-sm">{(addToCart.error as Error).message}</div>
        )}
        {addToCart.isSuccess && (
          <div className="text-green-700 text-sm">Added to cart — <Link to="/cart" className="underline">view cart</Link></div>
        )}
      </div>
    </div>
  );
}
