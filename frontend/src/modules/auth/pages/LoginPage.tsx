import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { PasswordInput } from '../components/PasswordInput';
import { VerifyEmailPanel } from '../components/VerifyEmailPanel';
import { useLogin } from '../hooks/useLogin';
import { useResendVerification } from '../hooks/useResendVerification';
import { isEmailNotVerifiedMessage } from '../utils/authErrors';

interface FormValues {
  email: string;
  password: string;
}

export function LoginPage() {
  const [verifyEmail, setVerifyEmail] = useState<string | null>(null);
  const { register, handleSubmit, formState, getValues, watch } = useForm<FormValues>({
    defaultValues: { email: 'admin@zionshop.local', password: 'Admin@123' },
  });
  const login = useLogin();
  const resend = useResendVerification();
  const emailValue = watch('email');

  const loginError = login.isError ? (login.error as Error).message : undefined;
  const showUnverifiedHelp = login.isError && isEmailNotVerifiedMessage(loginError);

  const startVerify = (email: string) => {
    const trimmed = email.trim().toLowerCase();
    if (!trimmed) return;
    setVerifyEmail(trimmed);
    login.reset();
  };

  const handleResendCode = () => {
    const email = getValues('email').trim();
    if (!email) return;
    resend.mutate(email, { onSuccess: () => startVerify(email) });
  };

  const onSubmit = handleSubmit((values) => login.mutate(values));

  if (verifyEmail) {
    return (
      <div className="mx-auto max-w-sm card p-6">
        <VerifyEmailPanel
          email={verifyEmail}
          onBack={() => setVerifyEmail(null)}
          backLabel="Back to sign in"
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-4">Sign in</h1>
      <form className="space-y-4" onSubmit={onSubmit}>
        <div>
          <label className="block text-sm font-medium mb-1">Email</label>
          <input
            className="input"
            type="email"
            autoComplete="email"
            {...register('email', { required: true })}
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Password</label>
          <PasswordInput autoComplete="current-password" {...register('password', { required: true })} />
        </div>
        {login.isError && !showUnverifiedHelp && (
          <div className="text-red-600 text-sm">{loginError || 'Login failed'}</div>
        )}
        {showUnverifiedHelp && (
          <div className="rounded-md border border-amber-200 bg-amber-50 p-3 space-y-3">
            <p className="text-sm text-amber-900">
              Your email is not verified yet. Resend the code or enter the code you received.
            </p>
            <div className="flex flex-col gap-2 sm:flex-row">
              <button
                type="button"
                className="btn-primary flex-1"
                disabled={resend.isPending || !emailValue?.trim()}
                onClick={handleResendCode}
              >
                {resend.isPending ? 'Sending…' : 'Resend code'}
              </button>
              <button
                type="button"
                className="btn-outline flex-1"
                disabled={!emailValue?.trim()}
                onClick={() => startVerify(getValues('email'))}
              >
                Enter code
              </button>
            </div>
            {resend.isError && (
              <p className="text-red-600 text-sm">{(resend.error as Error).message || 'Could not resend code'}</p>
            )}
          </div>
        )}
        <div className="text-right">
          <Link to="/forgot-password" className="text-sm text-brand-700 hover:underline">
            Forgot password?
          </Link>
        </div>
        <button type="submit" className="btn-primary w-full" disabled={formState.isSubmitting || login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        No account?{' '}
        <Link to="/register" className="text-brand-700 hover:underline">
          Register
        </Link>
      </p>
      <p className="text-xs text-slate-400 mt-2">
        Seed admin: <code>admin@zionshop.local</code> / <code>Admin@123</code>
      </p>
    </div>
  );
}
