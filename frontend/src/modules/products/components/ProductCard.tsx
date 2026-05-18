import { Link } from 'react-router-dom';
import type { ProductSummary } from '../types';

interface Props {
  product: ProductSummary;
}

export function ProductCard({ product }: Props) {
  return (
    <Link to={`/products/${product.slug}`} className="card overflow-hidden hover:shadow-md transition">
      <div className="aspect-square w-full bg-slate-100 overflow-hidden">
        {product.primaryImageUrl ? (
          <img src={product.primaryImageUrl} alt={product.name} className="w-full h-full object-cover" />
        ) : (
          <div className="flex h-full items-center justify-center text-slate-400 text-sm">No image</div>
        )}
      </div>
      <div className="p-3">
        <div className="text-xs text-slate-500">{product.categoryName ?? 'Uncategorized'}</div>
        <div className="font-medium text-slate-900 truncate">{product.name}</div>
        <div className="mt-1 text-brand-700 font-semibold">
          {product.currency} {product.price.toFixed(2)}
        </div>
      </div>
    </Link>
  );
}
