namespace HealthTest;

public class AgeBandCalculator : IAgeBandCalculator
{
    private List<List<int>> _ageBands;

    public AgeBandCalculator(AppSettings settings)
    {
        var s = settings ?? throw new ArgumentNullException(nameof(settings));
        _ageBands = s.AgeBands ?? throw new ArgumentNullException(nameof(s.AgeBands));
    }

    public int CalculateAgeBand(int age)
    {
        for (int i = 0; i < _ageBands.Count; i++)
        {
            var band = _ageBands[i];
            int lower = band[0];
            int upper = band[1];

            if (age >= lower && age <= upper) return i;
        }

        return -1;
    }
}

