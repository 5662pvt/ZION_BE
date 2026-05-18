import { useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ProductCard } from '../components/ProductCard';
import { ProductFilter } from '../components/ProductFilter';
import { Pagination } from '../components/Pagination';
import { useProducts } from '../hooks/useProducts';

export function ProductsListPage() {
  const [search, setSearch] = useSearchParams();

  const params = useMemo(() => ({
    page: Number(search.get('page') ?? '1') || 1,
    pageSize: 12,
    keyword: search.get('q') || undefined,
    categoryId: search.get('cat') || undefined,
    sort: search.get('sort') || 'name',
  }), [search]);

  const { data, isLoading, isError, error } = useProducts(params);

  const update = (next: { keyword?: string; categoryId?: string; sort?: string; page?: number }) => {
    const merged = new URLSearchParams(search);
    if (next.keyword !== undefined) {
      if (next.keyword) merged.set('q', next.keyword); else merged.delete('q');
      merged.set('page', '1');
    }
    if (next.categoryId !== undefined) {
      if (next.categoryId) merged.set('cat', next.categoryId); else merged.delete('cat');
      merged.set('page', '1');
    }
    if (next.sort !== undefined) {
      merged.set('sort', next.sort);
      merged.set('page', '1');
    }
    if (next.page !== undefined) merged.set('page', String(next.page));
    setSearch(merged);
  };

  return (
    <div className="space-y-4">
      <ProductFilter
        keyword={params.keyword ?? ''}
        categoryId={params.categoryId ?? ''}
        sort={params.sort ?? 'name'}
        onChange={update}
      />
      {isLoading && <div className="py-10 text-center text-slate-500">Loading…</div>}
      {isError && <div className="py-10 text-center text-red-600">{(error as Error).message}</div>}
      {data && (
        <>
          {data.items.length === 0 ? (
            <div className="py-10 text-center text-slate-500">No products found.</div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              {data.items.map((p) => <ProductCard key={p.id} product={p} />)}
            </div>
          )}
          <Pagination page={data.pagination.page} totalPages={data.pagination.totalPages} onChange={(p) => update({ page: p })} />
        </>
      )}
    </div>
  );
}
