import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { PasswordInput } from '../components/PasswordInput';
import { VerifyEmailPanel } from '../components/VerifyEmailPanel';
import { useRegister } from '../hooks/useRegister';

interface FormValues {
  email: string;
  password: string;
  displayName?: string;
}

export function RegisterPage() {
  const [pendingEmail, setPendingEmail] = useState<string | null>(null);

  const { register, handleSubmit, formState } = useForm<FormValues>();
  const reg = useRegister();

  const onRegister = handleSubmit((values) => {
    reg.mutate(values, {
      onSuccess: (data) => setPendingEmail(data.email),
    });
  });

  if (pendingEmail) {
    return (
      <div className="mx-auto max-w-sm card p-6">
        <VerifyEmailPanel
          email={pendingEmail}
          onBack={() => setPendingEmail(null)}
          backLabel="Back to registration"
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-4">Create account</h1>
      <form className="space-y-4" onSubmit={onRegister}>
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
          <PasswordInput
            autoComplete="new-password"
            {...register('password', {
              required: true,
              minLength: { value: 8, message: 'Password must be at least 8 characters' },
              validate: {
                upper: (v) => /[A-Z]/.test(v) || 'Password must contain uppercase',
                lower: (v) => /[a-z]/.test(v) || 'Password must contain lowercase',
                digit: (v) => /[0-9]/.test(v) || 'Password must contain a digit',
              },
            })}
          />
          {formState.errors.password && (
            <p className="text-red-600 text-xs mt-1">{formState.errors.password.message}</p>
          )}
          <p className="text-xs text-slate-500 mt-1">8+ chars, upper, lower, digit (e.g. Password1).</p>
        </div>
        {reg.isError && (
          <div className="text-red-600 text-sm">{(reg.error as Error).message || 'Registration failed'}</div>
        )}
        <button className="btn-primary w-full" disabled={formState.isSubmitting || reg.isPending}>
          {reg.isPending ? 'Creating…' : 'Create account'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        Already have an account?{' '}
        <Link to="/login" className="text-brand-700 hover:underline">
          Sign in
        </Link>
      </p>
    </div>
  );
}
