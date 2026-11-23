import { register, login, getCurrentUser, GENERAL_ERROR, LOGIN_INVALID, LOGIN_GENERAL_ERROR } from './auth';

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

describe('login', () => {
  const credentials = { email: 'anna@example.com', password: 'Passw0rd!' };

  test('posts the credentials as JSON with cookies included and returns the user', async () => {
    const user = { id: '1', email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen' };
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => user });

    const result = await login(credentials);

    expect(result).toEqual({ ok: true, fieldErrors: {}, user });
    const [url, options] = global.fetch.mock.calls[0];
    expect(url).toMatch(/\/api\/auth\/login$/);
    expect(options.method).toBe('POST');
    expect(options.credentials).toBe('include');
    expect(JSON.parse(options.body)).toEqual(credentials);
  });

  test('maps 401 to the Danish wrong-credentials message', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 401, json: async () => ({}) });

    const result = await login(credentials);

    expect(result).toEqual({ ok: false, fieldErrors: {}, generalError: LOGIN_INVALID });
  });

  test('maps 400 field errors to Danish "missing" messages', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ errors: { email: ['Email is required.'], password: ['Password is required.'] } }),
    });

    const result = await login({ email: '', password: '' });

    expect(result.fieldErrors).toEqual({ email: 'Email mangler.', password: 'Adgangskode mangler.' });
  });

  test('returns a general error on 5xx and on network failure', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({}) });
    expect(await login(credentials)).toEqual({ ok: false, fieldErrors: {}, generalError: LOGIN_GENERAL_ERROR });

    global.fetch = jest.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    expect(await login(credentials)).toEqual({ ok: false, fieldErrors: {}, generalError: LOGIN_GENERAL_ERROR });
  });
});

describe('getCurrentUser', () => {
  test('returns the user and sends cookies when logged in', async () => {
    const user = { id: '1', email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen' };
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => user });

    expect(await getCurrentUser()).toEqual(user);
    const [url, options] = global.fetch.mock.calls[0];
    expect(url).toMatch(/\/api\/auth\/me$/);
    expect(options.credentials).toBe('include');
  });

  test('returns null when not logged in or the API is unreachable', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 401 });
    expect(await getCurrentUser()).toBeNull();

    global.fetch = jest.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    expect(await getCurrentUser()).toBeNull();
  });
});
