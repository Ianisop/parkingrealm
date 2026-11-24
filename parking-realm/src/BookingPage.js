import { useEffect, useRef, useState } from 'react';
import { getSpaces, getVehicles, registerVehicle, getReservations, createReservation, cancelReservation } from './api/booking';
import { endForDuration, getBookingDefaults, toLocalInputValue, validateCriteria } from './bookingCriteria';
import { matchesRequirements, recommendSpace } from './parkingRecommendation';
import './BookingPage.css';
import { PageHeader } from './ui';

export { validateCriteria } from './bookingCriteria';

export default function BookingPage() {
  const [vehicles, setVehicles] = useState([]);
  const [vehicleId, setVehicleId] = useState('');
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [electricChoice, setElectricChoice] = useState('');
  const [vehicleError, setVehicleError] = useState('');
  const [vehicleBusy, setVehicleBusy] = useState(false);
  const [vehiclesLoading, setVehiclesLoading] = useState(true);
  const vehicleRequest = useRef(false);
  const operationRequest = useRef(false);
  const [defaults] = useState(() => getBookingDefaults());
  const [start, setStart] = useState(defaults.start);
  const [end, setEnd] = useState(defaults.end);
  const [durationHours, setDurationHours] = useState(1);
  const [now, setNow] = useState(Date.now);
  const [mode, setMode] = useState('automatic');
  const [preferredSpaceId, setPreferredSpaceId] = useState('');
  const [requirements, setRequirements] = useState({ requiresAccessible: false, requiresCharger: false });
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
  const criteriaError = validateCriteria(start, end, now);
  const availableCount = spaces.filter((space) => space.isAvailable).length;
  const selectedVehicle = vehicles.find((vehicle) => String(vehicle.id) === vehicleId);
  const eligibility = { ...requirements, isElectricVehicle: Boolean(selectedVehicle?.isElectric) };
  const matchingAvailableCount = spaces.filter((space) => space.isAvailable && matchesRequirements(space, eligibility)).length;
  const preferenceError = mode === 'automatic' && preferredSpaceId && !loading && !mapError && !criteriaError &&
    !spaces.some((space) => String(space.id) === preferredSpaceId)
    ? 'Den foretrukne placering findes ikke længere. Vælg en anden placering eller ingen præference.' : '';
  const recommendation = mode === 'automatic' && !loading && !mapError && !criteriaError
    ? recommendSpace(spaces, preferredSpaceId, eligibility) : null;
  const canBook = !busy && !vehicleBusy && !vehiclesLoading && Boolean(selectedVehicle) &&
    !loading && !mapError && !criteriaError && !preferenceError && matchingAvailableCount > 0 &&
    (mode === 'automatic' || spaces.some((space) => space.id === selectedId && space.isAvailable && matchesRequirements(space, eligibility)));

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30000);
    return () => clearInterval(timer);
  }, []);

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
      setVehiclesLoading(false);
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
    setMapError('');
    if (criteriaError) {
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
  }, [start, end, refresh, criteriaError]);

  function selectVehicle(value, availableVehicles = vehicles) {
    const vehicle = availableVehicles.find((item) => String(item.id) === value);
    setVehicleId(value);
    setSelectedId(null);
    if (!vehicle?.isElectric) setRequirements((current) => ({ ...current, requiresCharger: false }));
    setOperationError('');
  }

  function changeRequirement(name, checked) {
    setRequirements((current) => ({ ...current, [name]: checked }));
    setSelectedId(null);
    setOperationError('');
  }

  function changeStart(value) {
    const hours = (Date.parse(end) - Date.parse(start)) / 3600000;
    setStart(value);
    setEnd(endForDuration(value, hours > 0 ? hours : durationHours || 1));
    setOperationError('');
  }

  function chooseDuration(hours) {
    setDurationHours(hours);
    setEnd(endForDuration(start, hours));
    setOperationError('');
  }

  function bookToday() {
    const next = getBookingDefaults(Date.now(), durationHours || 1);
    setNow(Date.now());
    setStart(next.start);
    setEnd(next.end);
    setDurationHours(durationHours || 1);
    setOperationError('');
  }

  async function addVehicle(event) {
    event.preventDefault();
    if (vehicleRequest.current || vehiclesLoading || operationRequest.current) return;
    setVehicleError('');
    if (!registrationNumber.trim()) {
      setVehicleError('Registreringsnummer mangler.');
      return;
    }
    if (electricChoice === '') {
      setVehicleError('Angiv, om køretøjet er elektrisk.');
      return;
    }
    vehicleRequest.current = true;
    setVehicleBusy(true);
    const result = await registerVehicle(registrationNumber.trim(), electricChoice === 'true');
    vehicleRequest.current = false;
    setVehicleBusy(false);
    if (!result.ok) {
      setVehicleError(result.error);
      return;
    }
    setVehicles((current) => [...current, result.data]);
    selectVehicle(String(result.data.id), [...vehicles, result.data]);
    setRegistrationNumber('');
    setElectricChoice('');
  }

  async function book() {
    if (operationRequest.current || vehicleRequest.current) return;
    setConfirmation('');
    const error = validateCriteria(start, end) || preferenceError || (!vehicleId ? 'Vælg et køretøj.' : '') ||
      (mode === 'manual' && !selectedId ? 'Vælg en ledig plads.' : '');
    setOperationError(error);
    if (error || !canBook) return;
    operationRequest.current = true;
    setBusy(true);
    const result = await createReservation({
      vehicleId: Number(vehicleId), start: new Date(start).toISOString(), end: new Date(end).toISOString(),
      mode, parkingSpaceId: mode === 'manual' ? selectedId : null,
      ...requirements,
      ...(mode === 'automatic' && preferredSpaceId ? { preferredSpaceId: Number(preferredSpaceId) } : {}),
    });
    operationRequest.current = false;
    setBusy(false);
    if (result.ok) setConfirmation(`Reservation ${result.data.id} bekræftet. Plads ${result.data.parkingSpaceId}. ${new Date(result.data.start).toLocaleString('da-DK')} – ${new Date(result.data.end).toLocaleString('da-DK')}.`);
    else setOperationError(result.error);
    setRefresh((current) => current + 1);
  }

  async function cancel(id) {
    if (operationRequest.current || vehicleRequest.current) return;
    setConfirmation('');
    setOperationError('');
    operationRequest.current = true;
    setBusy(true);
    const result = await cancelReservation(id);
    operationRequest.current = false;
    setBusy(false);
    if (result.ok) {
      setConfirmation(`Reservation ${id} annulleret.`);
      setRefresh((current) => current + 1);
    } else setOperationError(result.error);
  }

  return (
    <section className="booking" aria-label="Booking">
      <PageHeader eyebrow="Gør plads til din dag" title="Book parkeringsplads" description="Book til i dag eller vælg en anden dato. Alle tidspunkter vises i din lokale tidszone." />
      <div className="grid items-start gap-6 xl:grid-cols-2">
      <section className="panel grid gap-4" aria-label="Køretøj og behov">
      <h3><span className="badge mr-3">01</span>Køretøj og behov</h3>
      <form className="grid gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4" onSubmit={addVehicle}>
        <p className="font-semibold text-slate-700">Tilføj et køretøj</p>
        <label htmlFor="registrationNumber">Registreringsnummer</label>
        <input id="registrationNumber" value={registrationNumber} maxLength={32} disabled={vehicleBusy || busy || vehiclesLoading}
          onChange={(event) => setRegistrationNumber(event.target.value)} />
        <label htmlFor="vehicleElectric">Er køretøjet elektrisk?</label>
        <select id="vehicleElectric" value={electricChoice} required disabled={vehicleBusy || busy || vehiclesLoading}
          onChange={(event) => setElectricChoice(event.target.value)}>
          <option value="">Vælg køretøjstype</option>
          <option value="true">Ja – elbil</option>
          <option value="false">Nej – ikke elbil</option>
        </select>
        <button className="btn" type="submit" disabled={vehicleBusy || busy || vehiclesLoading}>
          {vehicleBusy ? 'Registrerer…' : 'Registrer køretøj'}
        </button>
      </form>
      {vehiclesLoading && <p role="status">Henter køretøjer…</p>}
      {!vehiclesLoading && !vehicleError && vehicles.length === 0 && <p>Registrer dit køretøj ovenfor for at fortsætte. Det bliver valgt automatisk.</p>}
      {vehicleError && <p role="alert">{vehicleError}</p>}
      <label htmlFor="vehicleId">Køretøj</label>
      <select id="vehicleId" value={vehicleId} disabled={busy || vehicleBusy || vehiclesLoading}
        onChange={(event) => selectVehicle(event.target.value)}>
        <option value="">Vælg køretøj</option>
        {vehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.registrationNumber}</option>)}
      </select>
      {selectedVehicle && <p>{selectedVehicle.isElectric
        ? 'Elbil valgt: standardpladser og ladepladser er automatisk tilgængelige. Kræv en el-lader nedenfor, hvis du vil oplade.'
        : 'Ikke-elbil valgt: ladepladser er kun tilgængelige for elbiler.'}</p>}
      <fieldset className="booking-requirements" disabled={busy} aria-describedby="parkingRequirementsHelp">
        <legend>Krav til parkeringsplads</legend>
        <label><input id="requiresAccessible" type="checkbox" checked={requirements.requiresAccessible}
          aria-describedby="accessibleParkingHelp"
          onChange={(event) => changeRequirement('requiresAccessible', event.target.checked)} />Jeg har brug for en handicapplads</label>
        <p id="accessibleParkingHelp">Sæt kryds for at aktivere handicappladser i oversigten og ved automatisk tildeling. Det gælder både elbiler og andre køretøjer.</p>
        <label><input type="checkbox" checked={requirements.requiresCharger} disabled={!selectedVehicle?.isElectric}
          onChange={(event) => changeRequirement('requiresCharger', event.target.checked)} />Jeg har brug for en el-lader</label>
      </fieldset>
      <p id="parkingRequirementsHelp">Handicappladser kræver det tilsvarende behov, og ladepladser kræver en elbil.
        Vælger du begge krav, skal samme plads opfylde begge. Elbiler kan også vælge en standardplads uden opladning.</p>
      </section>
      <section className="panel grid gap-4" aria-label="Tidspunkt">
      <div className="flex flex-wrap items-center justify-between gap-3"><h3><span className="badge mr-3">02</span>Hvornår parkerer du?</h3>
      <button className="btn" type="button" disabled={busy} onClick={bookToday}>Book i dag</button></div>
      <label htmlFor="bookingStart">Start</label>
      <input id="bookingStart" type="datetime-local" value={start} disabled={busy}
        min={toLocalInputValue(new Date(now + 60000))} aria-describedby="bookingTimeHelp"
        aria-invalid={Boolean(criteriaError)} onChange={(event) => changeStart(event.target.value)} />
      <fieldset className="booking-shortcuts" disabled={busy}>
        <legend>Varighed</legend>
        {[1, 2, 4].map((hours) => <button className="btn" key={hours} type="button" aria-pressed={durationHours === hours}
          onClick={() => chooseDuration(hours)}>{hours} {hours === 1 ? 'time' : 'timer'}</button>)}
      </fieldset>
      <label htmlFor="bookingEnd">Slut</label>
      <input id="bookingEnd" type="datetime-local" value={end} disabled={busy}
        min={start ? endForDuration(start, 1 / 60) : undefined} aria-describedby="bookingTimeHelp"
        aria-invalid={Boolean(criteriaError)} onChange={(event) => {
          setEnd(event.target.value);
          setDurationHours(null);
          setOperationError('');
        }} />
      <p id="bookingTimeHelp">Start skal være efter nu. Du kan også booke senere i dag. Sluttiden følger varigheden, når du ændrer start. Tæt på midnat vælges næste tidspunkt i morgen.</p>
      {criteriaError && <p role="alert">{criteriaError}</p>}
      </section>
      </div>
      <section className="panel grid gap-5" aria-label="Vælg parkeringsplads">
      <div className="flex flex-wrap items-center justify-between gap-3"><h3><span className="badge mr-3">03</span>Find din plads</h3><span className="badge">{mode === 'automatic' ? 'Automatisk anbefaling' : 'Vælg din favorit'}</span></div>
      <fieldset className="flex flex-wrap gap-4 rounded-xl bg-slate-50 p-4" disabled={busy}>
        <legend>Tildeling</legend>
        <label className="flex items-center gap-2"><input type="radio" name="mode" checked={mode === 'automatic'}
          onChange={() => { setMode('automatic'); setSelectedId(null); setOperationError(''); }} />Automatisk</label>
        <label className="flex items-center gap-2"><input type="radio" name="mode" checked={mode === 'manual'}
          onChange={() => { setMode('manual'); setOperationError(''); }} />Vælg selv</label>
      </fieldset>
      {mode === 'automatic' && <>
        <label htmlFor="preferredSpaceId">Foretrukken placering</label>
        <select id="preferredSpaceId" value={preferredSpaceId} disabled={busy || loading}
          aria-describedby="parkingPreferenceHelp" aria-invalid={Boolean(preferenceError)}
          onChange={(event) => { setPreferredSpaceId(event.target.value); setOperationError(''); }}>
          <option value="">Ingen præference – første passende ledige plads</option>
          {spaces.map((space) => <option key={space.id} value={space.id}>
            Nær plads {space.id} (række {space.row + 1}, kolonne {space.column + 1})
          </option>)}
        </select>
        <p id="parkingPreferenceHelp">Vælg området, du vil parkere tæt på. En reserveret plads kan også bruges som placering.
          Afstanden beregnes som række- og kolonneforskel i oversigten, ikke faktisk gangafstand.</p>
        {preferenceError && <p role="alert">{preferenceError}</p>}
      </>}
      <button className="btn justify-self-start" type="button" disabled={busy} onClick={() => setRefresh((current) => current + 1)}>Opdater pladser</button>
      <p>Ledighed er baseret på reservationer, ikke fysisk belægning.</p>
      {loading && <p role="status">Henter pladser…</p>}
      {mapError && <p role="alert">{mapError}</p>}
      {!loading && !mapError && !criteriaError && spaces.length === 0 && <p>Ingen pladser er konfigureret.</p>}
      {!loading && !mapError && !criteriaError && spaces.length > 0 && !spaces.some((space) => space.isAvailable) && <p>Ingen ledige pladser. Vælg et andet tidsrum eller opdater pladser.</p>}
      {!loading && !mapError && !criteriaError && spaces.length > 0 && <p>{availableCount} af {spaces.length} pladser er ledige i det valgte tidsrum.</p>}
      {!loading && !mapError && !criteriaError && spaces.length > 0 && <p>{matchingAvailableCount} ledige pladser opfylder dine krav.</p>}
      {!loading && !mapError && !criteriaError && spaces.length > 0 && availableCount > 0 && matchingAvailableCount === 0 &&
        <p role="status">Ingen ledige pladser opfylder alle dine krav. Vælg et andet tidsrum. Der bookes ikke en plads uden de valgte faciliteter.</p>}
      {!requirements.requiresAccessible && spaces.some((space) => space.isAccessible && space.isAvailable) &&
        <p id="accessibleSpaceHint">Vil du booke en handicapplads? Sæt kryds i <a href="#requiresAccessible">Jeg har brug for en handicapplads</a> ovenfor.</p>}
      <div className="flex flex-wrap gap-4 text-xs text-slate-600" aria-label="Pladsforklaring">
        <span className="flex items-center gap-2"><span className="h-3 w-3 rounded border border-emerald-400 bg-white" />Ledig</span>
        <span className="flex items-center gap-2"><span className="h-3 w-3 rounded bg-slate-300" />Reserveret</span>
        <span className="flex items-center gap-2"><span className="h-3 w-3 rounded bg-brand-600" />Valgt / anbefalet</span>
        <span className="flex items-center gap-2"><span className="h-3 w-3 rounded border border-dashed border-slate-400" />Matcher ikke dine krav</span>
      </div>
      <div className="parking-map" aria-label="Parkeringsoversigt" tabIndex={0}>
        {spaces.map((space) => <button key={space.id} type="button"
          style={{ gridRow: space.row + 1, gridColumn: space.column + 1 }}
          aria-pressed={selectedId === space.id}
          data-recommended={recommendation?.space.id === space.id}
          data-eligible={matchesRequirements(space, eligibility)}
          data-available={space.isAvailable}
          aria-describedby={[
            recommendation?.space.id === space.id && 'parkingRecommendation',
            space.isAccessible && space.isAvailable && !requirements.requiresAccessible && 'accessibleSpaceHint',
          ].filter(Boolean).join(' ') || undefined}
          disabled={busy || mode !== 'manual' || !space.isAvailable || !matchesRequirements(space, eligibility)}
          onClick={() => setSelectedId(space.id)}>
          Plads {space.id}: {space.isAvailable ? 'Ledig' : 'Reserveret'}
          {space.isAccessible && ' · Handicapplads'}{space.hasCharger && ' · El-lader'}
        </button>)}
      </div>
      {selectedId && <p>Valgt plads: {selectedId}</p>}
      </section>
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_18rem] lg:items-center">
      {!criteriaError && <section className="booking-summary" aria-label="Bookingoversigt">
        <h3>Din booking</h3>
        <p>Køretøj: {selectedVehicle?.registrationNumber || 'Vælg eller registrer et køretøj'}</p>
        {selectedVehicle && <p>Køretøjstype: {selectedVehicle.isElectric ? 'Elbil' : 'Ikke elbil'}</p>}
        <p>{new Date(start).toLocaleString('da-DK')} – {new Date(end).toLocaleString('da-DK')}</p>
        <p>Varighed: {Math.round((Date.parse(end) - Date.parse(start)) / 60000)} minutter</p>
        <p>Krav: {[
          requirements.requiresAccessible && 'Handicapplads', requirements.requiresCharger && 'El-lader',
        ].filter(Boolean).join(' og ') || (selectedVehicle?.isElectric ? 'Standardplads eller ladeplads' : 'Standardplads')}</p>
        <p>{mode === 'automatic' ? 'Den bedst rangerede ledige plads tildeles automatisk.' : selectedId ? `Plads ${selectedId}` : 'Vælg en ledig plads i oversigten.'}</p>
        {recommendation && <p id="parkingRecommendation" role="status">
          Anbefalet plads: {recommendation.space.id}.
          {recommendation.distance !== null && ` Afstand i oversigten: ${recommendation.distance}.`}
          {' '}Ved samme afstand vælges laveste pladsnummer. Pladsen kontrolleres igen ved booking og kan ændre sig.
        </p>}
      </section>}
      <div className="grid gap-3"><button className="btn btn-primary min-h-14 text-base" type="button" disabled={!canBook} onClick={book}>{busy ? 'Behandler…' : 'Bekræft booking'}</button><p className="text-center text-xs">Pladsen kontrolleres igen, når du bekræfter.</p></div>
      </div>
      {operationError && <p role="alert">{operationError}</p>}
      {confirmation && <p role="status">{confirmation}</p>}
      <section className="panel grid gap-5" aria-label="Dine reservationer">
      <h2>Mine reservationer</h2>
      {reservationsLoading && <p>Henter reservationer…</p>}
      {reservationsError && <p role="alert">{reservationsError}</p>}
      {!reservationsLoading && !reservationsError && reservations.length === 0 && <p>Ingen reservationer.</p>}
      <ul className="grid gap-3">
        {reservations.map((reservation) => <li className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-slate-200 p-4 text-sm leading-7 text-slate-600" key={reservation.id}>
          <div>
          Reservation {reservation.id}, plads {reservation.parkingSpaceId}: {reservation.status === 'Active' ? 'Aktiv' : 'Annulleret'}.
          {' '}{new Date(reservation.start).toLocaleString('da-DK')} – {new Date(reservation.end).toLocaleString('da-DK')}
          </div>
          {reservation.status === 'Active' && <button className="btn btn-danger" type="button" disabled={busy || vehicleBusy} onClick={() => cancel(reservation.id)}>
            Annuller reservation {reservation.id}
          </button>}
        </li>)}
      </ul>
      </section>
    </section>
  );
}
