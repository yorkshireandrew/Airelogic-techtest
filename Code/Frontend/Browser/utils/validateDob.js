function daysInMonth(y, m) {
  return new Date(y, m, 0).getDate();
}

function validateDob(d, m, y) {
  const di = Number.parseInt(d, 10);
  const mi = Number.parseInt(m, 10);
  const yi = Number.parseInt(y, 10);
  if (Number.isNaN(di) || Number.isNaN(mi) || Number.isNaN(yi)) return false;
  if (mi < 1 || mi > 12) return false;
  const dim = daysInMonth(yi, mi);
  if (di < 1 || di > dim) return false;
  const dob = new Date(yi, mi - 1, di);
  const today = new Date();
  if (dob > today) return false;
  return true;
}

// CommonJS export for Node/Jest (guarded for browsers)
if (typeof module !== 'undefined' && typeof module.exports !== 'undefined') {
  module.exports = { validateDob, daysInMonth };
}

// Browser global export for the lightweight test runner
if (typeof window !== 'undefined') {
  try {
    window.ValidateDob = { validateDob, daysInMonth };
  } catch (e) { }
}
