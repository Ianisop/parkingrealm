import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import { getCurrentUser } from './api/auth';
import { deleteVehicle, getReservations, getSpaces, getVehicles, registerVehicle } from './api/booking';

jest.mock('./api/auth', () => ({ ...jest.requireActual('./api/auth'), getCurrentUser: jest.fn() }));
jest.mock('./api/booking');

let vehicles;

beforeEach(() => {
  jest.clearAllMocks();
  vehicles = [{ id: 1, registrationNumber: 'AB12345' }, { id: 2, registrationNumber: 'CD12345' }];
  getCurrentUser.mockResolvedValue({ id: '1', email: 'a@example.com', firstName: 'Anna', lastName: 'Hansen' });
  getReservations.mockResolvedValue({ ok: true, data: [] });
  getSpaces.mockResolvedValue({ ok: true, data: [] });
  getVehicles.mockImplementation(async () => ({ ok: true, data: [...vehicles] }));
  registerVehicle.mockImplementation(async (number) => {
    if (number === 'ZZ99999') return { ok: false, error: 'Registreringsnummeret er ugyldigt, ukendt eller allerede registreret.' };
    const vehicle = { id: 3, registrationNumber: number };
    vehicles.push(vehicle);
    return { ok: true, data: vehicle };
  });
  deleteVehicle.mockImplementation(async (id) => {
    vehicles = vehicles.filter((vehicle) => vehicle.id !== id);
    return { ok: true, data: null };
  });
});

test('booking registration succeeds for a known plate and fails visibly for an unknown one', async () => {
  render(<App />);
  await screen.findByRole('option', { name: 'AB12345' });

  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'ZZ99999');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('ukendt');
  expect(screen.queryByRole('option', { name: 'ZZ99999' })).not.toBeInTheDocument();

  userEvent.clear(screen.getByLabelText('Registreringsnummer'));
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'EF12345');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByRole('option', { name: 'EF12345' })).toBeInTheDocument();
  expect(registerVehicle).toHaveBeenCalledWith('EF12345');
});

test('a vehicle deleted in the overview is gone from the booking selection on return', async () => {
  render(<App />);
  await screen.findByRole('option', { name: 'CD12345' });

  userEvent.click(screen.getByRole('button', { name: 'Mine køretøjer' }));
  userEvent.click(await screen.findByRole('button', { name: 'Slet CD12345' }));
  userEvent.click(screen.getByRole('button', { name: 'Bekræft sletning af CD12345' }));
  await waitFor(() => expect(deleteVehicle).toHaveBeenCalledWith(2));

  userEvent.click(screen.getByRole('button', { name: 'Book parkering' }));
  expect(await screen.findByRole('option', { name: 'AB12345' })).toBeInTheDocument();
  expect(screen.queryByRole('option', { name: 'CD12345' })).not.toBeInTheDocument();
});

test('a vehicle registered in the overview is selectable in booking', async () => {
  render(<App />);
  userEvent.click(await screen.findByRole('button', { name: 'Mine køretøjer' }));
  await screen.findByText('AB12345');
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'EF12345');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  await screen.findByRole('status');

  userEvent.click(screen.getByRole('button', { name: 'Book parkering' }));
  expect(await screen.findByRole('option', { name: 'EF12345' })).toBeInTheDocument();
});
