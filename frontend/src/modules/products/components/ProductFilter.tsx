import { useCategories } from '../hooks/useProducts';

interface Props {
  keyword: string;
  categoryId: string;
  sort: string;
  onChange: (next: { keyword?: string; categoryId?: string; sort?: string }) => void;
}

export function ProductFilter({ keyword, categoryId, sort, onChange }: Props) {
  const categories = useCategories();
  return (
    <div className="card p-4 flex flex-col sm:flex-row gap-3 items-stretch sm:items-end">
      <div className="flex-1">
        <label className="block text-xs font-medium text-slate-600 mb-1">Search</label>
        <input className="input" value={keyword} onChange={(e) => onChange({ keyword: e.target.value })} placeholder="Search by name or SKU" />
      </div>
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">Category</label>
        <select className="input" value={categoryId} onChange={(e) => onChange({ categoryId: e.target.value })}>
          <option value="">All</option>
          {categories.data?.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
      </div>
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">Sort</label>
        <select className="input" value={sort} onChange={(e) => onChange({ sort: e.target.value })}>
          <option value="name">Name</option>
          <option value="price_asc">Price ↑</option>
          <option value="price_desc">Price ↓</option>
          <option value="newest">Newest</option>
        </select>
      </div>
    </div>
  );
}
