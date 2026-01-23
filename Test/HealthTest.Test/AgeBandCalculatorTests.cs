using System.Collections.Generic;
using Xunit;

namespace HealthTest.Test
{
    public class AgeBandCalculatorTests
    {
        private AppSettings CreateSettings()
        {
            return new AppSettings
            {
                AgeBands = new List<List<int>>
                {
                    new List<int> {16, 21},
                    new List<int> {22, 40},
                    new List<int> {41, 65},
                    new List<int> {66, 3000}
                }
            };
        }

        [Theory]
        [InlineData(16, 0)]
        [InlineData(21, 0)]
        [InlineData(22, 1)]
        [InlineData(40, 1)]
        [InlineData(41, 2)]
        [InlineData(65, 2)]
        [InlineData(66, 3)]
        [InlineData(3000, 3)]
        public void CalculateAgeBand_InRange_ReturnsExpectedIndex(int age, int expected)
        {
            var settings = CreateSettings();
            var calc = new AgeBandCalculator(settings);

            var idx = calc.CalculateAgeBand(age);

            Assert.Equal(expected, idx);
        }

        [Theory]
        [InlineData(15)]
        [InlineData(3010)]
        [InlineData(-1)]
        public void CalculateAgeBand_OutOfRange_ReturnsMinusOne(int age)
        {
            var settings = CreateSettings();
            var calc = new AgeBandCalculator(settings);

            var idx = calc.CalculateAgeBand(age);

            Assert.Equal(-1, idx);
        }
    }
}
