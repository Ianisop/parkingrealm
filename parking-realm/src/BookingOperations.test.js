import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
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
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: false }] });
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 2, row: 0, column: 0, isAvailable: true }] });
  getReservations.mockResolvedValue({ ok: true, data: [] });
});

afterEach(() => jest.restoreAllMocks());

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
  expect(await screen.findByText(/^Reservation 8 bekræftet\. Plads 2\./)).toHaveTextContent(new Date(reservation.start).toLocaleString('da-DK'));
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

test('books today using UTC timestamps and prevents repeated submission while pending', async () => {
  const now = new Date(2026, 5, 10, 10, 4, 30).getTime();
  jest.spyOn(Date, 'now').mockReturnValue(now);
  let resolveBooking;
  createReservation.mockImplementation(() => new Promise((resolve) => { resolveBooking = resolve; }));
  render(<BookingPage />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
  const start = screen.getByLabelText('Start').value;
  const end = screen.getByLabelText('Slut').value;
  const button = screen.getByRole('button', { name: 'Bekræft booking' });
  userEvent.click(button);
  userEvent.click(button);
  expect(createReservation).toHaveBeenCalledTimes(1);
  expect(createReservation).toHaveBeenCalledWith({
    vehicleId: 1, start: new Date(start).toISOString(), end: new Date(end).toISOString(),
    mode: 'automatic', parkingSpaceId: null, requiresAccessible: false, requiresCharger: false,
  });
  expect(Date.parse(start)).toBeGreaterThan(now);
  expect(screen.getByRole('button', { name: 'Behandler…' })).toBeDisabled();
  await act(async () => resolveBooking({ ok: true, data: { ...reservation, start: new Date(start).toISOString(), end: new Date(end).toISOString() } }));
  expect(await screen.findByText(/^Reservation 8 bekræftet/)).toBeInTheDocument();
});

test('revalidates a start that expires before submitting', async () => {
  render(<BookingPage />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
  jest.spyOn(Date, 'now').mockReturnValue(Date.parse(screen.getByLabelText('Start').value));
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Starttidspunktet er passeret.');
  expect(createReservation).not.toHaveBeenCalled();
});

test('automatic booking sends the location preference and confirms the server-assigned spot', async () => {
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 2, row: 0, column: 0, isAvailable: true },
    { id: 1, row: 9, column: 0, isAvailable: true },
  ] });
  createReservation.mockResolvedValue({ ok: true, data: { ...reservation, parkingSpaceId: 1 } });
  render(<BookingPage />);
  await fillCriteria();
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '2' } });
  expect(screen.getByText(/Anbefalet plads: 2\./)).toBeInTheDocument();
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  expect(await screen.findByText(/^Reservation 8 bekræftet\. Plads 1\./)).toBeInTheDocument();
  expect(createReservation).toHaveBeenCalledWith(expect.objectContaining({
    mode: 'automatic', parkingSpaceId: null, preferredSpaceId: 2,
  }));
});

test('manual booking omits a previously selected automatic preference', async () => {
  createReservation.mockResolvedValue({ ok: true, data: reservation });
  render(<BookingPage />);
  await fillCriteria();
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '2' } });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  userEvent.click(screen.getByRole('button', { name: 'Plads 2: Ledig' }));
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  await screen.findByText(/^Reservation 8 bekræftet/);
  const request = createReservation.mock.calls[0][0];
  expect(request).toMatchObject({ mode: 'manual', parkingSpaceId: 2 });
  expect(request).not.toHaveProperty('preferredSpaceId');
});

test.each(['automatic', 'manual'])('sends both checkbox requirements with a %s booking', async (mode) => {
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: true }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: true, isAccessible: true, hasCharger: true },
  ] });
  createReservation.mockResolvedValue({ ok: true, data: reservation });
  render(<BookingPage />);
  await fillCriteria();
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' }));
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' }));
  if (mode === 'manual') {
    userEvent.click(screen.getByLabelText('Vælg selv'));
    userEvent.click(screen.getByRole('button', { name: 'Plads 2: Ledig · Handicapplads · El-lader' }));
  }
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  await screen.findByText(/^Reservation 8 bekræftet/);
  expect(createReservation).toHaveBeenCalledWith(expect.objectContaining({
    mode, requiresAccessible: true, requiresCharger: true, parkingSpaceId: mode === 'manual' ? 2 : null,
  }));
});

test.each(['automatic', 'manual'])('a non-electric vehicle can book an accessible spot in %s mode', async (mode) => {
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 2, row: 0, column: 0, isAvailable: true, isAccessible: true },
  ] });
  createReservation.mockResolvedValue({ ok: true, data: reservation });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  await screen.findByRole('button', { name: 'Plads 2: Ledig · Handicapplads' });
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  expect(screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' })).toBeDisabled();
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' }));
  if (mode === 'manual') {
    userEvent.click(screen.getByLabelText('Vælg selv'));
    userEvent.click(screen.getByRole('button', { name: 'Plads 2: Ledig · Handicapplads' }));
  } else {
    expect(screen.getByText(/Anbefalet plads: 2\./)).toBeInTheDocument();
  }
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  await screen.findByText(/^Reservation 8 bekræftet\. Plads 2\./);
  expect(createReservation).toHaveBeenCalledWith(expect.objectContaining({
    vehicleId: 1, mode, requiresAccessible: true, requiresCharger: false,
    parkingSpaceId: mode === 'manual' ? 2 : null,
  }));
});

test('an EV can book a charging spot without sending an electric flag or requiring charging', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: true }] });
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 2, row: 0, column: 0, isAvailable: true, hasCharger: true }] });
  createReservation.mockResolvedValue({ ok: true, data: reservation });
  render(<BookingPage />);
  await fillCriteria();
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  await screen.findByText(/^Reservation 8 bekræftet/);
  const request = createReservation.mock.calls[0][0];
  expect(request).toMatchObject({ vehicleId: 1, requiresCharger: false });
  expect(request).not.toHaveProperty('isElectric');
  expect(request).not.toHaveProperty('isElectricVehicle');
});

test('a booking conflict refreshes availability and clears the unavailable manual selection', async () => {
  createReservation.mockResolvedValue({ ok: false, error: 'Pladsen er ikke længere ledig. Vælg en anden plads.' });
  render(<BookingPage />);
  await fillCriteria();
  userEvent.click(screen.getByLabelText('Vælg selv'));
  userEvent.click(screen.getByRole('button', { name: 'Plads 2: Ledig' }));
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 2, row: 0, column: 0, isAvailable: false }] });
  userEvent.click(screen.getByRole('button', { name: 'Bekræft booking' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Pladsen er ikke længere ledig.');
  await screen.findByText(/Ingen ledige pladser/);
  expect(screen.queryByText('Valgt plads: 2')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  expect(screen.queryByText(/bekræftet/)).not.toBeInTheDocument();
});
