export const PASSWORD_MIN_LENGTH = 8;
export const NAME_MAX_LENGTH = 100;

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+$/;
// E.164: '+' followed by country code and number, at most 15 digits.
const PHONE_REGEX = /^\+[1-9]\d{1,14}$/;

export function validateRegistration(values) {
  const errors = {};
  const email = values.email.trim();

  if (!email) errors.email = 'Email mangler.';
  else if (!EMAIL_REGEX.test(email)) errors.email = 'Email er ikke en gyldig emailadresse.';

  if (!values.password) {
    errors.password = 'Adgangskode mangler.';
  } else if (
    values.password.length < PASSWORD_MIN_LENGTH ||
    !/\d/.test(values.password) ||
    !/[a-z]/.test(values.password) ||
    !/[A-Z]/.test(values.password) ||
    !/[^a-zA-Z0-9]/.test(values.password)
  ) {
    errors.password = `Adgangskoden skal være mindst ${PASSWORD_MIN_LENGTH} tegn og indeholde et tal, et stort bogstav, et lille bogstav og et specialtegn.`;
  }

  validateName(errors, 'firstName', 'Fornavn', values.firstName);
  validateName(errors, 'lastName', 'Efternavn', values.lastName);

  const phone = values.phoneNumber.trim();
  if (!phone) errors.phoneNumber = 'Telefonnummer mangler.';
  else if (!PHONE_REGEX.test(phone))
    errors.phoneNumber = 'Telefonnummer skal starte med landekode, fx +4512345678.';

  return errors;
}

function validateName(errors, key, label, value) {
  const name = value.trim();
  if (!name) errors[key] = `${label} mangler.`;
  else if (name.length > NAME_MAX_LENGTH)
    errors[key] = `${label} må højst være ${NAME_MAX_LENGTH} tegn.`;
}
