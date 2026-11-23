export function toLocalInputValue(date) {
  const pad = (value) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function getBookingDefaults(now = Date.now(), durationHours = 1) {
  const start = Math.ceil((now + 60000) / 300000) * 300000;
  return {
    start: toLocalInputValue(new Date(start)),
    end: toLocalInputValue(new Date(start + durationHours * 3600000)),
  };
}

export function endForDuration(start, durationHours) {
  const instant = Date.parse(start);
  return Number.isFinite(instant)
    ? toLocalInputValue(new Date(instant + durationHours * 3600000)) : '';
}

export function validateCriteria(start, end, now = Date.now()) {
  if (!start || !end) return 'Angiv start og slut.';
  const from = Date.parse(start);
  const until = Date.parse(end);
  if (!Number.isFinite(from) || !Number.isFinite(until)) return 'Angiv gyldige datoer og tidspunkter.';
  if (from <= now) return 'Starttidspunktet er passeret. Vælg et nyt tidspunkt, også gerne i dag.';
  if (until <= from) return 'Slut skal være efter start.';
  return '';
}
