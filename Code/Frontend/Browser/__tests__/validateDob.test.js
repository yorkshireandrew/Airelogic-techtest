const { validateDob, daysInMonth } = require('../utils/validateDob');

test('daysInMonth returns correct days', () => {
  expect(daysInMonth(2020, 2)).toBe(29); // leap year
  expect(daysInMonth(2021, 2)).toBe(28);
  expect(daysInMonth(2021, 1)).toBe(31);
});

test('validateDob accepts valid date and rejects invalid', () => {
  expect(validateDob('01', '02', '1990')).toBe(true);
  expect(validateDob('31', '02', '1990')).toBe(false);
  expect(validateDob('a', 'b', 'c')).toBe(false);
  const futureYear = new Date().getFullYear() + 1;
  expect(validateDob('01', '01', String(futureYear))).toBe(false);
});
