import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useVerifyEmail } from '../hooks/useVerifyEmail';
import { useResendVerification } from '../hooks/useResendVerification';

interface Props {
  email: string;
  onBack?: () => void;
  backLabel?: string;
}

export function VerifyEmailPanel({ email, onBack, backLabel = 'Back' }: Props) {
  const [code, setCode] = useState('');
  const verify = useVerifyEmail();
  const resend = useResendVerification();

  const onVerify = (e: React.FormEvent) => {
    e.preventDefault();
    verify.mutate({ email, code: code.trim() });
  };

  return (
    <>
      <h1 className="text-xl font-semibold mb-2">Verify your email</h1>
      <p className="text-sm text-slate-500 mb-4">
        We sent a 6-digit code to <strong>{email}</strong>.
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
        onClick={() => resend.mutate(email)}
      >
        {resend.isPending ? 'Sending…' : 'Resend code'}
      </button>
      {resend.isSuccess && <p className="text-green-700 text-sm mt-2">Code sent again.</p>}
      <p className="text-sm text-slate-500 mt-4">
        {onBack ? (
          <button type="button" className="text-brand-700 hover:underline" onClick={onBack}>
            {backLabel}
          </button>
        ) : (
          <Link to="/login" className="text-brand-700 hover:underline">
            Back to sign in
          </Link>
        )}
      </p>
    </>
  );
}
