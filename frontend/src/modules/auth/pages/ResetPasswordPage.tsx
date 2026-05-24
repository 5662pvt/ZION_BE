import { useForm } from 'react-hook-form';
import { Link, useSearchParams } from 'react-router-dom';
import { useResetPassword } from '../hooks/useResetPassword';

interface FormValues {
  email: string;
  code: string;
  newPassword: string;
  confirmPassword: string;
}

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const defaultEmail = searchParams.get('email') ?? '';

  const { register, handleSubmit, formState, watch } = useForm<FormValues>({
    defaultValues: { email: defaultEmail, code: '', newPassword: '', confirmPassword: '' },
  });
  const reset = useResetPassword();

  const onSubmit = handleSubmit((values) => {
    if (values.newPassword !== values.confirmPassword) return;
    reset.mutate({ email: values.email, code: values.code.trim(), newPassword: values.newPassword });
  });

  const newPassword = watch('newPassword');
  const confirmPassword = watch('confirmPassword');
  const mismatch = confirmPassword.length > 0 && newPassword !== confirmPassword;

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-2">Reset password</h1>
      <p className="text-sm text-slate-500 mb-4">
        Enter the 6-digit code from your email (in dev, check the API console for{' '}
        <span className="font-mono text-xs">[DEV EMAIL]</span>) and choose a new password.
      </p>
      <form className="space-y-4" onSubmit={onSubmit}>
        <div>
          <label className="block text-sm font-medium mb-1">Email</label>
          <input className="input w-full" type="email" {...register('email', { required: true })} />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Reset code</label>
          <input
            className="input w-full tracking-widest text-center"
            inputMode="numeric"
            maxLength={6}
            {...register('code', { required: true, minLength: 6, maxLength: 6 })}
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">New password</label>
          <input className="input w-full" type="password" {...register('newPassword', { required: true, minLength: 8 })} />
          <p className="text-xs text-slate-500 mt-1">8+ chars, upper, lower, digit.</p>
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Confirm password</label>
          <input className="input w-full" type="password" {...register('confirmPassword', { required: true })} />
          {mismatch && <p className="text-red-600 text-xs mt-1">Passwords do not match</p>}
        </div>
        {reset.isError && (
          <div className="text-red-600 text-sm">{(reset.error as Error).message}</div>
        )}
        <button
          type="submit"
          className="btn-primary w-full"
          disabled={formState.isSubmitting || reset.isPending || mismatch}
        >
          {reset.isPending ? 'Resetting…' : 'Reset password'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        <Link to="/login" className="text-brand-700 hover:underline">Back to sign in</Link>
      </p>
    </div>
  );
}
