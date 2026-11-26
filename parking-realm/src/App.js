import { useEffect, useState } from 'react';
import { getCurrentUser, logout, LOGOUT_ERROR } from './api/auth';
import LoginPage from './LoginPage';
import RegisterPage from './RegisterPage';
import BookingPage from './BookingPage';
import VehiclesPage from './VehiclesPage';
import BookingStatisticsPage from './BookingStatisticsPage';
import UsersPage from './UsersPage';
import ForgotPasswordPage from './ForgotPasswordPage';
import ResetPasswordPage from './ResetPasswordPage';
import { Brand, Icon } from './ui';
import './RegisterPage.css';

// A reset link opens the app with ?resetToken=...&email=...
function readResetParams() {
  const query = new URLSearchParams(window.location.search);
  const token = query.get('resetToken');
  const email = query.get('email');
  return token && email ? { token, email } : null;
}

function App() {
  const [user, setUser] = useState(null);
  const [checking, setChecking] = useState(true);
  const [view, setView] = useState('login');
  const [resetParams, setResetParams] = useState(readResetParams);
  const [notice, setNotice] = useState('');
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState('');

  function leaveResetPage(message = '') {
    window.history.replaceState(null, '', window.location.pathname);
    setResetParams(null);
    setView('login');
    setNotice(message);
  }

  async function handleLogout() {
    setLoggingOut(true);
    setLogoutError('');
    const result = await logout();
    setLoggingOut(false);
    if (result.ok) {
      setUser(null);
      setView('login');
    } else {
      setLogoutError(LOGOUT_ERROR);
    }
  }
  // Restore the login state from the session cookie, e.g. after a page reload.
  useEffect(() => {
    let active = true;
    getCurrentUser().then((currentUser) => {
      if (active) {
        setUser(currentUser);
        setChecking(false);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  if (checking) {
    return (
      <main className="flex min-h-screen flex-col items-center justify-center gap-8">
        <Brand /><p role="status" className="text-sm text-slate-500">Henter…</p>
      </main>
    );
  }

  if (user) {
    return (
      <div className="min-h-screen lg:grid lg:grid-cols-[16rem_minmax(0,1fr)]">
        <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-xl focus:bg-white focus:p-4">Gå til indhold</a>
        <aside className="border-b border-slate-200 bg-white p-4 lg:sticky lg:top-0 lg:flex lg:h-screen lg:flex-col lg:border-b-0 lg:border-r lg:p-6">
          <Brand />
          <p className="mb-3 mt-9 hidden px-4 text-[10px] font-bold uppercase tracking-[0.2em] text-slate-400 lg:block">Dit overblik</p>
          <nav aria-label="Navigation" className="mt-5 flex gap-2 overflow-x-auto lg:mt-0 lg:flex-col">
            {[
              ['booking', 'Book parkering', 'parking'], ['vehicles', 'Mine køretøjer', 'car'],
              ...(user.isAdministrator ? [['statistics', 'Bookingstatistik', 'chart'], ['users', 'Brugere', 'users']] : []),
            ].map(([key, label, icon]) => <button key={key} type="button" className="nav-item"
              aria-current={(view === key || (key === 'booking' && !['vehicles', 'statistics', 'users'].includes(view))) ? 'page' : undefined}
              onClick={() => setView(key)}><Icon name={icon} />{label}</button>)}
          </nav>
          <div className="mt-auto hidden rounded-2xl bg-slate-50 p-4 lg:block">
            <p className="text-sm font-semibold text-slate-800">Mindre søgen. Mere frihed.</p>
            <p className="mt-2 text-xs leading-5 text-slate-500">Planlæg din parkering, og få mere tid til resten af dagen.</p>
          </div>
        </aside>
        <div className="min-w-0">
          <header className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-200 bg-white/80 px-5 py-5 sm:px-8">
          <h1 className="text-sm font-medium text-slate-600">
          Velkommen, {user.firstName} {user.lastName}
          </h1>
          <div className="flex items-center gap-3"><span className="badge">{user.isAdministrator ? 'Administrator' : 'Min konto'}</span><button type="button" className="btn" onClick={handleLogout} disabled={loggingOut}>Log ud</button><span aria-hidden="true" className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-900 text-sm font-bold text-white">{user.firstName?.[0]}{user.lastName?.[0]}</span></div>
          </header>
          {logoutError && <p role="alert" className="mx-5 mt-4 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700 sm:mx-8">{logoutError}</p>}
          <main id="main-content" tabIndex={-1} className="mx-auto max-w-7xl p-4 sm:p-8 lg:p-10">
        {user.isAdministrator && view === 'statistics' ? <BookingStatisticsPage />
          : user.isAdministrator && view === 'users' ? <UsersPage />
          : view === 'vehicles' ? <VehiclesPage /> : <BookingPage />}
          </main>
          <footer className="px-6 pb-6 text-center text-xs text-slate-400">ParkingRealm · Parkering med overblik</footer>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-2">
      <aside className="relative hidden overflow-hidden bg-slate-950 p-12 text-white lg:flex lg:flex-col lg:justify-between xl:p-16">
        <div className="pointer-events-none absolute -right-32 top-20 h-96 w-96 rounded-full bg-blue-600/20 blur-3xl" aria-hidden="true" />
        <Brand inverse />
        <div className="relative max-w-lg py-16"><span className="rounded-full border border-white/20 px-4 py-2 text-xs font-medium text-blue-200">Din plads. Din plan.</span>
          <h2 className="mt-8 text-5xl font-bold leading-tight tracking-tight xl:text-6xl">En nemmere vej<br />til din næste<br /><span className="text-blue-400">parkeringsplads.</span></h2>
          <p className="mt-6 max-w-sm text-base leading-7 text-slate-400">Find en plads, der passer til dig. Book på få øjeblikke, og ankom med ro i maven.</p>
          <div className="mt-10 grid gap-4 text-sm text-slate-300">{['Book til i dag eller planlæg fremad', 'Vælg selv eller få en anbefalet plads', 'Dine køretøjer og bookinger samlet'].map((text) => <p key={text} className="flex items-center gap-3"><span className="rounded-full bg-blue-500/15 p-1 text-blue-300"><Icon name="check" className="h-4 w-4" /></span>{text}</p>)}</div>
        </div><p className="text-xs text-slate-500">ParkingRealm · Mere tid til det, der betyder noget</p>
      </aside>
      <div className="flex min-h-screen flex-col justify-center px-6 py-10 sm:px-12">
        <div className="mx-auto mb-12 w-full max-w-md lg:hidden"><Brand /></div>
        {resetParams ? (
          <ResetPasswordPage
            email={resetParams.email}
            token={resetParams.token}
            onDone={() => leaveResetPage('Din adgangskode er nulstillet. Log ind med din nye adgangskode.')}
            onBack={() => leaveResetPage()}
          />
        ) : view === 'forgot' ? <ForgotPasswordPage onBack={() => setView('login')} />
          : view === 'login' ? <LoginPage onLoggedIn={setUser} notice={notice} onForgotPassword={() => { setNotice(''); setView('forgot'); }} />
          : <RegisterPage />}
      {!resetParams && view !== 'forgot' && (
      <nav aria-label="Konto" className="mx-auto mt-8 w-full max-w-md border-t border-slate-200 pt-6 text-center">
        {view === 'login' ? (
          <button className="text-sm font-semibold text-brand-700 hover:text-brand-900" type="button" onClick={() => { setNotice(''); setView('register'); }}>
            Ny bruger? Opret bruger
          </button>
        ) : (
          <button className="text-sm font-semibold text-brand-700 hover:text-brand-900" type="button" onClick={() => setView('login')}>
            Har du en bruger? Log ind
          </button>
        )}
      </nav>
      )}
      </div>
    </div>
  );
}

export default App;
