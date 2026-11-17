import { useCallback, useEffect, useState } from 'react';
import { deleteVehicle, getVehicles, registerVehicle } from './api/booking';
import './BookingPage.css';

export default function VehiclesPage() {
  const [vehicles, setVehicles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [confirmingId, setConfirmingId] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    const result = await getVehicles();
    setLoading(false);
    if (result.ok) {
      setVehicles(result.data);
      setLoadError('');
    } else setLoadError(result.error);
  }, []);

  useEffect(() => { load(); }, [load]);

  async function register(event) {
    event.preventDefault();
    if (busy) return;
    setError('');
    setMessage('');
    if (!registrationNumber.trim()) {
      setError('Registreringsnummer mangler.');
      return;
    }
    setBusy(true);
    const result = await registerVehicle(registrationNumber.trim());
    setBusy(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setVehicles((current) => [...current, result.data]);
    setLoadError('');
    setRegistrationNumber('');
    setMessage(`Køretøj ${result.data.registrationNumber} er registreret.`);
  }

  async function remove(vehicle) {
    if (busy) return;
    setError('');
    setMessage('');
    setConfirmingId(null);
    setBusy(true);
    const result = await deleteVehicle(vehicle.id);
    setBusy(false);
    if (result.ok) {
      setVehicles((current) => current.filter((item) => item.id !== vehicle.id));
      setMessage(`Køretøj ${vehicle.registrationNumber} er slettet.`);
    } else {
      setError(result.error);
      load();
    }
  }

  return (
    <section className="booking" aria-label="Køretøjer">
      <h2>Mine køretøjer</h2>
      <form onSubmit={register}>
        <label htmlFor="vehicleRegistrationNumber">Registreringsnummer</label>
        <input id="vehicleRegistrationNumber" value={registrationNumber} maxLength={32}
          onChange={(event) => setRegistrationNumber(event.target.value)} />
        <button type="submit" disabled={busy}>Registrer køretøj</button>
      </form>
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {loading && <p>Henter køretøjer…</p>}
      {loadError && <p role="alert">{loadError}</p>}
      {!loading && !loadError && vehicles.length === 0 && <p>Du har ingen registrerede køretøjer.</p>}
      <ul>
        {vehicles.map((vehicle) => <li key={vehicle.id}>
          {vehicle.registrationNumber}
          {confirmingId === vehicle.id ? <>
            {' '}<button type="button" disabled={busy} onClick={() => remove(vehicle)}>
              Bekræft sletning af {vehicle.registrationNumber}
            </button>
            <button type="button" onClick={() => setConfirmingId(null)}>Annuller</button>
          </> : <>
            {' '}<button type="button" disabled={busy} onClick={() => setConfirmingId(vehicle.id)}>
              Slet {vehicle.registrationNumber}
            </button>
          </>}
        </li>)}
      </ul>
    </section>
  );
}
