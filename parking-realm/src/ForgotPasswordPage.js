import { useState } from 'react';
import { forgotPassword } from './api/auth';
import './RegisterPage.css';

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+$/;

export default function ForgotPasswordPage({ onBack }) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [generalError, setGeneralError] = useState('');
  const [sent, setSent] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event) {
    event.preventDefault();
    setGeneralError('');
    setSent(false);

    const value = email.trim();
    const clientError = !value ? 'Email mangler.' : !EMAIL_REGEX.test(value) ? 'Email er ikke en gyldig emailadresse.' : '';
    setError(clientError);
    if (clientError) return;

    setSubmitting(true);
    const result = await forgotPassword(value);
    setSubmitting(false);

    if (result.ok) {
      setSent(true);
      setEmail('');
      return;
    }
    setError(result.fieldErrors?.email ?? '');
    setGeneralError(result.generalError ?? '');
  }

  return (
    <main className="register">
      <p className="eyebrow mb-3">Har du glemt din adgangskode?</p>
      <h1>Nulstil adgangskode</h1>
      <p className="page-description">Indtast din email, så sender vi et link til at vælge en ny adgangskode.</p>

      {sent && (
        <p role="status" className="register-success">
          Hvis emailen tilhører en konto, er der sendt en email med et link til at nulstille adgangskoden. Linket er gyldigt i 30 minutter.
        </p>
      )}
      {generalError && (
        <p role="alert" className="register-error">
          {generalError}
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate>
        <div className="register-field">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            aria-invalid={error ? 'true' : 'false'}
            aria-describedby={error ? 'email-error' : undefined}
          />
          {error && (
            <p id="email-error" className="register-error">
              {error}
            </p>
          )}
        </div>
        <button className="btn btn-primary mt-2 w-full" type="submit" disabled={submitting}>
          {submitting ? 'Sender…' : 'Send nulstillingslink'}
        </button>
      </form>
      <button className="mt-6 text-sm font-semibold text-brand-700 hover:text-brand-900" type="button" onClick={onBack}>
        Tilbage til log ind
      </button>
    </main>
  );
}
