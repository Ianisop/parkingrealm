import { createReservation, deleteVehicle, getSpaces, registerVehicle } from './booking';

beforeEach(() => { global.fetch = jest.fn(); });
afterEach(() => { delete global.fetch; });

test('loads interval availability with credentials', async () => {
  fetch.mockResolvedValue({ ok: true, json: async () => [] });
  const result = await getSpaces('2030-01-01T10:00:00Z', '2030-01-01T11:00:00Z');
  expect(result).toEqual({ ok: true, data: [] });
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/parking-spaces?'), expect.objectContaining({ credentials: 'include' }));
});

test('registers a vehicle with an antiforgery token', async () => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ id: 1 }) });
  await registerVehicle('AB12345');
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/vehicles'), expect.objectContaining({
    method: 'POST', body: JSON.stringify({ registrationNumber: 'AB12345' }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token' }),
  }));
});

test('antiforgery failure prevents a write', async () => {
  fetch.mockResolvedValueOnce({ ok: false });
  expect((await registerVehicle('AB12345')).ok).toBe(false);
  expect(fetch).toHaveBeenCalledTimes(1);
});

test('network failures return a visible error', async () => {
  fetch.mockRejectedValue(new Error('offline'));
  expect((await getSpaces()).ok).toBe(false);
});

const emptyResponse = (status) => ({ ok: status < 300, status, json: async () => { throw new SyntaxError('empty'); } });

test('deletes a vehicle with an antiforgery token and accepts an empty success body', async () => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce(emptyResponse(204));
  expect(await deleteVehicle(7)).toEqual({ ok: true, data: null });
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/vehicles/7'), expect.objectContaining({
    method: 'DELETE', credentials: 'include', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token' }),
  }));
});

test('antiforgery failure prevents a vehicle deletion', async () => {
  fetch.mockResolvedValueOnce({ ok: false });
  expect((await deleteVehicle(7)).ok).toBe(false);
  expect(fetch).toHaveBeenCalledTimes(1);
});

test.each([
  [409, 'Køretøjet kan ikke slettes, fordi det har reservationer.'],
  [404, 'Køretøjet findes ikke længere.'],
  [401, 'Log ind igen for at fortsætte.'],
  [500, 'Køretøjet kunne ikke slettes. Prøv igen senere.'],
])('vehicle deletion status %i gets its own message', async (status, message) => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ...emptyResponse(status), ok: false });
  expect(await deleteVehicle(7)).toEqual({ ok: false, error: message, fieldErrors: {} });
});

test('a reservation conflict is still reported as a parking conflict', async () => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ok: false, status: 409, json: async () => ({}) });
  expect((await createReservation({})).error).toBe('Pladsen er ikke længere ledig. Vælg en anden plads.');
});

test.each([
  [400, 'Registreringsnummeret er ugyldigt, ukendt eller allerede registreret.'],
  [503, 'Registreringsnummer-tjenesten er utilgængelig. Prøv igen senere.'],
  [500, 'Køretøjet kunne ikke registreres. Prøv igen senere.'],
])('vehicle registration status %i gets its own message', async (status, message) => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ok: false, status, json: async () => ({ errors: {} }) });
  expect((await registerVehicle('ZZ99999')).error).toBe(message);
});
