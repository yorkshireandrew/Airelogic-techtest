// Lightweight browser tests for LandingValidators
const tests = [];
function addTest(name, fn) { tests.push({ name, fn }); }

addTest('daysInMonth leap year', () => {
  const result = LandingValidators.daysInMonth(2020, 2);
  if (result !== 29) throw new Error('expected 29');
});

addTest('validateDob valid', () => {
  const r = LandingValidators.validateDobValues('01', '02', '1990');
  if (!r.valid) throw new Error('should be valid');
});

addTest('validateDob invalid day', () => {
  const r = LandingValidators.validateDobValues('31', '02', '1990');
  if (r.valid) throw new Error('should be invalid');
});

addTest('validateDob future', () => {
  const future = new Date().getFullYear() + 1;
  const r = LandingValidators.validateDobValues('01', '01', String(future));
  if (r.valid) throw new Error('should be invalid (future)');
});

window.__BROWSER_TESTS__ = (window.__BROWSER_TESTS__ || []).concat(tests);
