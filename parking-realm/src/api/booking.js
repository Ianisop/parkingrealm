const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5198';

function errorMessage(path, method, status) {
  if (status === 401) return 'Log ind igen for at fortsætte.';
  if (path.startsWith('/api/vehicles')) {
    if (method === 'DELETE') {
      if (status === 409) return 'Køretøjet kan ikke slettes, fordi det har reservationer.';
      if (status === 404) return 'Køretøjet findes ikke længere.';
      return 'Køretøjet kunne ikke slettes. Prøv igen senere.';
    }
    if (method === 'POST') {
      if (status === 400) return 'Registreringsnummeret er ugyldigt, ukendt eller allerede registreret.';
      if (status === 503) return 'Registreringsnummer-tjenesten er utilgængelig. Prøv igen senere.';
      return 'Køretøjet kunne ikke registreres. Prøv igen senere.';
    }
  }
  if (status === 409) return 'Pladsen er ikke længere ledig. Vælg en anden plads.';
  return 'Handlingen mislykkedes. Kontrollér oplysningerne og prøv igen.';
}

export async function bookingRequest(path, options = {}) {
  try {
    if (options.method === 'POST' || options.method === 'DELETE') {
      const tokenResponse = await fetch(`${API_URL}/api/antiforgery`, { credentials: 'include' });
      if (!tokenResponse.ok) return { ok: false, error: 'Handlingen kunne ikke godkendes. Log ind igen.' };
      const { requestToken } = await tokenResponse.json();
      if (!requestToken) return { ok: false, error: 'Handlingen kunne ikke godkendes. Prøv igen.' };
      options = { ...options, headers: { ...options.headers, 'X-CSRF-TOKEN': requestToken } };
    }
    const response = await fetch(`${API_URL}${path}`, { credentials: 'include', ...options });
    const data = await response.json().catch(() => null);
    if (response.ok) return { ok: true, data };
    return {
      ok: false,
      error: errorMessage(path, options.method, response.status),
      fieldErrors: data?.errors || {},
    };
  } catch {
    return { ok: false, error: 'Data kunne ikke hentes. Prøv igen senere.' };
  }
}

export function getSpaces(start, end) {
  const query = start && end ? `?${new URLSearchParams({ start, end })}` : '';
  return bookingRequest(`/api/parking-spaces${query}`);
}

export const getVehicles = () => bookingRequest('/api/vehicles');
export const getBookingStatistics = (start, end) => bookingRequest(
  `/api/admin/booking-statistics?${new URLSearchParams({ start, end })}`);
export const getReservations = () => bookingRequest('/api/reservations');
export const createReservation = (criteria) => bookingRequest('/api/reservations', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(criteria),
});
export const cancelReservation = (id) => bookingRequest(`/api/reservations/${id}/cancel`, { method: 'POST' });
export const registerVehicle = (registrationNumber) => bookingRequest('/api/vehicles', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ registrationNumber }),
});
export const deleteVehicle = (id) => bookingRequest(`/api/vehicles/${id}`, { method: 'DELETE' });
