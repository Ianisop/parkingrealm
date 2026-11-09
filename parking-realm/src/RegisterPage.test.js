import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RegisterPage from './RegisterPage';
import { register } from './api/auth';

jest.mock('./api/auth', () => ({
  ...jest.requireActual('./api/auth'),
  register: jest.fn(),
}));

const valid = {
  'Fornavn': 'Anna',
  'Efternavn': 'Hansen',
  'Email': 'anna@example.com',
  'Telefonnummer (med landekode, fx +4512345678)': '+4512345678',
  'Adgangskode': 'Passw0rd!',
};

function fill(overrides = {}) {
  for (const [label, value] of Object.entries({ ...valid, ...overrides })) {
    if (value) userEvent.type(screen.getByLabelText(label), value);
  }
}

function submit() {
  userEvent.click(screen.getByRole('button', { name: 'Opret bruger' }));
}

beforeEach(() => {
  register.mockReset();
});

test('renders all five fields and a submit button', () => {
  render(<RegisterPage />);

  for (const label of Object.keys(valid)) {
    expect(screen.getByLabelText(label)).toBeInTheDocument();
  }
  expect(screen.getByRole('button', { name: 'Opret bruger' })).toBeInTheDocument();
});

test('blocks submit and names the missing details when the form is empty', () => {
  render(<RegisterPage />);

  submit();

  expect(register).not.toHaveBeenCalled();
  expect(screen.getByText('Fornavn mangler.')).toBeInTheDocument();
  expect(screen.getByText('Efternavn mangler.')).toBeInTheDocument();
  expect(screen.getByText('Email mangler.')).toBeInTheDocument();
  expect(screen.getByText('Telefonnummer mangler.')).toBeInTheDocument();
  expect(screen.getByText('Adgangskode mangler.')).toBeInTheDocument();
});

test('blocks submit for an invalid email', () => {
  render(<RegisterPage />);
  fill({ Email: 'not-an-email' });

  submit();

  expect(register).not.toHaveBeenCalled();
  expect(screen.getByText('Email er ikke en gyldig emailadresse.')).toBeInTheDocument();
});

test('blocks submit for a phone number without country code', () => {
  render(<RegisterPage />);
  fill({ 'Telefonnummer (med landekode, fx +4512345678)': '12345678' });

  submit();

  expect(register).not.toHaveBeenCalled();
  expect(screen.getByText(/skal starte med landekode/)).toBeInTheDocument();
});

test('blocks submit for a weak password', () => {
  render(<RegisterPage />);
  fill({ Adgangskode: 'abc' });

  submit();

  expect(register).not.toHaveBeenCalled();
  expect(screen.getByText(/Adgangskoden skal være mindst 8 tegn/)).toBeInTheDocument();
});

test('shows a success message and clears the form after a successful registration', async () => {
  register.mockResolvedValue({ ok: true, fieldErrors: {} });
  render(<RegisterPage />);
  fill();

  submit();

  expect(await screen.findByText('Din profil er oprettet.')).toBeInTheDocument();
  expect(register).toHaveBeenCalledWith(
    expect.objectContaining({ email: 'anna@example.com', firstName: 'Anna', lastName: 'Hansen', phoneNumber: '+4512345678' })
  );
  expect(screen.getByLabelText('Email')).toHaveValue('');
});

test('shows the server error on the email field for a duplicate email', async () => {
  register.mockResolvedValue({ ok: false, fieldErrors: { email: 'Email er allerede i brug.' } });
  render(<RegisterPage />);
  fill();

  submit();

  expect(await screen.findByText('Email er allerede i brug.')).toBeInTheDocument();
  expect(screen.queryByText('Din profil er oprettet.')).not.toBeInTheDocument();
});

test('shows a general failure message when registration fails', async () => {
  register.mockResolvedValue({ ok: false, fieldErrors: {}, generalError: 'Oprettelsen mislykkedes. Prøv igen senere.' });
  render(<RegisterPage />);
  fill();

  submit();

  expect(await screen.findByRole('alert')).toHaveTextContent('Oprettelsen mislykkedes. Prøv igen senere.');
  await waitFor(() => expect(screen.getByRole('button', { name: 'Opret bruger' })).toBeEnabled());
  expect(screen.queryByText('Din profil er oprettet.')).not.toBeInTheDocument();
});
