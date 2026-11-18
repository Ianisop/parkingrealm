import {
  createReservation, createUser, deleteUser, deleteVehicle, getSpaces, getUsers, registerVehicle, updateUser,
} from './booking';

beforeEach(() => { global.fetch = jest.fn(); });
afterEach(() => { delete global.fetch; });

test('loads interval availability with credentials', async () => {
  fetch.mockResolvedValue({ ok: true, json: async () => [] });
  const result = await getSpaces('2030-01-01T10:00:00Z', '2030-01-01T11:00:00Z');
  expect(result).toEqual({ ok: true, data: [] });
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/parking-spaces?'), expect.objectContaining({ credentials: 'include' }));
});

test.each([true, false])('registers a vehicle with electric=%s and an antiforgery token', async (isElectric) => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ id: 1 }) });
  await registerVehicle('AB12345', isElectric);
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/vehicles'), expect.objectContaining({
    method: 'POST', body: JSON.stringify({ registrationNumber: 'AB12345', isElectric }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token' }),
  }));
});

test('antiforgery failure prevents a write', async () => {
  fetch.mockResolvedValueOnce({ ok: false });
  expect((await registerVehicle('AB12345', false)).ok).toBe(false);
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

const userPayload = { email: 'a@example.com', password: 'Passw0rd!', firstName: 'A', lastName: 'B', phoneNumber: '+4512345678' };

test('lists users with credentials and an optional search, without an antiforgery token', async () => {
  fetch.mockResolvedValue({ ok: true, json: async () => [] });
  await getUsers(' anna ');
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/admin/users?search=anna'),
    expect.objectContaining({ credentials: 'include' }));
  await getUsers();
  expect(fetch).toHaveBeenLastCalledWith(expect.stringMatching(/\/api\/admin\/users$/), expect.anything());
});

test.each([
  ['POST', () => createUser(userPayload), '/api/admin/users'],
  ['PUT', () => updateUser('u 1', userPayload), '/api/admin/users/u%201'],
  ['DELETE', () => deleteUser('u1'), '/api/admin/users/u1'],
])('%s on users sends an antiforgery token', async (method, call, url) => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce(method === 'DELETE' ? emptyResponse(204) : { ok: true, json: async () => ({}) });
  expect((await call()).ok).toBe(true);
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining(url), expect.objectContaining({
    method, credentials: 'include', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token' }),
  }));
});

test('antiforgery failure prevents a user write', async () => {
  fetch.mockResolvedValueOnce({ ok: false });
  expect((await updateUser('u1', userPayload)).ok).toBe(false);
  expect(fetch).toHaveBeenCalledTimes(1);
});

test('user validation errors from the API are returned as field errors', async () => {
  fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ok: false, status: 400, json: async () => ({ errors: { email: ['Email is already in use.'] } }) });
  expect(await createUser(userPayload)).toEqual({
    ok: false,
    error: 'Oplysningerne er ugyldige. Kontrollér felterne og prøv igen.',
    fieldErrors: { email: ['Email is already in use.'] },
  });
});

test.each([
  ['GET', 500, 'Brugere kunne ikke hentes. Prøv igen senere.', () => getUsers()],
  ['POST', 500, 'Brugeren kunne ikke oprettes. Prøv igen senere.', () => createUser(userPayload)],
  ['PUT', 404, 'Brugeren findes ikke længere.', () => updateUser('u1', userPayload)],
  ['PUT', 403, 'Administratorer kan ikke ændres eller slettes her.', () => updateUser('u1', userPayload)],
  ['PUT', 500, 'Brugeren kunne ikke opdateres. Prøv igen senere.', () => updateUser('u1', userPayload)],
  ['DELETE', 404, 'Brugeren findes ikke længere.', () => deleteUser('u1')],
  ['DELETE', 403, 'Administratorer kan ikke ændres eller slettes her.', () => deleteUser('u1')],
  ['DELETE', 500, 'Brugeren kunne ikke slettes. Prøv igen senere.', () => deleteUser('u1')],
])('%s on users with status %i gets its own message', async (method, status, message, call) => {
  if (method !== 'GET') fetch.mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: 'token' }) });
  fetch.mockResolvedValueOnce({ ...emptyResponse(status), ok: false });
  expect((await call()).error).toBe(message);
});
