import { useState } from 'react';
import { login } from './api/auth';
import './RegisterPage.css';

const FIELDS = [
  { name: 'email', label: 'Email', type: 'email', autoComplete: 'username' },
  { name: 'password', label: 'Adgangskode', type: 'password', autoComplete: 'current-password' },
];

const EMPTY = { email: '', password: '' };

export default function LoginPage({ onLoggedIn }) {
  const [values, setValues] = useState(EMPTY);
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
    if (!values.email.trim()) clientErrors.email = 'Email mangler.';
    if (!values.password) clientErrors.password = 'Adgangskode mangler.';
    setErrors(clientErrors);
    if (Object.keys(clientErrors).length > 0) return;

    setSubmitting(true);
    const result = await login({ email: values.email.trim(), password: values.password });
    setSubmitting(false);

    if (result.ok) {
      onLoggedIn(result.user);
      return;
    }

    setErrors(result.fieldErrors);
    setGeneralError(result.generalError ?? '');
    setValues((current) => ({ ...current, password: '' }));
  }

  return (
    <main className="register">
      <h1>Log ind</h1>

      {generalError && (
        <p role="alert" className="register-error">
          {generalError}
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate>
        {FIELDS.map(({ name, label, type, autoComplete }) => (
          <div className="register-field" key={name}>
            <label htmlFor={name}>{label}</label>
            <input
              id={name}
              name={name}
              type={type}
              autoComplete={autoComplete}
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
        <button type="submit" disabled={submitting}>
          {submitting ? 'Logger ind…' : 'Log ind'}
        </button>
      </form>
    </main>
  );
}
