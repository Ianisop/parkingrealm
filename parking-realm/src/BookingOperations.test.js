import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BookingPage from './BookingPage';
import { getSpaces, getVehicles, getReservations, createReservation, cancelReservation } from './api/booking';

jest.mock('./api/booking');

const reservation = {
  id: 8, parkingSpaceId: 2, vehicleId: 1, status: 'Active',
  start: '2090-01-01T10:00:00Z', end: '2090-01-01T11:00:00Z',
};

beforeEach(() => {
  jest.clearAllMocks();
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345' }] });
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 2, row: 0, column: 0, isAvailable: true }] });
  getReservations.mockResolvedValue({ ok: true, data: [] });
});

async function fillCriteria() {
  await screen.findByRole('option', { name: 'AB12345' });
  fireEvent.change(screen.getByLabelText('Start'), { target: { value: '2090-01-01T10:00' } });
  fireEvent.change(screen.getByLabelText('Slut'), { target: { value: '2090-01-01T11:00' } });
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
}

test('successful booking confirms committed details and refreshes the map and list', async () => {
  createReservation.mockResolvedValue({ ok: true, data: reservation });
  render(<BookingPage />);
  await fillCriteria();
  getReservations.mockResolvedValue({ ok: true, data: [reservation] });
  const before = getSpaces.mock.calls.length;
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  expect(await screen.findByText('Reservation 8 bekræftet. Plads 2.')).toBeInTheDocument();
  await screen.findByRole('button', { name: 'Annuller reservation 8' });
  expect(getSpaces.mock.calls.length).toBeGreaterThan(before);
  expect(createReservation).toHaveBeenCalledWith(expect.objectContaining({ vehicleId: 1, mode: 'automatic', parkingSpaceId: null }));
});

test('failed booking never displays confirmation', async () => {
  createReservation.mockResolvedValue({ ok: false, error: 'Pladsen er blevet reserveret.' });
  render(<BookingPage />);
  await fillCriteria();
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Pladsen er blevet reserveret.');
  expect(screen.queryByText(/bekræftet/)).not.toBeInTheDocument();
});

test('successful cancellation refreshes reservation status', async () => {
  getReservations.mockResolvedValue({ ok: true, data: [reservation] });
  cancelReservation.mockResolvedValue({ ok: true, data: { id: 8, status: 'Cancelled' } });
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Annuller reservation 8' });
  getReservations.mockResolvedValue({ ok: true, data: [{ ...reservation, status: 'Cancelled' }] });
  userEvent.click(screen.getByRole('button', { name: 'Annuller reservation 8' }));
  expect(await screen.findByText('Reservation 8 annulleret.')).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Annuller reservation 8' })).not.toBeInTheDocument());
});

test('failed cancellation leaves the reservation active and shows no success', async () => {
  getReservations.mockResolvedValue({ ok: true, data: [reservation] });
  cancelReservation.mockResolvedValue({ ok: false, error: 'Annullering mislykkedes.' });
  render(<BookingPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Annuller reservation 8' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Annullering mislykkedes.');
  expect(screen.queryByText('Reservation 8 annulleret.')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Annuller reservation 8' })).toBeInTheDocument();
});
