import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UsersPage from './UsersPage';
import { createUser, deleteUser, getUsers, updateUser } from './api/booking';

jest.mock('./api/booking');

const anna = { id: 'u1', email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen', phoneNumber: '+4512345678', isAdministrator: false };
const admin = { id: 'a1', email: 'root@example.com', firstName: 'Root', lastName: 'Admin', phoneNumber: '+4500000000', isAdministrator: true };

beforeEach(() => {
  jest.clearAllMocks();
  getUsers.mockResolvedValue({ ok: true, data: [anna, admin] });
});

function fillCreateForm(overrides = {}) {
  const values = { Email: 'bo@example.com', Adgangskode: 'Passw0rd!', Fornavn: 'Bo', Efternavn: 'Jensen', Telefonnummer: '+4511223344', ...overrides };
  Object.entries(values).forEach(([label, value]) => {
    const input = screen.getByLabelText(label);
    userEvent.clear(input);
    if (value) userEvent.type(input, value);
  });
}

test('lists users and protects administrator rows', async () => {
  render(<UsersPage />);
  expect(screen.getByText('Henter brugere…')).toBeInTheDocument();
  expect(await screen.findByText('anna@example.com')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Rediger anna@example.com' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Slet anna@example.com' })).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /root@example.com/ })).not.toBeInTheDocument();
  expect(screen.getByText('Kan ikke ændres her')).toBeInTheDocument();
});

test('shows an empty state and a failed load is not shown as empty', async () => {
  getUsers.mockResolvedValueOnce({ ok: true, data: [] });
  const { unmount } = render(<UsersPage />);
  expect(await screen.findByText('Ingen brugere fundet.')).toBeInTheDocument();
  unmount();

  getUsers.mockResolvedValueOnce({ ok: false, error: 'Brugere kunne ikke hentes. Prøv igen senere.' });
  render(<UsersPage />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Brugere kunne ikke hentes.');
  expect(screen.queryByText('Ingen brugere fundet.')).not.toBeInTheDocument();
});

test('searches by the entered text', async () => {
  render(<UsersPage />);
  await screen.findByText('anna@example.com');
  userEvent.type(screen.getByLabelText('Søg efter navn eller email'), 'anna');
  userEvent.click(screen.getByRole('button', { name: 'Søg' }));
  await waitFor(() => expect(getUsers).toHaveBeenLastCalledWith('anna'));
});

test('creates a user, confirms it and reloads the list', async () => {
  createUser.mockResolvedValue({ ok: true, data: { id: 'u2', email: 'bo@example.com' } });
  render(<UsersPage />);
  await screen.findByText('anna@example.com');
  userEvent.click(screen.getByRole('button', { name: 'Opret bruger' }));
  fillCreateForm();
  userEvent.click(screen.getByRole('button', { name: 'Opret' }));

  expect(await screen.findByRole('status')).toHaveTextContent('Brugeren bo@example.com er oprettet.');
  expect(createUser).toHaveBeenCalledWith({
    email: 'bo@example.com', password: 'Passw0rd!', firstName: 'Bo', lastName: 'Jensen', phoneNumber: '+4511223344',
  });
  expect(getUsers).toHaveBeenCalledTimes(2);
  expect(screen.queryByRole('form', { name: 'Opret bruger' })).not.toBeInTheDocument();
});

test('invalid details are not sent and are explained', async () => {
  render(<UsersPage />);
  await screen.findByText('anna@example.com');
  userEvent.click(screen.getByRole('button', { name: 'Opret bruger' }));
  fillCreateForm({ Email: 'not-an-email', Adgangskode: 'short', Fornavn: '', Telefonnummer: '12345678' });
  userEvent.click(screen.getByRole('button', { name: 'Opret' }));

  expect(await screen.findByText('Email er ikke en gyldig emailadresse.')).toBeInTheDocument();
  expect(screen.getByText('Fornavn mangler.')).toBeInTheDocument();
  expect(screen.getByText(/Adgangskoden skal være/)).toBeInTheDocument();
  expect(screen.getByText('Telefonnummer skal starte med landekode, fx +4512345678.')).toBeInTheDocument();
  expect(createUser).not.toHaveBeenCalled();
});

test('API field errors and failures are shown and nothing is reported as created', async () => {
  createUser.mockResolvedValue({
    ok: false, error: 'Oplysningerne er ugyldige. Kontrollér felterne og prøv igen.', fieldErrors: { email: ['Email is already in use.'] },
  });
  render(<UsersPage />);
  await screen.findByText('anna@example.com');
  userEvent.click(screen.getByRole('button', { name: 'Opret bruger' }));
  fillCreateForm();
  userEvent.click(screen.getByRole('button', { name: 'Opret' }));

  expect(await screen.findByText('Email is already in use.')).toBeInTheDocument();
  expect(screen.getAllByRole('alert').some((alert) => alert.textContent.includes('Oplysningerne er ugyldige'))).toBe(true);
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});

test('edits a user without asking for a password', async () => {
  updateUser.mockResolvedValue({ ok: true, data: { ...anna, firstName: 'Anne' } });
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Rediger anna@example.com' }));
  expect(screen.queryByLabelText('Adgangskode')).not.toBeInTheDocument();
  expect(screen.getByLabelText('Fornavn')).toHaveValue('Anna');
  userEvent.clear(screen.getByLabelText('Fornavn'));
  userEvent.type(screen.getByLabelText('Fornavn'), 'Anne');
  userEvent.click(screen.getByRole('button', { name: 'Gem ændringer' }));

  expect(await screen.findByRole('status')).toHaveTextContent('Brugeren anna@example.com er opdateret.');
  expect(updateUser).toHaveBeenCalledWith('u1', {
    email: 'anna@example.com', firstName: 'Anne', lastName: 'Hansen', phoneNumber: '+4512345678',
  });
});

test('an invalid edit is not sent', async () => {
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Rediger anna@example.com' }));
  userEvent.clear(screen.getByLabelText('Efternavn'));
  userEvent.click(screen.getByRole('button', { name: 'Gem ændringer' }));
  expect(await screen.findByText('Efternavn mangler.')).toBeInTheDocument();
  expect(updateUser).not.toHaveBeenCalled();
});

test('a failed edit shows the failure', async () => {
  updateUser.mockResolvedValue({ ok: false, error: 'Brugeren findes ikke længere.', fieldErrors: {} });
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Rediger anna@example.com' }));
  userEvent.click(screen.getByRole('button', { name: 'Gem ændringer' }));
  expect(await screen.findByText('Brugeren findes ikke længere.')).toBeInTheDocument();
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});

test('deletion requires confirmation and can be cancelled', async () => {
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet anna@example.com' }));
  expect(deleteUser).not.toHaveBeenCalled();
  userEvent.click(screen.getByRole('button', { name: 'Annuller' }));
  expect(screen.getByRole('button', { name: 'Slet anna@example.com' })).toBeInTheDocument();
  expect(deleteUser).not.toHaveBeenCalled();
});

test('confirmed deletion removes the user and reports it', async () => {
  deleteUser.mockResolvedValue({ ok: true, data: null });
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet anna@example.com' }));
  getUsers.mockResolvedValue({ ok: true, data: [admin] });
  userEvent.click(screen.getByRole('button', { name: 'Bekræft sletning af anna@example.com' }));

  expect(await screen.findByRole('status')).toHaveTextContent('Brugeren anna@example.com er slettet.');
  expect(deleteUser).toHaveBeenCalledWith('u1');
  await waitFor(() => expect(screen.queryByText('anna@example.com')).not.toBeInTheDocument());
  expect(within(screen.getByRole('table')).getByText('root@example.com')).toBeInTheDocument();
});

test('a failed deletion is reported', async () => {
  deleteUser.mockResolvedValue({ ok: false, error: 'Brugeren kunne ikke slettes. Prøv igen senere.' });
  render(<UsersPage />);
  userEvent.click(await screen.findByRole('button', { name: 'Slet anna@example.com' }));
  userEvent.click(screen.getByRole('button', { name: 'Bekræft sletning af anna@example.com' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Brugeren kunne ikke slettes.');
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});
