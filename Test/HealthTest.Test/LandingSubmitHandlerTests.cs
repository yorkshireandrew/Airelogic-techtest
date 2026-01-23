using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace HealthTest.Test
{
    public class LandingSubmitHandlerTests
    {
        [Fact]
        public void LandingFormParser_Parse_ParsesFormValues()
        {
            // Values have trailing spaces to test trimming
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1234567890 "},
                {"surname", "Smith "},
                {"dob_day", "01 "},
                {"dob_month", "02 "},
                {"dob_year", "1990 "}
            };

            var form = new FormCollection(dict);
            var parser = new LandingFormParser();
            var landing = parser.Parse(form);

            Assert.Equal("1234567890", landing.nhs);
            Assert.Equal("Smith", landing.surname);
            Assert.Equal("01", landing.day);
            Assert.Equal("02", landing.month);
            Assert.Equal("1990", landing.year);
        }
    }
}
