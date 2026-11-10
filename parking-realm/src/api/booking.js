const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5198';

export async function bookingRequest(path, options = {}) {
  try {
    if (options.method === 'POST') {
      const tokenResponse = await fetch(`${API_URL}/api/antiforgery`, { credentials: 'include' });
      if (!tokenResponse.ok) return { ok: false, error: 'Handlingen kunne ikke godkendes. Log ind igen.' };
      const { requestToken } = await tokenResponse.json();
      if (!requestToken) return { ok: false, error: 'Handlingen kunne ikke godkendes. Prøv igen.' };
      options = { ...options, headers: { ...options.headers, 'X-CSRF-TOKEN': requestToken } };
    }
    const response = await fetch(`${API_URL}${path}`, { credentials: 'include', ...options });
    const data = await response.json();
    if (response.ok) return { ok: true, data };
    return {
      ok: false,
      error: response.status === 409
        ? 'Pladsen er ikke længere ledig. Vælg en anden plads.'
        : response.status === 401
          ? 'Log ind igen for at fortsætte.'
          : 'Handlingen mislykkedes. Kontrollér oplysningerne og prøv igen.',
      fieldErrors: data.errors || {},
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
