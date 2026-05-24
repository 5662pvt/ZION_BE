import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { useQuery } from '@tanstack/react-query';
import { accountApi } from '../services/accountApi';
import { useUpdateProfile } from '../hooks/useUpdateProfile';
import { useAddAddress } from '../hooks/useAddAddress';

interface ProfileFormValues {
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
}

interface AddressFormValues {
  line1: string;
  line2: string;
  city: string;
  state: string;
  country: string;
  postalCode: string;
  isDefault: boolean;
}

export function AccountPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['profile-me'],
    queryFn: () => accountApi.getProfile(),
  });

  const updateProfile = useUpdateProfile();
  const addAddress = useAddAddress();

  const profileForm = useForm<ProfileFormValues>({
    defaultValues: { fullName: '', phoneNumber: '', dateOfBirth: '' },
  });

  const addressForm = useForm<AddressFormValues>({
    defaultValues: {
      line1: '',
      line2: '',
      city: '',
      state: '',
      country: '',
      postalCode: '',
      isDefault: false,
    },
  });

  useEffect(() => {
    if (!data) return;
    profileForm.reset({
      fullName: data.fullName ?? '',
      phoneNumber: data.phoneNumber ?? '',
      dateOfBirth: data.dateOfBirth ? data.dateOfBirth.slice(0, 10) : '',
    });
  }, [data, profileForm]);

  const onProfileSubmit = profileForm.handleSubmit((values) => {
    updateProfile.mutate({
      fullName: values.fullName || null,
      phoneNumber: values.phoneNumber || null,
      dateOfBirth: values.dateOfBirth || null,
    });
  });

  const onAddressSubmit = addressForm.handleSubmit((values) => {
    addAddress.mutate(
      {
        line1: values.line1,
        line2: values.line2 || null,
        city: values.city,
        state: values.state || null,
        country: values.country,
        postalCode: values.postalCode,
        isDefault: values.isDefault,
      },
      {
        onSuccess: () => {
          addressForm.reset({
            line1: '',
            line2: '',
            city: '',
            state: '',
            country: '',
            postalCode: '',
            isDefault: false,
          });
        },
      },
    );
  });

  if (isLoading) return <div className="py-10 text-center text-slate-500">Loading…</div>;
  if (isError) return <div className="py-10 text-center text-red-600">{(error as Error).message}</div>;
  if (!data) return null;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">My account</h1>

      <div className="card p-4 space-y-3">
        <div className="text-sm text-slate-500">Email: {data.email}</div>

        <h2 className="font-semibold">Profile</h2>
        <form className="space-y-3" onSubmit={onProfileSubmit}>
          <div>
            <label className="block text-sm font-medium mb-1">Full name</label>
            <input className="input w-full" {...profileForm.register('fullName')} />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Phone</label>
            <input className="input w-full" {...profileForm.register('phoneNumber')} />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Date of birth</label>
            <input className="input w-full" type="date" {...profileForm.register('dateOfBirth')} />
          </div>
          {updateProfile.isError && (
            <div className="text-red-600 text-sm">{(updateProfile.error as Error).message}</div>
          )}
          {updateProfile.isSuccess && (
            <div className="text-green-700 text-sm">Profile updated.</div>
          )}
          <button
            type="submit"
            className="btn-primary"
            disabled={profileForm.formState.isSubmitting || updateProfile.isPending}
          >
            {updateProfile.isPending ? 'Saving…' : 'Save profile'}
          </button>
        </form>
      </div>

      <div className="card p-4">
        <h2 className="font-semibold mb-2">Addresses ({data.addresses.length})</h2>
        {data.addresses.length === 0 ? (
          <div className="text-sm text-slate-500 mb-4">No addresses yet.</div>
        ) : (
          <ul className="space-y-2 text-sm mb-4">
            {data.addresses.map((a) => (
              <li key={a.id} className="border-b border-slate-100 pb-2 last:border-none">
                {a.line1}
                {a.line2 ? `, ${a.line2}` : ''}, {a.city}
                {a.state ? `, ${a.state}` : ''}, {a.country} {a.postalCode}
                {a.isDefault ? ' · (default)' : ''}
              </li>
            ))}
          </ul>
        )}

        <h3 className="font-medium text-sm mb-2">Add address</h3>
        <form className="space-y-3" onSubmit={onAddressSubmit}>
          <div>
            <label className="block text-sm font-medium mb-1">Address line 1</label>
            <input className="input w-full" {...addressForm.register('line1', { required: true })} />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Address line 2</label>
            <input className="input w-full" {...addressForm.register('line2')} />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium mb-1">City</label>
              <input className="input w-full" {...addressForm.register('city', { required: true })} />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">State</label>
              <input className="input w-full" {...addressForm.register('state')} />
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium mb-1">Country</label>
              <input className="input w-full" {...addressForm.register('country', { required: true })} />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">Postal code</label>
              <input className="input w-full" {...addressForm.register('postalCode', { required: true })} />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" {...addressForm.register('isDefault')} />
            Set as default address
          </label>
          {addAddress.isError && (
            <div className="text-red-600 text-sm">{(addAddress.error as Error).message}</div>
          )}
          <button
            type="submit"
            className="btn-primary"
            disabled={addressForm.formState.isSubmitting || addAddress.isPending}
          >
            {addAddress.isPending ? 'Adding…' : 'Add address'}
          </button>
        </form>
      </div>
    </div>
  );
}
