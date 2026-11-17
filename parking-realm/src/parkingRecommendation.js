export function gridDistance(space, reference) {
  return Math.abs(space.row - reference.row) + Math.abs(space.column - reference.column);
}

export function matchesRequirements(space, {
  requiresAccessible = false, requiresCharger = false, isElectricVehicle = false,
} = {}) {
  return Boolean(space.isAccessible) === requiresAccessible &&
    (!requiresCharger || Boolean(space.hasCharger)) && (!space.hasCharger || isElectricVehicle);
}

export function recommendSpace(spaces, preferredSpaceId = '', requirements = {}) {
  const reference = preferredSpaceId
    ? spaces.find((space) => String(space.id) === String(preferredSpaceId)) : null;
  if (preferredSpaceId && !reference) return null;
  const available = spaces.filter((space) => space.isAvailable && matchesRequirements(space, requirements));
  available.sort((first, second) =>
    (reference ? gridDistance(first, reference) - gridDistance(second, reference) : 0) || first.id - second.id);
  const space = available[0];
  return space ? { space, distance: reference ? gridDistance(space, reference) : null } : null;
}
