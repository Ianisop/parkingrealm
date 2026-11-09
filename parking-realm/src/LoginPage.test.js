import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import LoginPage from './LoginPage';
import { login } from './api/auth';

jest.mock('./api/auth', () => ({
  ...jest.requireActual('./api/auth'),
  login: jest.fn(),
}));

const user = { id: '1', email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen' };

function fill(email = 'anna@example.com', password = 'Passw0rd!') {
  if (email) userEvent.type(screen.getByLabelText('Email'), email);
  if (password) userEvent.type(screen.getByLabelText('Adgangskode'), password);
}

function submit() {
  userEvent.click(screen.getByRole('button', { name: 'Log ind' }));
}

beforeEach(() => {
  login.mockReset();
});

test('renders email and password fields and a submit button', () => {
  render(<LoginPage onLoggedIn={jest.fn()} />);

  expect(screen.getByLabelText('Email')).toBeInTheDocument();
  expect(screen.getByLabelText('Adgangskode')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Log ind' })).toBeInTheDocument();
});

test('blocks an empty submit and names the missing details', () => {
  render(<LoginPage onLoggedIn={jest.fn()} />);

  submit();

  expect(login).not.toHaveBeenCalled();
  expect(screen.getByText('Email mangler.')).toBeInTheDocument();
  expect(screen.getByText('Adgangskode mangler.')).toBeInTheDocument();
});

test('hands the user to onLoggedIn after a successful login', async () => {
  login.mockResolvedValue({ ok: true, fieldErrors: {}, user });
  const onLoggedIn = jest.fn();
  render(<LoginPage onLoggedIn={onLoggedIn} />);
  fill();

  submit();

  await screen.findByRole('button', { name: 'Log ind' });
  expect(login).toHaveBeenCalledWith({ email: 'anna@example.com', password: 'Passw0rd!' });
  expect(onLoggedIn).toHaveBeenCalledWith(user);
});

test('shows the wrong-credentials message, clears the password and does not log in', async () => {
  login.mockResolvedValue({ ok: false, fieldErrors: {}, generalError: 'Email eller adgangskode er forkert.' });
  const onLoggedIn = jest.fn();
  render(<LoginPage onLoggedIn={onLoggedIn} />);
  fill('anna@example.com', 'Wrong123!');

  submit();

  expect(await screen.findByRole('alert')).toHaveTextContent('Email eller adgangskode er forkert.');
  expect(onLoggedIn).not.toHaveBeenCalled();
  expect(screen.getByLabelText('Adgangskode')).toHaveValue('');
  expect(screen.getByLabelText('Email')).toHaveValue('anna@example.com');
});

test('shows a general failure message when login fails technically', async () => {
  login.mockResolvedValue({ ok: false, fieldErrors: {}, generalError: 'Login mislykkedes. Prøv igen senere.' });
  const onLoggedIn = jest.fn();
  render(<LoginPage onLoggedIn={onLoggedIn} />);
  fill();

  submit();

  expect(await screen.findByRole('alert')).toHaveTextContent('Login mislykkedes. Prøv igen senere.');
  expect(onLoggedIn).not.toHaveBeenCalled();
});
