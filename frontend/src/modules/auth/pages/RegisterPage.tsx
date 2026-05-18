import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { useRegister } from '../hooks/useRegister';

interface FormValues {
  email: string;
  password: string;
  displayName?: string;
}

export function RegisterPage() {
  const { register, handleSubmit, formState } = useForm<FormValues>();
  const reg = useRegister();
  const onSubmit = handleSubmit((values) => reg.mutate(values));

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-4">Create account</h1>
      <form className="space-y-4" onSubmit={onSubmit}>
        <div>
          <label className="block text-sm font-medium mb-1">Email</label>
          <input className="input" type="email" {...register('email', { required: true })} />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Display name</label>
          <input className="input" type="text" {...register('displayName')} />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Password</label>
          <input className="input" type="password" {...register('password', { required: true, minLength: 8 })} />
          <p className="text-xs text-slate-500 mt-1">8+ chars, must contain upper, lower, digit.</p>
        </div>
        {reg.isError && (
          <div className="text-red-600 text-sm">{(reg.error as Error).message || 'Registration failed'}</div>
        )}
        <button className="btn-primary w-full" disabled={formState.isSubmitting || reg.isPending}>
          {reg.isPending ? 'Creating…' : 'Create account'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        Already have an account? <Link to="/login" className="text-brand-700 hover:underline">Sign in</Link>
      </p>
    </div>
  );
}
