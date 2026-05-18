import { useQuery } from '@tanstack/react-query';
import { apiClient, unwrap } from '@/services/apiClient';
import type { ApiResponse } from '@/shared/types/api';

interface ProfileDto {
  id: string;
  authUserId: string;
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  addresses: Array<{ id: string; line1: string; city: string; country: string; postalCode: string; isDefault: boolean }>;
}

export function AccountPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['profile-me'],
    queryFn: async () => unwrap(await apiClient.get<ApiResponse<ProfileDto>>('/users/me')),
  });

  if (isLoading) return <div className="py-10 text-center text-slate-500">Loading…</div>;
  if (isError) return <div className="py-10 text-center text-red-600">{(error as Error).message}</div>;
  if (!data) return null;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">My account</h1>
      <div className="card p-4 space-y-2 text-sm">
        <div><span className="text-slate-500">Email:</span> {data.email}</div>
        <div><span className="text-slate-500">Full name:</span> {data.fullName ?? '—'}</div>
        <div><span className="text-slate-500">Phone:</span> {data.phoneNumber ?? '—'}</div>
      </div>
      <div className="card p-4">
        <h2 className="font-semibold mb-2">Addresses ({data.addresses.length})</h2>
        {data.addresses.length === 0 ? (
          <div className="text-sm text-slate-500">No addresses yet.</div>
        ) : (
          <ul className="space-y-2 text-sm">
            {data.addresses.map((a) => (
              <li key={a.id} className="border-b border-slate-100 pb-2 last:border-none">
                {a.line1}, {a.city}, {a.country} {a.postalCode} {a.isDefault ? '· (default)' : ''}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
