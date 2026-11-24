export function Icon({ name, className = 'h-5 w-5' }) {
  const paths = {
    parking: 'M8 18V6h5a4 4 0 0 1 0 8H8m0-8v8',
    car: 'm5 7 2-4h10l2 4m-15 0h16v10H4V7Zm1 10v3m14-3v3M7 11h2m6 0h2',
    chart: 'M4 20h16M7 16v-5m5 5V4m5 12V8',
    users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m20 0v-2a4 4 0 0 0-3-3.87M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm8-7a4 4 0 0 1 0 8',
    check: 'm5 12 4 4L19 6',
    arrow: 'M5 12h14m-6-6 6 6-6 6',
  };
  return <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name] || paths.parking} /></svg>;
}

export function Brand({ inverse = false }) {
  return <div className={`flex items-center gap-3 ${inverse ? 'text-white' : 'text-slate-900'}`}>
    <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-600 text-white shadow-lg shadow-blue-600/20"><Icon name="parking" className="h-7 w-7" /></span>
    <span className="text-xl font-bold tracking-tight">Parking<span className={inverse ? 'text-blue-300' : 'text-brand-600'}>Realm</span></span>
  </div>;
}

export function PageHeader({ eyebrow, title, description, children }) {
  return <header className="flex flex-wrap items-start justify-between gap-4">
    <div>{eyebrow && <p className="eyebrow mb-2">{eyebrow}</p>}<h2 className="page-heading">{title}</h2><p className="page-description">{description}</p></div>
    {children}
  </header>;
}
