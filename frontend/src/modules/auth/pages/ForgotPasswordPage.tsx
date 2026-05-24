import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { useForgotPassword } from '../hooks/useForgotPassword';

interface FormValues {
  email: string;
}

export function ForgotPasswordPage() {
  const { register, handleSubmit, formState } = useForm<FormValues>();
  const forgot = useForgotPassword();
  const onSubmit = handleSubmit((values) => forgot.mutate(values.email));

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-2">Forgot password</h1>
      <p className="text-sm text-slate-500 mb-4">
        Enter your email and we will send you a 6-digit reset code.
      </p>
      <form className="space-y-4" onSubmit={onSubmit}>
        <div>
          <label className="block text-sm font-medium mb-1">Email</label>
          <input className="input w-full" type="email" autoComplete="email" {...register('email', { required: true })} />
        </div>
        {forgot.isError && (
          <div className="text-red-600 text-sm">{(forgot.error as Error).message}</div>
        )}
        <button type="submit" className="btn-primary w-full" disabled={formState.isSubmitting || forgot.isPending}>
          {forgot.isPending ? 'Sending…' : 'Send reset code'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        <Link to="/login" className="text-brand-700 hover:underline">Back to sign in</Link>
      </p>
    </div>
  );
}
