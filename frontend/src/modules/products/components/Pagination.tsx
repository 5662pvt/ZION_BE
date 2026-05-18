interface Props {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
}

export function Pagination({ page, totalPages, onChange }: Props) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-center gap-2 mt-6">
      <button className="btn-outline" disabled={page <= 1} onClick={() => onChange(page - 1)}>Prev</button>
      <span className="text-sm text-slate-600">Page {page} of {totalPages}</span>
      <button className="btn-outline" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Next</button>
    </div>
  );
}
