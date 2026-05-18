import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { useLogin } from '../hooks/useLogin';

interface FormValues {
  email: string;
  password: string;
}

export function LoginPage() {
  const { register, handleSubmit, formState } = useForm<FormValues>({
    defaultValues: { email: 'admin@zionshop.local', password: 'Admin@123' },
  });
  const login = useLogin();
  const onSubmit = handleSubmit((values) => login.mutate(values));

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-4">Sign in</h1>
      <form className="space-y-4" onSubmit={onSubmit}>
        <div>
          <label className="block text-sm font-medium mb-1">Email</label>
          <input className="input" type="email" autoComplete="email"
            {...register('email', { required: true })} />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Password</label>
          <input className="input" type="password" autoComplete="current-password"
            {...register('password', { required: true })} />
        </div>
        {login.isError && (
          <div className="text-red-600 text-sm">
            {(login.error as Error).message || 'Login failed'}
          </div>
        )}
        <button type="submit" className="btn-primary w-full" disabled={formState.isSubmitting || login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        No account? <Link to="/register" className="text-brand-700 hover:underline">Register</Link>
      </p>
      <p className="text-xs text-slate-400 mt-2">
        Seed admin: <code>admin@zionshop.local</code> / <code>Admin@123</code>
      </p>
    </div>
  );
}
