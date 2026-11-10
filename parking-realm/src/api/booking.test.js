import { getSpaces, registerVehicle } from './booking';

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
