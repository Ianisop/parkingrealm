import { useEffect, useState } from 'react';
import { getCurrentUser } from './api/auth';
import LoginPage from './LoginPage';
import RegisterPage from './RegisterPage';
import './RegisterPage.css';

function App() {
  const [user, setUser] = useState(null);
  const [checking, setChecking] = useState(true);
  const [view, setView] = useState('login');

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
      <main className="register">
        <p>Henter…</p>
      </main>
    );
  }

  if (user) {
    return (
      <main className="register">
        <h1>
          Velkommen, {user.firstName} {user.lastName}
        </h1>
      </main>
    );
  }

  return (
    <>
      {view === 'login' ? <LoginPage onLoggedIn={setUser} /> : <RegisterPage />}
      <nav className="register">
        {view === 'login' ? (
          <button type="button" onClick={() => setView('register')}>
            Ny bruger? Opret bruger
          </button>
        ) : (
          <button type="button" onClick={() => setView('login')}>
            Har du en bruger? Log ind
          </button>
        )}
      </nav>
    </>
  );
}

export default App;
