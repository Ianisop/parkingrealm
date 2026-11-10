import { useEffect, useState } from 'react';
import { getSpaces, getVehicles, registerVehicle, getReservations, createReservation, cancelReservation } from './api/booking';
import './BookingPage.css';

export function validateCriteria(start, end) {
  if (!start || !end) return 'Angiv start og slut.';
  if (!Number.isFinite(Date.parse(start)) || !Number.isFinite(Date.parse(end)) ||
      Date.parse(start) <= Date.now() || Date.parse(end) <= Date.parse(start)) {
    return 'Start skal være i fremtiden, og slut skal være efter start.';
  }
  return '';
}

export default function BookingPage() {
  const [vehicles, setVehicles] = useState([]);
  const [vehicleId, setVehicleId] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [vehicleError, setVehicleError] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [mode, setMode] = useState('automatic');
  const [selectedId, setSelectedId] = useState(null);
  const [spaces, setSpaces] = useState([]);
  const [mapError, setMapError] = useState('');
  const [loading, setLoading] = useState(true);
  const [refresh, setRefresh] = useState(0);
  const [reservations, setReservations] = useState([]);
  const [reservationsError, setReservationsError] = useState('');
  const [reservationsLoading, setReservationsLoading] = useState(true);
  const [operationError, setOperationError] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    setReservationsLoading(true);
    setReservationsError('');
    getReservations().then((result) => {
      if (!active) return;
      setReservationsLoading(false);
      setReservations(result.ok ? result.data : []);
      if (!result.ok) setReservationsError(result.error);
    });
    return () => { active = false; };
  }, [refresh]);

  useEffect(() => {
    let active = true;
    getVehicles().then((result) => {
      if (!active) return;
      if (result.ok) {
        setVehicles(result.data);
        setVehicleId(String(result.data[0]?.id || ''));
      } else setVehicleError(result.error);
    });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    setSelectedId(null);
    setSpaces([]);
    const error = start || end ? validateCriteria(start, end) : '';
    setMapError(error);
    if (error) {
      setLoading(false);
      return;
    }
    setLoading(true);
    getSpaces(start ? new Date(start).toISOString() : null, end ? new Date(end).toISOString() : null)
      .then((result) => {
        if (!active) return;
        setLoading(false);
        if (result.ok) setSpaces(result.data);
        else setMapError(result.error);
      });
    return () => { active = false; };
  }, [start, end, refresh]);

  async function addVehicle(event) {
    event.preventDefault();
    setVehicleError('');
    if (!registrationNumber.trim()) {
      setVehicleError('Registreringsnummer mangler.');
      return;
    }
    const result = await registerVehicle(registrationNumber.trim());
    if (!result.ok) {
      setVehicleError(result.error);
      return;
    }
    setVehicles((current) => [...current, result.data]);
    setVehicleId(String(result.data.id));
    setRegistrationNumber('');
  }

  async function book() {
    setConfirmation('');
    const error = validateCriteria(start, end) || (!vehicleId ? 'Vælg et køretøj.' : '') ||
      (mode === 'manual' && !selectedId ? 'Vælg en ledig plads.' : '');
    setOperationError(error);
    if (error || busy) return;
    setBusy(true);
    const result = await createReservation({
      vehicleId: Number(vehicleId), start: new Date(start).toISOString(), end: new Date(end).toISOString(),
      mode, parkingSpaceId: mode === 'manual' ? selectedId : null,
    });
    setBusy(false);
    if (result.ok) setConfirmation(`Reservation ${result.data.id} bekræftet. Plads ${result.data.parkingSpaceId}.`);
    else setOperationError(result.error);
    setRefresh((current) => current + 1);
  }

  async function cancel(id) {
    if (busy) return;
    setConfirmation('');
    setOperationError('');
    setBusy(true);
    const result = await cancelReservation(id);
    setBusy(false);
    if (result.ok) {
      setConfirmation(`Reservation ${id} annulleret.`);
      setRefresh((current) => current + 1);
    } else setOperationError(result.error);
  }

  return (
    <section className="booking" aria-label="Booking">
      <h2>Book parkeringsplads</h2>
      <form onSubmit={addVehicle}>
        <label htmlFor="registrationNumber">Registreringsnummer</label>
        <input id="registrationNumber" value={registrationNumber} maxLength={32}
          onChange={(event) => setRegistrationNumber(event.target.value)} />
        <button type="submit">Registrer køretøj</button>
      </form>
      {vehicleError && <p role="alert">{vehicleError}</p>}
      <label htmlFor="vehicleId">Køretøj</label>
      <select id="vehicleId" value={vehicleId} onChange={(event) => setVehicleId(event.target.value)}>
        <option value="">Vælg køretøj</option>
        {vehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.registrationNumber}</option>)}
      </select>
      <label htmlFor="bookingStart">Start</label>
      <input id="bookingStart" type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} />
      <label htmlFor="bookingEnd">Slut</label>
      <input id="bookingEnd" type="datetime-local" value={end} onChange={(event) => setEnd(event.target.value)} />
      <fieldset>
        <legend>Tildeling</legend>
        <label><input type="radio" name="mode" checked={mode === 'automatic'}
          onChange={() => { setMode('automatic'); setSelectedId(null); }} />Automatisk</label>
        <label><input type="radio" name="mode" checked={mode === 'manual'}
          onChange={() => setMode('manual')} />Vælg selv</label>
      </fieldset>
      <button type="button" onClick={() => setRefresh((current) => current + 1)}>Opdater pladser</button>
      <p>Ledighed er baseret på reservationer, ikke fysisk belægning.</p>
      {loading && <p role="status">Henter pladser…</p>}
      {mapError && <p role="alert">{mapError}</p>}
      {!loading && !mapError && spaces.length === 0 && <p>Ingen pladser er konfigureret.</p>}
      {!loading && !mapError && spaces.length > 0 && !spaces.some((space) => space.isAvailable) && <p>Ingen ledige pladser.</p>}
      <div className="parking-map" aria-label="Parkeringsoversigt">
        {spaces.map((space) => <button key={space.id} type="button"
          style={{ gridRow: space.row + 1, gridColumn: space.column + 1 }}
          aria-pressed={selectedId === space.id}
          disabled={mode !== 'manual' || !space.isAvailable}
          onClick={() => setSelectedId(space.id)}>
          Plads {space.id}: {space.isAvailable ? 'Ledig' : 'Reserveret'}
        </button>)}
      </div>
      {selectedId && <p>Valgt plads: {selectedId}</p>}
      <button type="button" disabled={busy || loading || Boolean(mapError)} onClick={book}>Bekræft booking</button>
      {operationError && <p role="alert">{operationError}</p>}
      {confirmation && <p role="status">{confirmation}</p>}
      <h2>Mine reservationer</h2>
      {reservationsLoading && <p>Henter reservationer…</p>}
      {reservationsError && <p role="alert">{reservationsError}</p>}
      {!reservationsLoading && !reservationsError && reservations.length === 0 && <p>Ingen reservationer.</p>}
      <ul>
        {reservations.map((reservation) => <li key={reservation.id}>
          Reservation {reservation.id}, plads {reservation.parkingSpaceId}: {reservation.status === 'Active' ? 'Aktiv' : 'Annulleret'}.
          {' '}{new Date(reservation.start).toLocaleString('da-DK')} – {new Date(reservation.end).toLocaleString('da-DK')}
          {reservation.status === 'Active' && <button type="button" disabled={busy} onClick={() => cancel(reservation.id)}>
            Annuller reservation {reservation.id}
          </button>}
        </li>)}
      </ul>
    </section>
  );
}
