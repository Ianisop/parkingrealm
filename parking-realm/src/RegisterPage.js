import { useState } from 'react';
import { register } from './api/auth';
import { validateRegistration } from './registerValidation';
import './RegisterPage.css';

const FIELDS = [
  { name: 'firstName', label: 'Fornavn', type: 'text', autoComplete: 'given-name' },
  { name: 'lastName', label: 'Efternavn', type: 'text', autoComplete: 'family-name' },
  { name: 'email', label: 'Email', type: 'email', autoComplete: 'email' },
  { name: 'phoneNumber', label: 'Telefonnummer (med landekode, fx +4512345678)', type: 'tel', autoComplete: 'tel' },
  { name: 'password', label: 'Adgangskode', type: 'password', autoComplete: 'new-password' },
];

const EMPTY = { firstName: '', lastName: '', email: '', phoneNumber: '', password: '' };

export default function RegisterPage() {
  const [values, setValues] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [generalError, setGeneralError] = useState('');
  const [success, setSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  function handleChange(event) {
    const { name, value } = event.target;
    setValues((current) => ({ ...current, [name]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setSuccess(false);
    setGeneralError('');

    const clientErrors = validateRegistration(values);
    setErrors(clientErrors);
    if (Object.keys(clientErrors).length > 0) return;

    setSubmitting(true);
    const result = await register({
      ...values,
      email: values.email.trim(),
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      phoneNumber: values.phoneNumber.trim(),
    });
    setSubmitting(false);

    if (result.ok) {
      setValues(EMPTY);
      setErrors({});
      setSuccess(true);
      return;
    }

    // Errors for fields the form does not show are reported as a general error.
    const known = new Set(FIELDS.map((field) => field.name));
    const shown = Object.fromEntries(Object.entries(result.fieldErrors).filter(([name]) => known.has(name)));
    setErrors(shown);
    const hasUnshown = Object.keys(result.fieldErrors).length > Object.keys(shown).length;
    setGeneralError(result.generalError ?? (hasUnshown ? 'Oprettelsen mislykkedes. Tjek oplysningerne og prøv igen.' : ''));
  }

  return (
    <main className="register">
      <h1>Opret bruger</h1>

      {success && (
        <p role="status" className="register-success">
          Din profil er oprettet.
        </p>
      )}
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
          {submitting ? 'Opretter…' : 'Opret bruger'}
        </button>
      </form>
    </main>
  );
}
