import { useEffect, useRef, useState } from 'react';
import { bookingRequest } from './api/booking';
import './BookingPage.css';

const dateValue = (date) => date.toISOString().slice(0, 10);

export default function AdminDashboard() {
  const [start, setStart] = useState(() => dateValue(new Date(Date.now() - 29 * 86400000)));
  const [end, setEnd] = useState(() => dateValue(new Date()));
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [writeError, setWriteError] = useState('');
  const [occupied, setOccupied] = useState('');
  const [busy, setBusy] = useState(false);
  const writing = useRef(false);
  const [refresh, setRefresh] = useState(0);
  const invalid = !start || !end || start > end || Date.parse(end) - Date.parse(start) >= 366 * 86400000;

  useEffect(() => {
    let active = true;
    let fetching = false;
    setData(null);
    setError('');
    if (invalid) return undefined;
    async function load() {
      if (fetching) return;
      fetching = true;
      const query = new URLSearchParams({
        start: new Date(`${start}T00:00:00Z`).toISOString(),
        end: new Date(Date.parse(`${end}T00:00:00Z`) + 86400000).toISOString(),
      });
      const result = await bookingRequest(`/api/admin/dashboard?${query}`);
      fetching = false;
      if (!active) return;
      setData(result.ok ? result.data : null);
      setError(result.ok ? '' : result.error);
    }
    load();
    const timer = setInterval(load, 15000);
    return () => { active = false; clearInterval(timer); };
  }, [start, end, invalid, refresh]);

  async function record(path, body) {
    if (writing.current) return;
    writing.current = true;
    setBusy(true);
    setWriteError('');
    const result = await bookingRequest(`/api/admin/dashboard/${path}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    });
    writing.current = false;
    setBusy(false);
    if (result.ok) {
      setOccupied('');
      setRefresh((value) => value + 1);
    } else setWriteError(result.error);
  }

  return <section className="booking" aria-label="Administratordashboard">
    <h2>Administratordashboard</h2>
    <p>Status opdateres hvert 15. sekund. Datoer og bookingmønstre vises i UTC.</p>
    <label htmlFor="reportStart">Fra dato (UTC)</label>
    <input id="reportStart" type="date" value={start} onChange={(e) => setStart(e.target.value)} />
    <label htmlFor="reportEnd">Til dato (UTC, inklusive)</label>
    <input id="reportEnd" type="date" value={end} onChange={(e) => setEnd(e.target.value)} />
    {invalid && <p role="alert">Vælg et gyldigt interval på højst 366 dage.</p>}
    <button onClick={() => setRefresh((value) => value + 1)}>Opdater dashboard</button>
    {error && <p role="alert">Dashboarddata kunne ikke vises. {error}</p>}
    {writeError && <p role="alert">Registreringen mislykkedes. {writeError}</p>}
    {!data && !error && !invalid && <p role="status">Henter dashboard…</p>}
    {data && <>
      <section aria-label="Parkeringshusets status">
        <h3>Aktuel status · UC12</h3>
        <p>Kapacitet: {data.status.capacity} pladser. Aktive reservationer nu: {data.status.reserved}.</p>
        {data.status.hasData
          ? <p>Observeret belægning: {data.status.occupied}. Ledige: {data.status.available}.</p>
          : <p>Aktuel fysisk belægning er ukendt. Der mangler en observation fra de seneste fem minutter med den aktuelle kapacitet.</p>}
        {data.status.observedAt && <p>Seneste observation: {new Date(data.status.observedAt).toLocaleString()}.</p>}
        <form onSubmit={(e) => { e.preventDefault(); record('occupancy', { occupied: Number(occupied) }); }}>
          <label htmlFor="occupiedCount">Observeret antal optagede pladser</label>
          <input id="occupiedCount" type="number" min="0" max={data.status.capacity} required value={occupied}
            onChange={(e) => setOccupied(e.target.value)} disabled={busy} />
          <button disabled={busy || occupied === '' || data.status.capacity === 0}>Gem observation</button>
        </form>
        <p>Registrér kun faktisk observerede tal. Reservationer er ikke fysisk fremmøde.</p>
      </section>
      <section aria-label="Bookingmønstre">
        <h3>Bookingstatistik · UC13</h3>
        {!data.bookings.hasData ? <p>Ingen bookingdata i perioden; mønstre kan ikke beregnes.</p> : <>
          <p>Bookinger: {data.bookings.total}. Aktive: {data.bookings.active}. Annullerede: {data.bookings.cancelled}.</p>
          <table><caption>Bookinger pr. startdato (UTC)</caption><thead><tr><th>Dato</th><th>Antal</th></tr></thead>
            <tbody>{data.bookings.days.map((d) => <tr key={d.day}><td>{d.day.slice(0, 10)}</td><td>{d.count}</td></tr>)}</tbody></table>
          <ul aria-label="Bookinger pr. starttime">{data.bookings.hours.map((h) => <li key={h.hour}>Kl. {h.hour}: {h.count} bookinger</li>)}</ul>
        </>}
      </section>
      <section aria-label="Belægningshistorik">
        <h3>Belægningsgrad · UC14</h3>
        <p>Dagligt gennemsnit af registrerede stikprøver, ikke kontinuerligt målt belægning. Historikken slettes ikke automatisk og bevares mindst 12 måneder. Dage uden observationer er ukendte.</p>
        {!data.occupancy.hasData ? <p>Ingen belægningsdata i perioden.</p> : <table>
          <caption>Observeret belægning over tid</caption><thead><tr><th>Dato (UTC)</th><th>Gennemsnit</th><th>Observationer</th></tr></thead>
          <tbody>{data.occupancy.days.map((d) => <tr key={d.day}><td>{d.day.slice(0, 10)}</td><td>{d.averagePercent.toFixed(1)} %</td><td>{d.samples}</td></tr>)}</tbody>
        </table>}
      </section>
      <section aria-label="Fremmødestatistik">
        <h3>Fremmøde · UC15</h3>
        <p>Afsluttede, ikke-annullerede bookinger: {data.attendance.eligible}. Fremmødt: {data.attendance.attended}. Registreret udeblivelse: {data.attendance.noShows}. Ukendt: {data.attendance.unknown}.</p>
        {data.attendance.hasData ? <p>Fremmøde blandt kendte resultater: {data.attendance.percent.toFixed(1)} %.</p>
          : <p>Fremmøde kan ikke beregnes: ingen registrerede fremmødedata.</p>}
        <p>Manglende fremmødedata tælles aldrig som udeblivelse. De seneste 100 afsluttede bookinger uden registrering vises nedenfor, uafhængigt af rapportperioden.</p>
        <ul>{data.attendance.pending.map((r) => <li key={r.id}>
          Reservation {r.id}, plads {r.parkingSpaceId}, afsluttet {new Date(r.end).toLocaleString()}:
          <button disabled={busy} onClick={() => record('attendance', { reservationId: r.id, attended: true })}>Fremmødt · {r.id}</button>
          <button disabled={busy} onClick={() => record('attendance', { reservationId: r.id, attended: false })}>Udeblevet · {r.id}</button>
        </li>)}</ul>
      </section>
      <section aria-label="Demoindtjening">
        <h3>Daglig indtjening · UC16 · DEMODATA</h3>
        <p>Fiktive eksempelbeløb — ikke faktisk eller dokumenteret indtjening. Ingen betalingsintegration eller beregning fra bookinger.</p>
        <p>Eksempeldato (UTC): {data.revenue.day.slice(0, 10)}. Rapportfilteret ændrer ikke dagens demo.</p>
        <p>Aktuel indtjening (fiktivt eksempel): {data.revenue.actualExample} DKK.</p>
        <p>Forventet indtjening (fiktivt eksempel): {data.revenue.expectedExample} DKK.</p>
        <p>Faktiske aktive bookinger med start i dag: {data.revenue.todayBookings}. Dette tal bruges ikke til eksempelbeløbene.</p>
      </section>
    </>}
  </section>;
}
