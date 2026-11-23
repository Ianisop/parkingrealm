const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5198';

export const GENERAL_ERROR = 'Oprettelsen mislykkedes. Prøv igen senere.';
export const LOGIN_INVALID = 'Email eller adgangskode er forkert.';
export const LOGIN_GENERAL_ERROR = 'Login mislykkedes. Prøv igen senere.';

// The API answers with English messages; the UI shows Danish ones.
function translateServerError(field, message) {
  if (field === 'email' && /already in use/i.test(message)) return 'Email er allerede i brug.';
  if (field === 'password') return 'Adgangskoden opfylder ikke kravene.';
  return 'Værdien er ugyldig.';
}

export async function register({ email, password, firstName, lastName, phoneNumber }) {
  let response;
  try {
    response = await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, firstName, lastName, phoneNumber }),
    });
  } catch {
    return { ok: false, fieldErrors: {}, generalError: GENERAL_ERROR };
  }

  if (response.ok) return { ok: true, fieldErrors: {} };

  if (response.status === 400) {
    const fieldErrors = {};
    try {
      const body = await response.json();
      for (const [field, messages] of Object.entries(body.errors ?? {})) {
        fieldErrors[field] = translateServerError(field, messages[0] ?? '');
      }
    } catch {
      // fall through to the general error below
    }
    if (Object.keys(fieldErrors).length > 0) return { ok: false, fieldErrors };
  }

  return { ok: false, fieldErrors: {}, generalError: GENERAL_ERROR };
}

export async function login({ email, password }) {
  let response;
  try {
    response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email, password }),
    });
  } catch {
    return { ok: false, fieldErrors: {}, generalError: LOGIN_GENERAL_ERROR };
  }

  if (response.ok) {
    try {
      return { ok: true, fieldErrors: {}, user: await response.json() };
    } catch {
      return { ok: false, fieldErrors: {}, generalError: LOGIN_GENERAL_ERROR };
    }
  }

  if (response.status === 401) {
    return { ok: false, fieldErrors: {}, generalError: LOGIN_INVALID };
  }

  if (response.status === 400) {
    const fieldErrors = {};
    try {
      const body = await response.json();
      if (body.errors?.email) fieldErrors.email = 'Email mangler.';
      if (body.errors?.password) fieldErrors.password = 'Adgangskode mangler.';
    } catch {
      // fall through to the general error below
    }
    if (Object.keys(fieldErrors).length > 0) return { ok: false, fieldErrors };
  }

  return { ok: false, fieldErrors: {}, generalError: LOGIN_GENERAL_ERROR };
}

// Returns the logged-in user, or null when not logged in or the API cannot be reached.
export async function getCurrentUser() {
  try {
    const response = await fetch(`${API_URL}/api/auth/me`, { credentials: 'include' });
    return response.ok ? await response.json() : null;
  } catch {
    return null;
  }
}
