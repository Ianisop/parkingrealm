import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BookingPage from './BookingPage';
import { getSpaces, getVehicles, registerVehicle, getReservations } from './api/booking';

jest.mock('./api/booking');

beforeEach(() => {
  jest.clearAllMocks();
  getReservations.mockResolvedValue({ ok: true, data: [] });
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345' }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 1, isAvailable: true },
    { id: 2, row: 1, column: 0, isAvailable: false },
  ] });
});

test('shows coordinates and only selects available spaces in manual mode', async () => {
  render(<BookingPage />);
  const free = await screen.findByRole('button', { name: 'Plads 1: Ledig' });
  expect(free).toHaveStyle({ gridRow: 1, gridColumn: 2 });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  userEvent.click(free);
  expect(screen.getByText('Valgt plads: 1')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Plads 2: Reserveret' })).toBeDisabled();
});

test('invalid criteria clear stale availability', async () => {
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Plads 1: Ledig' });
  fireEvent.change(screen.getByLabelText('Start'), { target: { value: '2020-01-01T10:00' } });
  expect(await screen.findByRole('alert')).toHaveTextContent('Angiv start og slut.');
  expect(screen.queryByRole('button', { name: 'Plads 1: Ledig' })).not.toBeInTheDocument();
});

test('refresh retrieves new availability', async () => {
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Plads 1: Ledig' });
  getSpaces.mockResolvedValue({ ok: true, data: [] });
  userEvent.click(screen.getByRole('button', { name: 'Opdater pladser' }));
  expect(await screen.findByText('Ingen pladser er konfigureret.')).toBeInTheDocument();
  expect(getSpaces).toHaveBeenCalledTimes(2);
});

test('fetch failures do not show spaces as free', async () => {
  getSpaces.mockResolvedValue({ ok: false, error: 'Status kunne ikke hentes.' });
  render(<BookingPage />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Status kunne ikke hentes.');
  expect(screen.queryByText('Ingen pladser er konfigureret.')).not.toBeInTheDocument();
});

test('registers a vehicle and selects it for booking', async () => {
  registerVehicle.mockResolvedValue({ ok: true, data: { id: 3, registrationNumber: 'CD12345' } });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'CD12345');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  await waitFor(() => expect(screen.getByLabelText('Køretøj')).toHaveValue('3'));
});
