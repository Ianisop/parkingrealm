import { useState } from 'react';
import { getBookingStatistics } from './api/booking';
import { PageHeader } from './ui';

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
    <section className="page-content" aria-label="Bookingstatistik">
      <PageHeader eyebrow="Indsigt og overblik" title="Bookingstatistik" description="Forstå dine bookingmønstre. Vælg en periode for at se aktivitet og udvikling." />
      <form className="panel grid items-end gap-4 md:grid-cols-[1fr_1fr_auto]" onSubmit={load}>
        <div className="field">
        <label htmlFor="statisticsStart">Fra</label>
        <input id="statisticsStart" type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} />
        </div><div className="field">
        <label htmlFor="statisticsEnd">Til</label>
        <input id="statisticsEnd" type="datetime-local" value={end} onChange={(event) => setEnd(event.target.value)} />
        </div><button className="btn btn-primary" type="submit" disabled={loading}>Hent statistik</button>
      </form>
      <p className="text-sm text-slate-500">Bookinger grupperes efter reservationens startdato i UTC.</p>
      {loading && <p role="status">Henter statistik…</p>}
      {error && <p role="alert">{error}</p>}
      {statistics && <>
        <div className="grid gap-4 sm:grid-cols-3">
        <p className="metric text-xl font-bold tracking-tight text-slate-900">Bookinger: {statistics.total}</p>
        <p className="metric text-xl font-bold tracking-tight text-emerald-700">Aktive: {statistics.active}</p>
        <p className="metric text-xl font-bold tracking-tight text-slate-500">Annullerede: {statistics.cancelled}</p>
        </div>
        {!statistics.hasData ? <p className="panel py-12 text-center text-sm text-slate-500">Ingen bookingdata i perioden.</p> : <div className="panel"><div className="table-scroll" tabIndex={0} role="region" aria-label="Daglig bookingstatistik"><table>
          <caption>Bookinger pr. dag (UTC)</caption>
          <thead><tr><th>Dato</th><th>Bookinger</th><th>Aktive</th><th>Annullerede</th></tr></thead>
          <tbody>{statistics.days.map((day) => <tr key={day.day}>
            <td>{day.day.slice(0, 10)}</td><td>{day.total}</td><td>{day.active}</td><td>{day.cancelled}</td>
          </tr>)}</tbody>
        </table></div></div>}
      </>}
      {!statistics && !loading && !error && <div className="panel py-16 text-center"><p className="eyebrow mb-3">Et bedre beslutningsgrundlag</p><h3 className="text-lg font-semibold">Se historien bag dine bookinger</h3><p className="mt-2 text-sm text-slate-500">Vælg start og slut ovenfor for at hente din rapport.</p></div>}
    </section>
  );
}
