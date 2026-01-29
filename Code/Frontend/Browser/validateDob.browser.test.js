// Lightweight browser tests for validateDob
const tests = [];
function addTest(name, fn) { tests.push({ name, fn }); }

addTest('daysInMonth leap year', () => {
  const result = ValidateDob.daysInMonth(2020, 2);
  if (result !== 29) throw new Error('expected 29');
});

addTest('daysInMonth feb non-leap', () => {
  const result = ValidateDob.daysInMonth(2019, 2);
  if (result !== 28) throw new Error('expected 28');
});

addTest('validateDob valid', () => {
  if (!ValidateDob.validateDob('01', '02', '1990')) throw new Error('should be valid');
});

addTest('validateDob invalid day', () => {
  if (ValidateDob.validateDob('31', '02', '1990')) throw new Error('should be invalid');
});

addTest('validateDob future', () => {
  const future = new Date().getFullYear() + 1;
  if (ValidateDob.validateDob('01', '01', String(future))) throw new Error('should be invalid (future)');
});

// Expose tests list for the runner
window.__BROWSER_TESTS__ = tests;
