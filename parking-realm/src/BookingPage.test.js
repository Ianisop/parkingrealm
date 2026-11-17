import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BookingPage from './BookingPage';
import { getSpaces, getVehicles, registerVehicle, getReservations } from './api/booking';

jest.mock('./api/booking');

beforeEach(() => {
  jest.clearAllMocks();
  jest.spyOn(Date, 'now').mockReturnValue(new Date(2026, 5, 10, 10, 4, 30).getTime());
  getReservations.mockResolvedValue({ ok: true, data: [] });
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: false }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 1, isAvailable: true },
    { id: 2, row: 1, column: 0, isAvailable: false },
  ] });
});

afterEach(() => jest.restoreAllMocks());

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
  expect(await screen.findByRole('alert')).toHaveTextContent('Starttidspunktet er passeret.');
  expect(screen.queryByRole('button', { name: 'Plads 1: Ledig' })).not.toBeInTheDocument();
  expect(screen.queryByText(/Anbefalet plads:/)).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
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

test.each([true, false])('registers a vehicle with electric=%s and selects it for booking', async (isElectric) => {
  registerVehicle.mockResolvedValue({ ok: true, data: { id: 3, registrationNumber: 'CD12345', isElectric } });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'CD12345');
  userEvent.selectOptions(screen.getByLabelText('Er køretøjet elektrisk?'), String(isElectric));
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  await waitFor(() => expect(screen.getByLabelText('Køretøj')).toHaveValue('3'));
  expect(registerVehicle).toHaveBeenCalledWith('CD12345', isElectric);
  expect(screen.getByLabelText('Er køretøjet elektrisk?')).toHaveValue('');
});

test('starts with a same-day interval and shows a booking summary and availability count', async () => {
  render(<BookingPage />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
  expect(screen.getByLabelText('Start')).toHaveValue('2026-06-10T10:10');
  expect(screen.getByLabelText('Slut')).toHaveValue('2026-06-10T11:10');
  expect(getSpaces).toHaveBeenCalledWith(new Date('2026-06-10T10:10').toISOString(), new Date('2026-06-10T11:10').toISOString());
  const summary = within(screen.getByRole('region', { name: 'Bookingoversigt' }));
  expect(summary.getByText('Køretøj: AB12345')).toBeInTheDocument();
  expect(summary.getByText('Varighed: 60 minutter')).toBeInTheDocument();
  expect(screen.getByText('1 af 2 pladser er ledige i det valgte tidsrum.')).toBeInTheDocument();
});

test('duration presets follow start changes and allow a custom end', async () => {
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  userEvent.click(screen.getByRole('button', { name: '2 timer' }));
  expect(screen.getByLabelText('Slut')).toHaveValue('2026-06-10T12:10');
  fireEvent.change(screen.getByLabelText('Start'), { target: { value: '2026-06-10T14:00' } });
  expect(screen.getByLabelText('Slut')).toHaveValue('2026-06-10T16:00');
  fireEvent.change(screen.getByLabelText('Slut'), { target: { value: '2026-06-10T17:30' } });
  expect(screen.getByRole('button', { name: '2 timer' })).toHaveAttribute('aria-pressed', 'false');
  expect(screen.getByText('Varighed: 210 minutter')).toBeInTheDocument();
  userEvent.click(screen.getByRole('button', { name: 'Book i dag' }));
  expect(screen.getByLabelText('Start')).toHaveValue('2026-06-10T10:10');
  expect(screen.getByLabelText('Slut')).toHaveValue('2026-06-10T11:10');
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
});

test('clearing and replacing start preserves the selected duration preset', async () => {
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  userEvent.click(screen.getByRole('button', { name: '2 timer' }));
  fireEvent.change(screen.getByLabelText('Start'), { target: { value: '' } });
  expect(screen.getByLabelText('Slut')).toHaveValue('');
  fireEvent.change(screen.getByLabelText('Start'), { target: { value: '2026-06-10T14:00' } });
  expect(screen.getByLabelText('Slut')).toHaveValue('2026-06-10T16:00');
  expect(screen.getByRole('button', { name: '2 timer' })).toHaveAttribute('aria-pressed', 'true');
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
});

test('booking requires a vehicle, available capacity and a manual selection when applicable', async () => {
  render(<BookingPage />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
  fireEvent.change(screen.getByLabelText('Køretøj'), { target: { value: '' } });
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Køretøj'), { target: { value: '1' } });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  userEvent.click(screen.getByRole('button', { name: 'Plads 1: Ledig' }));
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled();
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 1, row: 0, column: 1, isAvailable: false }] });
  userEvent.click(screen.getByRole('button', { name: 'Opdater pladser' }));
  await screen.findByText(/Ingen ledige pladser/);
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  expect(screen.queryByText('Valgt plads: 1')).not.toBeInTheDocument();
});

test('recommends and highlights the nearest available spot to an occupied preferred location', async () => {
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 10, column: 10, isAvailable: true },
    { id: 5, row: 2, column: 3, isAvailable: true },
    { id: 3, row: 3, column: 2, isAvailable: true },
    { id: 9, row: 2, column: 2, isAvailable: false },
  ] });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'Nær plads 9 (række 3, kolonne 3)' });
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '9' } });
  expect(screen.getByText(/Anbefalet plads: 3\./)).toHaveTextContent('Afstand i oversigten: 1.');
  expect(screen.getByRole('button', { name: 'Plads 3: Ledig' })).toHaveAttribute('data-recommended', 'true');
  expect(screen.getByRole('button', { name: 'Plads 9: Reserveret' })).toBeDisabled();
  userEvent.click(screen.getByLabelText('Vælg selv'));
  expect(screen.queryByLabelText('Foretrukken placering')).not.toBeInTheDocument();
  expect(screen.queryByText(/Anbefalet plads:/)).not.toBeInTheDocument();
});

test('refresh updates the recommendation without losing the preferred location', async () => {
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: true },
    { id: 3, row: 0, column: 2, isAvailable: false },
  ] });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'Nær plads 3 (række 1, kolonne 3)' });
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '3' } });
  expect(screen.getByText(/Anbefalet plads: 2\./)).toBeInTheDocument();
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: false },
    { id: 3, row: 0, column: 2, isAvailable: false },
  ] });
  userEvent.click(screen.getByRole('button', { name: 'Opdater pladser' }));
  expect(await screen.findByText(/Anbefalet plads: 1\./)).toHaveTextContent('Afstand i oversigten: 2.');
  expect(screen.getByLabelText('Foretrukken placering')).toHaveValue('3');
});

test('a removed preferred location blocks booking until the preference is cleared', async () => {
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'Nær plads 2 (række 2, kolonne 1)' });
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '2' } });
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 1, row: 0, column: 1, isAvailable: true }] });
  userEvent.click(screen.getByRole('button', { name: 'Opdater pladser' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Den foretrukne placering findes ikke længere.');
  expect(screen.queryByText(/Anbefalet plads:/)).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Foretrukken placering'), { target: { value: '' } });
  expect(screen.getByText(/Anbefalet plads: 1\./)).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled();
});

test('checkboxes filter recommendations and label accessible and charging spaces', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: true }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true, isAccessible: true },
    { id: 2, row: 0, column: 1, isAvailable: true },
    { id: 3, row: 0, column: 2, isAvailable: true, hasCharger: true },
    { id: 4, row: 0, column: 3, isAvailable: true, isAccessible: true, hasCharger: true },
  ] });
  render(<BookingPage />);
  expect(await screen.findByText(/Anbefalet plads: 2\./)).toBeInTheDocument();
  const accessible = screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' });
  const charger = screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' });
  expect(accessible).not.toBeChecked();
  expect(charger).not.toBeChecked();
  userEvent.click(accessible);
  expect(screen.getByText(/Anbefalet plads: 1\./)).toBeInTheDocument();
  expect(screen.getByText('Krav: Handicapplads')).toBeInTheDocument();
  userEvent.click(charger);
  expect(screen.getByText(/Anbefalet plads: 4\./)).toBeInTheDocument();
  expect(screen.getByText('Krav: Handicapplads og El-lader')).toBeInTheDocument();
  userEvent.click(accessible);
  expect(screen.getByText(/Anbefalet plads: 3\./)).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Plads 1: Ledig · Handicapplads' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Plads 3: Ledig · El-lader' })).toBeInTheDocument();
  expect(screen.getByText('1 ledige pladser opfylder dine krav.')).toBeInTheDocument();
});

test('accessible spaces explain the checkbox and become selectable for a non-electric vehicle', async () => {
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true, isAccessible: true },
    { id: 2, row: 0, column: 1, isAvailable: false, isAccessible: true },
  ] });
  render(<BookingPage />);
  const space = await screen.findByRole('button', { name: 'Plads 1: Ledig · Handicapplads' });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  const checkbox = screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' });
  expect(checkbox).toBeEnabled();
  expect(checkbox).toHaveAccessibleDescription(/både elbiler og andre køretøjer/);
  expect(space).toBeDisabled();
  expect(space).toHaveAccessibleDescription(/Sæt kryds/);
  expect(screen.getByRole('link', { name: 'Jeg har brug for en handicapplads' })).toHaveAttribute('href', `#${checkbox.id}`);
  userEvent.click(checkbox);
  expect(screen.queryByText(/Vil du booke en handicapplads/)).not.toBeInTheDocument();
  expect(space).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Plads 2: Reserveret · Handicapplads' })).toBeDisabled();
  userEvent.click(space);
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled();
  userEvent.click(checkbox);
  expect(space).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
});

test('manual selection is cleared when checkbox requirements change', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: true }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true, isAccessible: true },
    { id: 2, row: 0, column: 1, isAvailable: true },
    { id: 3, row: 0, column: 2, isAvailable: true, hasCharger: true },
  ] });
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Plads 2: Ledig' });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  const accessibleSpace = screen.getByRole('button', { name: 'Plads 1: Ledig · Handicapplads' });
  const standardSpace = screen.getByRole('button', { name: 'Plads 2: Ledig' });
  expect(accessibleSpace).toBeDisabled();
  userEvent.click(standardSpace);
  expect(screen.getByText('Valgt plads: 2')).toBeInTheDocument();
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' }));
  expect(screen.queryByText('Valgt plads: 2')).not.toBeInTheDocument();
  expect(standardSpace).toBeDisabled();
  expect(accessibleSpace).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  userEvent.click(accessibleSpace);
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled();
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' }));
  expect(screen.queryByText('Valgt plads: 1')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  expect(screen.getByText(/Ingen ledige pladser opfylder alle dine krav/)).toBeInTheDocument();
});

test('both requirements cannot be satisfied by separate accessible and charging spots', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345', isElectric: true }] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true, isAccessible: true },
    { id: 2, row: 0, column: 1, isAvailable: true, hasCharger: true },
  ] });
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Plads 1: Ledig · Handicapplads' });
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en handicapplads' }));
  userEvent.click(screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' }));
  expect(screen.queryByText(/Anbefalet plads:/)).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  expect(screen.getByText(/Ingen ledige pladser opfylder alle dine krav/)).toBeInTheDocument();
});

test('registering requires an explicit vehicle-type choice', async () => {
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  const choice = screen.getByLabelText('Er køretøjet elektrisk?');
  expect(choice).toHaveValue('');
  expect(choice).toBeRequired();
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'CD12345');
  fireEvent.submit(screen.getByLabelText('Registreringsnummer').closest('form'));
  expect(await screen.findByRole('alert')).toHaveTextContent('Angiv, om køretøjet er elektrisk.');
  expect(registerVehicle).not.toHaveBeenCalled();
});

test('registering an electric vehicle immediately makes charging spots eligible', async () => {
  getSpaces.mockResolvedValue({ ok: true, data: [{ id: 7, row: 0, column: 0, isAvailable: true, hasCharger: true }] });
  registerVehicle.mockResolvedValue({ ok: true, data: { id: 3, registrationNumber: 'EV12345', isElectric: true } });
  render(<BookingPage />);
  await screen.findByRole('option', { name: 'AB12345' });
  await screen.findByRole('button', { name: 'Plads 7: Ledig · El-lader' });
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'EV12345');
  userEvent.selectOptions(screen.getByLabelText('Er køretøjet elektrisk?'), 'true');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByText(/Anbefalet plads: 7\./)).toBeInTheDocument();
  expect(screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' })).not.toBeChecked();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled();
});

test('switching from an electric vehicle clears a charger requirement and manual selection', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [
    { id: 1, registrationNumber: 'EV12345', isElectric: true },
    { id: 2, registrationNumber: 'AB12345', isElectric: false },
  ] });
  getSpaces.mockResolvedValue({ ok: true, data: [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: true, hasCharger: true },
  ] });
  render(<BookingPage />);
  await screen.findByRole('button', { name: 'Plads 2: Ledig · El-lader' });
  await screen.findByRole('option', { name: 'EV12345' });
  userEvent.click(screen.getByLabelText('Vælg selv'));
  const charger = screen.getByRole('checkbox', { name: 'Jeg har brug for en el-lader' });
  const chargingSpace = screen.getByRole('button', { name: 'Plads 2: Ledig · El-lader' });
  expect(charger).toBeEnabled();
  expect(chargingSpace).toBeEnabled();
  userEvent.click(charger);
  userEvent.click(chargingSpace);
  expect(screen.getByText('Valgt plads: 2')).toBeInTheDocument();
  userEvent.selectOptions(screen.getByLabelText('Køretøj'), '2');
  expect(charger).not.toBeChecked();
  expect(charger).toBeDisabled();
  expect(chargingSpace).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Plads 1: Ledig' })).toBeEnabled();
  expect(screen.queryByText('Valgt plads: 2')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  userEvent.selectOptions(screen.getByLabelText('Køretøj'), '1');
  expect(charger).toBeEnabled();
  expect(charger).not.toBeChecked();
  expect(chargingSpace).toBeEnabled();
});

test('a new customer can register a vehicle inline without duplicate submissions', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [] });
  let resolveRegistration;
  registerVehicle.mockImplementation(() => new Promise((resolve) => { resolveRegistration = resolve; }));
  render(<BookingPage />);
  await screen.findByText(/Registrer dit køretøj ovenfor/);
  expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeDisabled();
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'CD12345');
  userEvent.selectOptions(screen.getByLabelText('Er køretøjet elektrisk?'), 'false');
  const form = screen.getByLabelText('Registreringsnummer').closest('form');
  fireEvent.submit(form);
  fireEvent.submit(form);
  expect(registerVehicle).toHaveBeenCalledTimes(1);
  expect(screen.getByRole('button', { name: 'Registrerer…' })).toBeDisabled();
  await act(async () => resolveRegistration({ ok: true, data: { id: 3, registrationNumber: 'CD12345', isElectric: false } }));
  expect(screen.getByLabelText('Køretøj')).toHaveValue('3');
  await waitFor(() => expect(screen.getByRole('button', { name: 'Bekræft booking' })).toBeEnabled());
});
