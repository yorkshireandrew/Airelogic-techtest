// Validator utilities for LandingPage, testable in-browser
function daysInMonth(y, m) {
  return new Date(y, m, 0).getDate();
}

function validateDobValues(d, m, y) {
  const di = Number.parseInt(d, 10);
  const mi = Number.parseInt(m, 10);
  const yi = Number.parseInt(y, 10);
  if (Number.isNaN(di) || Number.isNaN(mi) || Number.isNaN(yi)) {
    return { valid: false, field: 'day', message: 'Please enter a valid numeric date of birth' };
  }
  if (mi < 1 || mi > 12) {
    return { valid: false, field: 'month', message: 'Month must be between 1 and 12' };
  }
  const dim = daysInMonth(yi, mi);
  if (di < 1 || di > dim) {
    return { valid: false, field: 'day', message: 'Day is invalid for the selected month and year' };
  }
  const dob = new Date(yi, mi - 1, di);
  const today = new Date();
  if (dob > today) {
    return { valid: false, field: 'day', message: 'Date of birth cannot be in the future' };
  }
  return { valid: true };
}

// CommonJS export for Node/Jest
if (typeof module !== 'undefined' && typeof module.exports !== 'undefined') {
  module.exports = { validateDobValues, daysInMonth };
}

// Browser global
if (typeof window !== 'undefined') {
  try { window.LandingValidators = { validateDobValues, daysInMonth }; } catch (e) {}
}
