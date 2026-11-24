import { useCallback, useEffect, useState } from 'react';
import { createUser, deleteUser, getUsers, updateUser } from './api/booking';
import { NAME_MAX_LENGTH, validateRegistration } from './registerValidation';
import './BookingPage.css';
import { PageHeader } from './ui';

const EMPTY = { email: '', password: '', firstName: '', lastName: '', phoneNumber: '' };
// The password is not part of editing; this value only satisfies the shared validation.
const UNUSED_PASSWORD = 'Aa1!aaaa';

function messagesFrom(fieldErrors) {
  return Object.fromEntries(Object.entries(fieldErrors || {}).map(([key, value]) => [key, [].concat(value).join(' ')]));
}

export default function UsersPage() {
  const [users, setUsers] = useState([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [form, setForm] = useState(null); // null | { id: null | string, values }
  const [errors, setErrors] = useState({});
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [confirmingId, setConfirmingId] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async (term = '') => {
    setLoading(true);
    const result = await getUsers(term);
    setLoading(false);
    if (result.ok) {
      setUsers(result.data);
      setLoadError('');
    } else {
      setUsers([]);
      setLoadError(result.error);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  function openForm(user) {
    setErrors({});
    setError('');
    setMessage('');
    setConfirmingId(null);
    setForm(user
      ? { id: user.id, values: { ...EMPTY, email: user.email, firstName: user.firstName, lastName: user.lastName, phoneNumber: user.phoneNumber || '' } }
      : { id: null, values: EMPTY });
  }

  const setField = (key) => (event) => setForm((current) => ({ ...current, values: { ...current.values, [key]: event.target.value } }));

  async function submit(event) {
    event.preventDefault();
    if (busy) return;
    setError('');
    setMessage('');
    const { id, values } = form;
    const found = validateRegistration(id ? { ...values, password: UNUSED_PASSWORD } : values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const payload = {
      email: values.email.trim(),
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      phoneNumber: values.phoneNumber.trim(),
    };
    setBusy(true);
    const result = id ? await updateUser(id, payload) : await createUser({ ...payload, password: values.password });
    setBusy(false);
    if (!result.ok) {
      setErrors(messagesFrom(result.fieldErrors));
      setError(result.error);
      return;
    }
    setForm(null);
    setMessage(id ? `Brugeren ${result.data.email} er opdateret.` : `Brugeren ${result.data.email} er oprettet.`);
    load(search);
  }

  async function remove(user) {
    if (busy) return;
    setError('');
    setMessage('');
    setConfirmingId(null);
    setBusy(true);
    const result = await deleteUser(user.id);
    setBusy(false);
    if (result.ok) {
      setMessage(`Brugeren ${user.email} er slettet.`);
      if (form?.id === user.id) setForm(null);
    } else setError(result.error);
    load(search);
  }

  function runSearch(event) {
    event.preventDefault();
    load(search);
  }

  const field = (key, label, type = 'text') => (
    <div className="field">
      <label htmlFor={`user-${key}`}>{label}</label>
      <input id={`user-${key}`} type={type} value={form.values[key]} onChange={setField(key)}
        maxLength={key === 'firstName' || key === 'lastName' ? NAME_MAX_LENGTH : undefined}
        autoComplete={type === 'password' ? 'new-password' : 'off'} />
      {errors[key] && <p role="alert">{errors[key]}</p>}
    </div>
  );

  return (
    <section className="booking" aria-label="Brugeradministration">
      <PageHeader eyebrow="Administration" title="Brugere" description="Administrér profiler og kontaktoplysninger. Søg, opret eller opdatér brugere her.">
        <button className="btn btn-primary" type="button" onClick={() => openForm(null)}>Opret bruger</button>
      </PageHeader>
      <form className="panel grid items-end gap-3 sm:grid-cols-[1fr_auto]" onSubmit={runSearch} role="search">
        <div className="field">
        <label htmlFor="userSearch">Søg efter navn eller email</label>
        <input id="userSearch" value={search} onChange={(event) => setSearch(event.target.value)} />
        </div><button className="btn" type="submit">Søg</button>
      </form>

      {form && (
        <form className="panel grid gap-5 sm:grid-cols-2" onSubmit={submit} aria-label={form.id ? 'Rediger bruger' : 'Opret bruger'}>
          <h3 className="sm:col-span-2">{form.id ? 'Rediger bruger' : 'Opret bruger'}</h3>
          {field('email', 'Email', 'email')}
          {!form.id && field('password', 'Adgangskode', 'password')}
          {field('firstName', 'Fornavn')}
          {field('lastName', 'Efternavn')}
          {field('phoneNumber', 'Telefonnummer', 'tel')}
          {errors.registration && <p role="alert">{errors.registration}</p>}
          <div className="flex gap-3 sm:col-span-2"><button className="btn btn-primary" type="submit" disabled={busy}>{form.id ? 'Gem ændringer' : 'Opret'}</button>
          <button className="btn" type="button" onClick={() => setForm(null)}>Annuller</button></div>
        </form>
      )}

      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {loading && <p>Henter brugere…</p>}
      {loadError && <p role="alert">{loadError}</p>}
      {!loading && !loadError && users.length === 0 && <p className="panel py-12 text-center">Ingen brugere fundet.</p>}
      {users.length > 0 && (
        <div className="panel"><div className="table-scroll" tabIndex={0} role="region" aria-label="Brugeroversigt"><table>
          <caption>Brugere</caption>
          <thead>
            <tr><th>Navn</th><th>Email</th><th>Telefon</th><th>Rolle</th><th>Handlinger</th></tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td className="font-semibold text-slate-900">{user.firstName} {user.lastName}</td>
                <td>{user.email}</td>
                <td>{user.phoneNumber}</td>
                <td><span className="badge">{user.isAdministrator ? 'Administrator' : 'Bruger'}</span></td>
                <td>
                  {user.isAdministrator ? 'Kan ikke ændres her' : confirmingId === user.id ? (
                    <>
                      <button className="btn btn-danger" type="button" disabled={busy} onClick={() => remove(user)}>
                        Bekræft sletning af {user.email}
                      </button>
                      <button className="btn m-1" type="button" onClick={() => setConfirmingId(null)}>Annuller</button>
                    </>
                  ) : (
                    <>
                      <button className="btn m-1" type="button" disabled={busy} onClick={() => openForm(user)}>Rediger {user.email}</button>
                      <button className="btn btn-danger m-1" type="button" disabled={busy} onClick={() => { setConfirmingId(user.id); setMessage(''); setError(''); }}>
                        Slet {user.email}
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table></div></div>
      )}
    </section>
  );
}
