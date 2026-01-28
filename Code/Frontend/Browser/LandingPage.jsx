const { useEffect, useRef } = React;

function LandingPage({ visible = true }) {
  const formRef = useRef(null);
  const nhsRef = useRef(null);
  const surnameRef = useRef(null);
  const dayRef = useRef(null);
  const monthRef = useRef(null);
  const yearRef = useRef(null);

  useEffect(() => {
    try {
      if (formRef.current) formRef.current.reset();
    } catch (e) {}
  }, []);

  if (!visible) return null;

  const clearDobValidity = () => {
    if (dayRef.current) dayRef.current.setCustomValidity('');
    if (monthRef.current) monthRef.current.setCustomValidity('');
    if (yearRef.current) yearRef.current.setCustomValidity('');
  };

  const daysInMonth = (y, m) => new Date(y, m, 0).getDate();

  const validateDob = () => {
    const dEl = dayRef.current;
    const mEl = monthRef.current;
    const yEl = yearRef.current;
    if (!dEl || !mEl || !yEl) return true;
    const d = parseInt(dEl.value, 10);
    const m = parseInt(mEl.value, 10);
    const y = parseInt(yEl.value, 10);

    if (Number.isNaN(d) || Number.isNaN(m) || Number.isNaN(y)) {
      dEl.setCustomValidity('Please enter a valid numeric date of birth');
      return false;
    }

    if (m < 1 || m > 12) {
      mEl.setCustomValidity('Month must be between 1 and 12');
      return false;
    }

    const dim = daysInMonth(y, m);
    if (d < 1 || d > dim) {
      dEl.setCustomValidity('Day is invalid for the selected month and year');
      return false;
    }

    const dob = new Date(y, m - 1, d);
    const today = new Date();
    if (dob > today) {
      dEl.setCustomValidity('Date of birth cannot be in the future');
      return false;
    }

    clearDobValidity();
    return true;
  };

  const handleSubmit = (e) => {
    if (nhsRef.current) nhsRef.current.value = nhsRef.current.value.trim();
    if (surnameRef.current) surnameRef.current.value = surnameRef.current.value.trim();

    if (surnameRef.current && surnameRef.current.value === '') {
      surnameRef.current.setCustomValidity('Please enter your surname');
      e.preventDefault();
      if (surnameRef.current.reportValidity) surnameRef.current.reportValidity();
      return;
    }

    if (nhsRef.current) {
      const v = nhsRef.current.value;
      if (!/^\d{9,10}$/.test(v)) {
        nhsRef.current.setCustomValidity('NHS number must be a 9 or 10-digit numeric value');
        e.preventDefault();
        if (nhsRef.current.reportValidity) nhsRef.current.reportValidity();
        return;
      }
    }

    clearDobValidity();
    if (!validateDob()) {
      e.preventDefault();
      if (dayRef.current && dayRef.current.reportValidity) dayRef.current.reportValidity();
    }
  };

  return (
    <div>
      <h1>Health Test Questionnaire</h1>
      <form id="health-form" ref={formRef} method="post" action="/landing-submit" onSubmit={handleSubmit}>
        <div>
          <label htmlFor="nhs">NHS Number</label><br />
          <input id="nhs" name="nhs" type="text" pattern="[0-9]{9,10}" inputMode="numeric" maxLength={10} required placeholder="1234567890" title="Enter a 9- or 10-digit NHS number" ref={nhsRef} onInput={() => nhsRef.current && nhsRef.current.setCustomValidity('')} />
        </div>

        <div>
          <label htmlFor="surname">Surname</label><br />
          <input id="surname" name="surname" type="text" required ref={surnameRef} onInput={() => surnameRef.current && surnameRef.current.setCustomValidity('')} />
        </div>

        <fieldset>
          <legend>Date of Birth</legend>
          <label htmlFor="dob-day">Day</label>
          <input id="dob-day" name="dob_day" type="number" min="1" max="31" required style={{ width: '5rem' }} ref={dayRef} onInput={() => dayRef.current && dayRef.current.setCustomValidity('')} />

          <label htmlFor="dob-month">Month</label>
          <input id="dob-month" name="dob_month" type="number" min="1" max="12" required style={{ width: '5rem' }} ref={monthRef} onInput={() => monthRef.current && monthRef.current.setCustomValidity('')} />

          <label htmlFor="dob-year">Year</label>
          <input id="dob-year" name="dob_year" type="number" min="1800" max="2100" required style={{ width: '6rem' }} ref={yearRef} onInput={() => yearRef.current && yearRef.current.setCustomValidity('')} />
        </fieldset>

        <div>
          <button type="submit">Submit</button>
        </div>
      </form>
    </div>
  );
}
