import { useState } from 'react';
import { resetPassword } from './api/auth';
import { passwordError } from './registerValidation';
import './RegisterPage.css';

export default function ResetPasswordPage({ email, token, onDone, onBack }) {
  const [values, setValues] = useState({ password: '', confirm: '' });
  const [errors, setErrors] = useState({});
  const [generalError, setGeneralError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  function handleChange(event) {
    const { name, value } = event.target;
    setValues((current) => ({ ...current, [name]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setGeneralError('');

    const clientErrors = {};
    const ruleError = passwordError(values.password);
    if (ruleError) clientErrors.password = ruleError;
    if (!values.confirm) clientErrors.confirm = 'Bekræft adgangskoden.';
    else if (values.password !== values.confirm) clientErrors.confirm = 'Adgangskoderne er ikke ens.';
    setErrors(clientErrors);
    if (Object.keys(clientErrors).length > 0) return;

    setSubmitting(true);
    const result = await resetPassword({ email, token, newPassword: values.password });
    setSubmitting(false);

    if (result.ok) {
      onDone();
      return;
    }
    setErrors(result.fieldErrors ?? {});
    setGeneralError(result.generalError ?? '');
  }

  const fields = [
    { name: 'password', label: 'Ny adgangskode' },
    { name: 'confirm', label: 'Bekræft ny adgangskode' },
  ];

  return (
    <main className="register">
      <p className="eyebrow mb-3">Nulstil adgangskode</p>
      <h1>Vælg ny adgangskode</h1>
      <p className="page-description">Vælg en ny adgangskode til {email}.</p>

      {generalError && (
        <p role="alert" className="register-error">
          {generalError}
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate>
        {fields.map(({ name, label }) => (
          <div className="register-field" key={name}>
            <label htmlFor={name}>{label}</label>
            <input
              id={name}
              name={name}
              type="password"
              autoComplete="new-password"
              value={values[name]}
              onChange={handleChange}
              aria-invalid={errors[name] ? 'true' : 'false'}
              aria-describedby={errors[name] ? `${name}-error` : undefined}
            />
            {errors[name] && (
              <p id={`${name}-error`} className="register-error">
                {errors[name]}
              </p>
            )}
          </div>
        ))}
        <button className="btn btn-primary mt-2 w-full" type="submit" disabled={submitting}>
          {submitting ? 'Gemmer…' : 'Nulstil adgangskode'}
        </button>
      </form>
      <button className="mt-6 text-sm font-semibold text-brand-700 hover:text-brand-900" type="button" onClick={onBack}>
        Tilbage til log ind
      </button>
    </main>
  );
}
