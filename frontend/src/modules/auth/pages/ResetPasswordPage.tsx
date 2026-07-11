import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { PasswordInput } from '../components/PasswordInput';
import { VerifyEmailPanel } from '../components/VerifyEmailPanel';
import { useResetPassword } from '../hooks/useResetPassword';
import { isEmailNotVerifiedMessage } from '../utils/authErrors';

interface FormValues {
  email: string;
  code: string;
  newPassword: string;
  confirmPassword: string;
}

interface ResetLocationState {
  devCode?: string;
}

const passwordRules = {
  required: true,
  minLength: { value: 8, message: 'Password must be at least 8 characters' },
  validate: {
    upper: (v: string) => /[A-Z]/.test(v) || 'Password must contain uppercase',
    lower: (v: string) => /[a-z]/.test(v) || 'Password must contain lowercase',
    digit: (v: string) => /[0-9]/.test(v) || 'Password must contain a digit',
  },
};

export function ResetPasswordPage() {
  const [verifyEmail, setVerifyEmail] = useState<string | null>(null);
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const devCodeHint = (location.state as ResetLocationState | null)?.devCode;
  const defaultEmail = searchParams.get('email') ?? '';

  const { register, handleSubmit, formState, watch, setValue } = useForm<FormValues>({
    defaultValues: { email: defaultEmail, code: '', newPassword: '', confirmPassword: '' },
  });
  const reset = useResetPassword();
  const code = watch('code');

  useEffect(() => {
    if (devCodeHint) setValue('code', devCodeHint);
  }, [devCodeHint, setValue]);

  const onSubmit = handleSubmit((values) => {
    if (values.newPassword !== values.confirmPassword) return;
    reset.mutate({ email: values.email, code: values.code.trim(), newPassword: values.newPassword });
  });

  const newPassword = watch('newPassword');
  const confirmPassword = watch('confirmPassword');
  const mismatch = confirmPassword.length > 0 && newPassword !== confirmPassword;
  const resetError = reset.isError ? (reset.error as Error).message : undefined;
  const showVerifyHelp = reset.isError && isEmailNotVerifiedMessage(resetError);

  if (verifyEmail) {
    return (
      <div className="mx-auto max-w-sm card p-6">
        <VerifyEmailPanel
          email={verifyEmail}
          onBack={() => setVerifyEmail(null)}
          backLabel="Back to reset password"
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-sm card p-6">
      <h1 className="text-xl font-semibold mb-2">Reset password</h1>
      <p className="text-sm text-slate-500 mb-4">
        Use the 6-digit <strong>password reset</strong> code (not the email verification code). Each request
        invalidates previous codes.
      </p>
      {devCodeHint && (
        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 mb-4 text-sm text-green-900">
          Dev reset code:{' '}
          <span className="font-mono text-lg font-bold tracking-widest">{devCodeHint}</span>
        </div>
      )}
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
            value={code}
            onChange={(e) => setValue('code', e.target.value.replace(/\D/g, '').slice(0, 6), { shouldValidate: true })}
            required
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">New password</label>
          <PasswordInput autoComplete="new-password" {...register('newPassword', passwordRules)} />
          {formState.errors.newPassword && (
            <p className="text-red-600 text-xs mt-1">{formState.errors.newPassword.message}</p>
          )}
          <p className="text-xs text-slate-500 mt-1">8+ chars, upper, lower, digit.</p>
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Confirm password</label>
          <PasswordInput autoComplete="new-password" {...register('confirmPassword', { required: true })} />
          {mismatch && <p className="text-red-600 text-xs mt-1">Passwords do not match</p>}
        </div>
        {reset.isError && !showVerifyHelp && (
          <div className="text-red-600 text-sm">{resetError}</div>
        )}
        {showVerifyHelp && (
          <div className="rounded-md border border-amber-200 bg-amber-50 p-3 space-y-2">
            <p className="text-sm text-amber-900">
              This account is not verified yet. Verify your email first, or request a new reset code after verifying.
            </p>
            <button
              type="button"
              className="btn-primary w-full"
              onClick={() => setVerifyEmail(watch('email').trim().toLowerCase())}
              disabled={!watch('email')?.trim()}
            >
              Verify email
            </button>
          </div>
        )}
        <button
          type="submit"
          className="btn-primary w-full"
          disabled={formState.isSubmitting || reset.isPending || mismatch || code.length !== 6}
        >
          {reset.isPending ? 'Resetting…' : 'Reset password'}
        </button>
      </form>
      <p className="text-sm text-slate-500 mt-4">
        Wrong or expired code?{' '}
        <Link to="/forgot-password" className="text-brand-700 hover:underline">
          Request a new code
        </Link>
      </p>
      <p className="text-sm text-slate-500 mt-2">
        <Link to="/login" className="text-brand-700 hover:underline">
          Back to sign in
        </Link>
      </p>
    </div>
  );
}
