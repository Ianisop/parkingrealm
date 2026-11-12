import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import VehiclesPage from './VehiclesPage';
import { deleteVehicle, getVehicles, registerVehicle } from './api/booking';

jest.mock('./api/booking');

beforeEach(() => {
  jest.clearAllMocks();
  getVehicles.mockResolvedValue({ ok: true, data: [{ id: 1, registrationNumber: 'AB12345' }] });
});

test('lists the vehicles returned for the user', async () => {
  render(<VehiclesPage />);
  expect(screen.getByText('Henter køretøjer…')).toBeInTheDocument();
  expect(await screen.findByText('AB12345')).toBeInTheDocument();
});

test('shows an empty state', async () => {
  getVehicles.mockResolvedValue({ ok: true, data: [] });
  render(<VehiclesPage />);
  expect(await screen.findByText('Du har ingen registrerede køretøjer.')).toBeInTheDocument();
});

test('a failed load is reported instead of an empty list', async () => {
  getVehicles.mockResolvedValue({ ok: false, error: 'Data kunne ikke hentes. Prøv igen senere.' });
  render(<VehiclesPage />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Data kunne ikke hentes.');
  expect(screen.queryByText('Du har ingen registrerede køretøjer.')).not.toBeInTheDocument();
});

test('registers a vehicle and confirms it', async () => {
  registerVehicle.mockResolvedValue({ ok: true, data: { id: 2, registrationNumber: 'CD12345' } });
  render(<VehiclesPage />);
  await screen.findByText('AB12345');
  userEvent.type(screen.getByLabelText('Registreringsnummer'), ' CD12345 ');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByText('CD12345')).toBeInTheDocument();
  expect(registerVehicle).toHaveBeenCalledWith('CD12345');
  expect(screen.getByRole('status')).toHaveTextContent('Køretøj CD12345 er registreret.');
});

test('a rejected registration shows the failure and adds nothing', async () => {
  registerVehicle.mockResolvedValue({ ok: false, error: 'Registreringsnummeret er ugyldigt, ukendt eller allerede registreret.' });
  render(<VehiclesPage />);
  await screen.findByText('AB12345');
  userEvent.type(screen.getByLabelText('Registreringsnummer'), 'ZZ99999');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('ukendt');
  expect(screen.queryByText('ZZ99999')).not.toBeInTheDocument();
});

test('a blank registration number is not submitted', async () => {
  render(<VehiclesPage />);
  await screen.findByText('AB12345');
  userEvent.click(screen.getByRole('button', { name: 'Registrer køretøj' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Registreringsnummer mangler.');
  expect(registerVehicle).not.toHaveBeenCalled();
});

test('deletes a vehicle after confirmation', async () => {
  deleteVehicle.mockResolvedValue({ ok: true, data: null });
  render(<VehiclesPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet AB12345' }));
  expect(deleteVehicle).not.toHaveBeenCalled();
  userEvent.click(screen.getByRole('button', { name: 'Bekræft sletning af AB12345' }));
  await waitFor(() => expect(screen.queryByText('AB12345')).not.toBeInTheDocument());
  expect(deleteVehicle).toHaveBeenCalledWith(1);
  expect(screen.getByRole('status')).toHaveTextContent('Køretøj AB12345 er slettet.');
  expect(screen.getByText('Du har ingen registrerede køretøjer.')).toBeInTheDocument();
});

test('cancelling the confirmation keeps the vehicle', async () => {
  render(<VehiclesPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet AB12345' }));
  userEvent.click(screen.getByRole('button', { name: 'Annuller' }));
  expect(screen.getByRole('button', { name: 'Slet AB12345' })).toBeInTheDocument();
  expect(deleteVehicle).not.toHaveBeenCalled();
});

test('a failed deletion is reported and the vehicle stays', async () => {
  deleteVehicle.mockResolvedValue({ ok: false, error: 'Køretøjet kan ikke slettes, fordi det har reservationer.' });
  render(<VehiclesPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet AB12345' }));
  userEvent.click(screen.getByRole('button', { name: 'Bekræft sletning af AB12345' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('har reservationer');
  expect(screen.getByText('AB12345')).toBeInTheDocument();
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});
