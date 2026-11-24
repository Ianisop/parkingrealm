import { useCallback, useEffect, useState } from 'react';
import { deleteVehicle, getVehicles, registerVehicle } from './api/booking';
import './BookingPage.css';
import { Icon, PageHeader } from './ui';

export default function VehiclesPage() {
  const [vehicles, setVehicles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [electricChoice, setElectricChoice] = useState('');
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
    if (electricChoice === '') {
      setError('Angiv, om køretøjet er elektrisk.');
      return;
    }
    setBusy(true);
    const result = await registerVehicle(registrationNumber.trim(), electricChoice === 'true');
    setBusy(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setVehicles((current) => [...current, result.data]);
    setLoadError('');
    setRegistrationNumber('');
    setElectricChoice('');
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
      <PageHeader eyebrow="Din garage" title="Mine køretøjer" description="Tilføj dine køretøjer én gang, og vælg dem nemt, næste gang du booker." >
        {!loading && !loadError && <span className="badge">{vehicles.length} registrerede køretøjer</span>}
      </PageHeader>
      <div className="grid items-start gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
      <form className="panel grid gap-4" onSubmit={register}>
        <div className="mb-1"><span className="mb-4 inline-flex rounded-xl bg-brand-50 p-3 text-brand-600"><Icon name="car" /></span><h3>Tilføj et køretøj</h3><p className="mt-2">Indtast nummerpladen, og vælg køretøjstype.</p></div>
        <label htmlFor="vehicleRegistrationNumber">Registreringsnummer</label>
        <input id="vehicleRegistrationNumber" value={registrationNumber} maxLength={32}
          onChange={(event) => setRegistrationNumber(event.target.value)} />
        <label htmlFor="vehicleElectric">Er køretøjet elektrisk?</label>
        <select id="vehicleElectric" value={electricChoice} onChange={(event) => setElectricChoice(event.target.value)}>
          <option value="">Vælg køretøjstype</option>
          <option value="true">Ja – elbil</option>
          <option value="false">Nej – ikke elbil</option>
        </select>
        <button className="btn btn-primary mt-2" type="submit" disabled={busy}>Registrer køretøj</button>
      </form>
      <div className="grid gap-4">
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {loading && <p>Henter køretøjer…</p>}
      {loadError && <p role="alert">{loadError}</p>}
      {!loading && !loadError && vehicles.length === 0 && <div className="panel py-12 text-center"><Icon name="car" className="mx-auto mb-4 h-10 w-10 text-slate-300" /><h3>Din garage er klar</h3><p className="mt-2">Du har ingen registrerede køretøjer.</p></div>}
      <ul className="grid gap-4">
        {vehicles.map((vehicle) => <li className="panel flex flex-wrap items-center justify-between gap-5" key={vehicle.id}>
          <div className="flex items-center gap-4"><span className="rounded-xl bg-slate-100 p-3 text-slate-500"><Icon name="car" className="h-7 w-7" /></span><div><span className="inline-block rounded-lg border border-slate-300 bg-slate-50 px-3 py-1 font-mono text-lg font-bold tracking-wider">{vehicle.registrationNumber}</span><p className="mt-2">{vehicle.isElectric ? 'Elbil · ladepladser tilgængelige' : 'Standardkøretøj'}</p></div></div>
          <div className="flex flex-wrap gap-2">
          {confirmingId === vehicle.id ? <>
            {' '}<button className="btn btn-danger" type="button" disabled={busy} onClick={() => remove(vehicle)}>
              Bekræft sletning af {vehicle.registrationNumber}
            </button>
            <button className="btn" type="button" onClick={() => setConfirmingId(null)}>Annuller</button>
          </> : <>
            {' '}<button className="btn btn-danger" type="button" disabled={busy} onClick={() => setConfirmingId(vehicle.id)}>
              Slet {vehicle.registrationNumber}
            </button>
          </>}
          </div>
        </li>)}
      </ul>
      </div>
      </div>
    </section>
  );
}
