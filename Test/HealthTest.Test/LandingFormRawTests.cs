using HealthTest;
using Xunit;

namespace HealthTest.Test
{
    public class LandingFormRawTests
    {
        [Theory]
        [InlineData("1234567890", true)]
        [InlineData("0000000000", true)]
        [InlineData("12345", false)]
        [InlineData("ABCDEFGHIJ", false)]
        [InlineData("123456789a", false)]
        public void NhsIsValidFormat_Works(string input, bool expected)
        {
            var lf = new LandingFormRaw();
            var actual = lf.NhsIsValidFormat(input);
            Assert.Equal(expected, actual);
        }
    }
}
