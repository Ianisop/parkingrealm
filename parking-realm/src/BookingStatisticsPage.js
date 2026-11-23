import { useState } from 'react';
import { getBookingStatistics } from './api/booking';

export default function BookingStatisticsPage() {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [statistics, setStatistics] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function load(event) {
    event.preventDefault();
    setStatistics(null);
    setError('');
    if (!start || !end || !Number.isFinite(Date.parse(start)) || !Number.isFinite(Date.parse(end)) ||
        Date.parse(end) <= Date.parse(start)) {
      setError('Angiv et gyldigt tidsinterval.');
      return;
    }
    setLoading(true);
    const result = await getBookingStatistics(new Date(start).toISOString(), new Date(end).toISOString());
    setLoading(false);
    if (result.ok) setStatistics(result.data);
    else setError(result.error);
  }

  return (
    <section aria-label="Bookingstatistik">
      <h2>Bookingstatistik</h2>
      <form onSubmit={load}>
        <label htmlFor="statisticsStart">Fra</label>
        <input id="statisticsStart" type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} />
        <label htmlFor="statisticsEnd">Til</label>
        <input id="statisticsEnd" type="datetime-local" value={end} onChange={(event) => setEnd(event.target.value)} />
        <button type="submit" disabled={loading}>Hent statistik</button>
      </form>
      <p>Bookinger grupperes efter reservationens startdato i UTC.</p>
      {loading && <p role="status">Henter statistik…</p>}
      {error && <p role="alert">{error}</p>}
      {statistics && <>
        <p>Bookinger: {statistics.total}</p>
        <p>Aktive: {statistics.active}</p>
        <p>Annullerede: {statistics.cancelled}</p>
        {!statistics.hasData ? <p>Ingen bookingdata i perioden.</p> : <table>
          <caption>Bookinger pr. dag (UTC)</caption>
          <thead><tr><th>Dato</th><th>Bookinger</th><th>Aktive</th><th>Annullerede</th></tr></thead>
          <tbody>{statistics.days.map((day) => <tr key={day.day}>
            <td>{day.day.slice(0, 10)}</td><td>{day.total}</td><td>{day.active}</td><td>{day.cancelled}</td>
          </tr>)}</tbody>
        </table>}
      </>}
    </section>
  );
}
