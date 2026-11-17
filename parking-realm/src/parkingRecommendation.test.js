import { gridDistance, matchesRequirements, recommendSpace } from './parkingRecommendation';

const spaces = [
  { id: 1, row: 10, column: 10, isAvailable: true },
  { id: 5, row: 2, column: 3, isAvailable: true },
  { id: 3, row: 3, column: 2, isAvailable: true },
  { id: 9, row: 2, column: 2, isAvailable: false },
];

test('ranks available spots nearest to an occupied anchor and breaks ties by ID', () => {
  const before = [...spaces];
  expect(recommendSpace(spaces, '9')).toEqual({ space: spaces[2], distance: 1 });
  expect(spaces).toEqual(before);
});

test('recommends the preferred spot itself when it is available', () => {
  expect(recommendSpace(spaces, '5')).toEqual({ space: spaces[1], distance: 0 });
});

test('no preference uses lowest available ID for backward compatibility', () => {
  expect(recommendSpace(spaces)).toEqual({ space: spaces[0], distance: null });
});

test('missing anchors and unavailable or empty inventory produce no recommendation', () => {
  expect(recommendSpace(spaces, '99')).toBeNull();
  expect(recommendSpace(spaces.map((space) => ({ ...space, isAvailable: false })), '9')).toBeNull();
  expect(recommendSpace([])).toBeNull();
});

test('grid distances support large coordinates', () => {
  expect(gridDistance({ row: 2147483647, column: 2147483647 },
    { row: -2147483648, column: -2147483648 })).toBe(8589934590);
});

test.each([
  [false, false, 1], [true, false, 2], [false, true, 3], [true, true, 4],
])('requirements accessible=%s charger=%s select only compatible spots', (requiresAccessible, requiresCharger, expected) => {
  const inventory = [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: true, isAccessible: true },
    { id: 3, row: 0, column: 2, isAvailable: true, hasCharger: true },
    { id: 4, row: 0, column: 3, isAvailable: true, isAccessible: true, hasCharger: true },
  ];
  const requirements = { requiresAccessible, requiresCharger, isElectricVehicle: requiresCharger };
  expect(recommendSpace(inventory, '1', requirements)?.space.id).toBe(expected);
  expect(inventory.filter((space) => matchesRequirements(space, requirements)).map((space) => space.id)).toEqual([expected]);
});

test('never relaxes requirements when a matching spot is unavailable', () => {
  const inventory = [
    { id: 1, row: 0, column: 0, isAvailable: true },
    { id: 2, row: 0, column: 1, isAvailable: false, hasCharger: true },
  ];
  expect(recommendSpace(inventory, '', { requiresCharger: true, isElectricVehicle: true })).toBeNull();
  expect(recommendSpace(inventory, '', { requiresAccessible: true, requiresCharger: true, isElectricVehicle: true })).toBeNull();
});

test('an electric vehicle can use standard and charging spots without requiring charging', () => {
  const standard = { id: 2, row: 0, column: 0, isAvailable: true };
  const charging = { id: 3, row: 0, column: 1, isAvailable: true, hasCharger: true };
  const requirements = { isElectricVehicle: true };
  expect(matchesRequirements(standard, requirements)).toBe(true);
  expect(matchesRequirements(charging, requirements)).toBe(true);
  expect(recommendSpace([standard, charging], '3', requirements)?.space.id).toBe(3);
  expect(recommendSpace([standard, charging], '', { ...requirements, requiresCharger: true })?.space.id).toBe(3);
});

test('non-electric vehicles cannot use charging spots or satisfy a charging requirement', () => {
  const charging = { id: 3, row: 0, column: 1, isAvailable: true, hasCharger: true };
  expect(matchesRequirements(charging)).toBe(false);
  expect(matchesRequirements(charging, { requiresCharger: true })).toBe(false);
  expect(recommendSpace([charging])).toBeNull();
});

test('electric eligibility does not bypass the accessible-space requirement', () => {
  const accessibleCharging = { isAccessible: true, hasCharger: true };
  expect(matchesRequirements(accessibleCharging, { isElectricVehicle: true })).toBe(false);
  expect(matchesRequirements(accessibleCharging, { isElectricVehicle: true, requiresAccessible: true })).toBe(true);
});
