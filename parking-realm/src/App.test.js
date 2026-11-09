import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import { getCurrentUser, login } from './api/auth';

jest.mock('./api/auth', () => ({
  ...jest.requireActual('./api/auth'),
  getCurrentUser: jest.fn(),
  login: jest.fn(),
}));

const user = { id: '1', email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen' };

beforeEach(() => {
  getCurrentUser.mockReset();
  login.mockReset();
});

test('shows the login page when there is no session', async () => {
  getCurrentUser.mockResolvedValue(null);

  render(<App />);

  expect(await screen.findByRole('heading', { name: 'Log ind' })).toBeInTheDocument();
});

test('restores an existing session and greets the user', async () => {
  getCurrentUser.mockResolvedValue(user);

  render(<App />);

  expect(await screen.findByRole('heading', { name: 'Velkommen, Anna Hansen' })).toBeInTheDocument();
  expect(screen.queryByRole('heading', { name: 'Log ind' })).not.toBeInTheDocument();
});

test('switches between login and registration', async () => {
  getCurrentUser.mockResolvedValue(null);
  render(<App />);
  await screen.findByRole('heading', { name: 'Log ind' });

  userEvent.click(screen.getByRole('button', { name: 'Ny bruger? Opret bruger' }));
  expect(screen.getByRole('heading', { name: 'Opret bruger' })).toBeInTheDocument();

  userEvent.click(screen.getByRole('button', { name: 'Har du en bruger? Log ind' }));
  expect(screen.getByRole('heading', { name: 'Log ind' })).toBeInTheDocument();
});

test('shows the greeting after a successful login', async () => {
  getCurrentUser.mockResolvedValue(null);
  login.mockResolvedValue({ ok: true, fieldErrors: {}, user });
  render(<App />);
  await screen.findByRole('heading', { name: 'Log ind' });

  userEvent.type(screen.getByLabelText('Email'), 'anna@example.com');
  userEvent.type(screen.getByLabelText('Adgangskode'), 'Passw0rd!');
  userEvent.click(screen.getByRole('button', { name: 'Log ind' }));

  expect(await screen.findByRole('heading', { name: 'Velkommen, Anna Hansen' })).toBeInTheDocument();
});
