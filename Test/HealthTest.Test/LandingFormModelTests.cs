using HealthTest;
using Xunit;

namespace HealthTest.Test
{
    public class LandingFormModelTests
    {
        [Theory]
        [InlineData("111222333", true)] // 9 digits
        [InlineData("1112223339", true)] // 10 digits, valid check
        [InlineData("9434765919", true)] // 10 digits, valid check
        [InlineData("9876543210", true)] // 10 digits, valid check
        [InlineData("5554443338", true)] // 10 digits, valid check
        [InlineData("2468135792", true)] // 10 digits, valid check
        [InlineData("12345", false)]
        [InlineData("ABCDEFGHIJ", false)]
        [InlineData("123456789a", false)]
        public void NhsIsValidFormat_Works(string input, bool expected)
        {
            var lf = new LandingFormModel();
            var actual = lf.NhsIsValid(input);
            Assert.Equal(expected, actual);
        }
    }
}
