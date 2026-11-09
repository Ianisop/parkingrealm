import { register, GENERAL_ERROR } from './auth';

const details = {
  email: 'anna@example.com',
  password: 'Passw0rd!',
  firstName: 'Anna',
  lastName: 'Hansen',
  phoneNumber: '+4512345678',
};

afterEach(() => {
  jest.restoreAllMocks();
});

test('sends all five fields as JSON to the register endpoint', async () => {
  global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 201 });

  const result = await register(details);

  expect(result).toEqual({ ok: true, fieldErrors: {} });
  const [url, options] = global.fetch.mock.calls[0];
  expect(url).toMatch(/\/api\/auth\/register$/);
  expect(options.method).toBe('POST');
  expect(JSON.parse(options.body)).toEqual(details);
});

test('translates 400 field errors to Danish', async () => {
  global.fetch = jest.fn().mockResolvedValue({
    ok: false,
    status: 400,
    json: async () => ({ errors: { email: ['Email is already in use.'], phoneNumber: ['x'] } }),
  });

  const result = await register(details);

  expect(result.ok).toBe(false);
  expect(result.fieldErrors).toEqual({
    email: 'Email er allerede i brug.',
    phoneNumber: 'Værdien er ugyldig.',
  });
});

test('returns a general error on 5xx', async () => {
  global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({}) });

  const result = await register(details);

  expect(result).toEqual({ ok: false, fieldErrors: {}, generalError: GENERAL_ERROR });
});

test('returns a general error when the network fails', async () => {
  global.fetch = jest.fn().mockRejectedValue(new TypeError('Failed to fetch'));

  const result = await register(details);

  expect(result).toEqual({ ok: false, fieldErrors: {}, generalError: GENERAL_ERROR });
});

test('returns a general error when a 400 has no readable field errors', async () => {
  global.fetch = jest.fn().mockResolvedValue({
    ok: false,
    status: 400,
    json: async () => {
      throw new Error('not json');
    },
  });

  const result = await register(details);

  expect(result.generalError).toBe(GENERAL_ERROR);
});
