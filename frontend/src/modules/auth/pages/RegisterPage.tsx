import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { useRegister } from '../hooks/useRegister';
import { useVerifyEmail } from '../hooks/useVerifyEmail';
import { useResendVerification } from '../hooks/useResendVerification';

interface FormValues {
  email: string;
  password: string;
  displayName?: string;
}

export function RegisterPage() {
  const [pendingEmail, setPendingEmail] = useState<string | null>(null);
  const [code, setCode] = useState('');

  const { register, handleSubmit, formState } = useForm<FormValues>();
  const reg = useRegister();
  const verify = useVerifyEmail();
  const resend = useResendVerification();

  const onRegister = handleSubmit((values) => {
    reg.mutate(values, {
      onSuccess: (data) => setPendingEmail(data.email),
    });
  });

  const onVerify = (e: React.FormEvent) => {
    e.preventDefault();
    if (!pendingEmail) return;
    verify.mutate({ email: pendingEmail, code: code.trim() });
  };

  if (pendingEmail) {
    return (
      <div className="mx-auto max-w-sm card p-6">
        <h1 className="text-xl font-semibold mb-2">Verify your email</h1>
        <p className="text-sm text-slate-500 mb-4">
          We sent a 6-digit code to <strong>{pendingEmail}</strong>.
        </p>
        <form className="space-y-4" onSubmit={onVerify}>
          <div>
            <label className="block text-sm font-medium mb-1">Verification code</label>
            <input
              className="input w-full tracking-widest text-center"
              inputMode="numeric"
              maxLength={6}
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              required
            />
          </div>
          {verify.isError && (
            <div className="text-red-600 text-sm">{(verify.error as Error).message || 'Invalid code'}</div>
          )}
          <button type="submit" className="btn-primary w-full" disabled={verify.isPending || code.length !== 6}>
            {verify.isPending ? 'Verifying…' : 'Verify and sign in'}
          </button>
        </form>
        <button
          type="button"
          className="btn-outline w-full mt-3"
          disabled={resend.isPending}
          onClick={() => resend.mutate(pendingEmail)}
        >
          {resend.isPending ? 'Sending…' : 'Resend code'}
        </button>
        {resend.isSuccess && <p className="text-green-700 text-sm mt-2">Code sent again.</p>}
        <p className="text-sm text-slate-500 mt-4">
          <Link to="/login" className="text-brand-700 hover:underline">Back to sign in</Link>
        </p>
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
          <input
            className="input"
            type="password"
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
        Already have an account? <Link to="/login" className="text-brand-700 hover:underline">Sign in</Link>
      </p>
    </div>
  );
}
