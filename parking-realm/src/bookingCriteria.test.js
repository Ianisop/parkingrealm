import { endForDuration, getBookingDefaults, toLocalInputValue, validateCriteria } from './bookingCriteria';

test('defaults allow booking today with a submission buffer and one hour duration', () => {
  const now = new Date(2026, 5, 10, 10, 4, 30).getTime();
  const { start, end } = getBookingDefaults(now);
  expect(start).toBe('2026-06-10T10:10');
  expect(end).toBe('2026-06-10T11:10');
  expect(Date.parse(start) - now).toBeGreaterThanOrEqual(60000);
  expect(validateCriteria(start, end, now)).toBe('');
});

test('the next valid slot rolls into tomorrow near midnight', () => {
  const now = new Date(2026, 5, 10, 23, 59, 30).getTime();
  const { start, end } = getBookingDefaults(now);
  expect(start).toBe('2026-06-11T00:05');
  expect(end).toBe('2026-06-11T01:05');
  expect(validateCriteria(start, end, now)).toBe('');
});

test('formats browser local time without using UTC calendar fields', () => {
  expect(toLocalInputValue(new Date(2026, 0, 2, 3, 4))).toBe('2026-01-02T03:04');
  expect(endForDuration('2026-06-10T23:00', 2)).toBe('2026-06-11T01:00');
  expect(endForDuration('', 1)).toBe('');
});

test('validation distinguishes missing, invalid, elapsed and reversed times', () => {
  const now = new Date(2026, 5, 10, 10).getTime();
  expect(validateCriteria('', '', now)).toBe('Angiv start og slut.');
  expect(validateCriteria('invalid', '2026-06-10T12:00', now)).toMatch(/gyldige/);
  expect(validateCriteria('2026-06-10T10:00', '2026-06-10T11:00', now)).toMatch(/passeret/);
  expect(validateCriteria('2026-06-10T11:00', '2026-06-10T11:00', now)).toBe('Slut skal være efter start.');
  expect(validateCriteria('2026-06-10T11:00', '2026-06-10T10:00', now)).toBe('Slut skal være efter start.');
});
