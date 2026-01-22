using HealthTest;
using Xunit;

namespace HealthTest.Test
{
    public class LandingFormModelTests
    {
        [Theory]
        [InlineData("1234567890", 10,true)]
        [InlineData("0000000000", 10,true)]
        [InlineData("12345", 10,false)]
        [InlineData("ABCDEFGHIJ", 10,false)]
        [InlineData("123456789a", 10,false)]
        public void NhsIsValidFormat_Works(string input, int expectedLength,bool expected)
        {
            var lf = new LandingFormModel();
            var actual = lf.NhsIsValidFormat(input);
            Assert.Equal(expected, actual);
        }
    }
}
