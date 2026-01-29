using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HealthTest.Test
{
    public class LandingSubmitHandlerValidateNhsCheckDigitTests
    {
        private DefaultHttpContext CreateContextWithForm(Dictionary<string, StringValues> values)
        {
            var ctx = new DefaultHttpContext();
            var form = new FormCollection(values);
            try
            {
                ctx.Request.Form = form;
            }
            catch
            {
                ctx.Features.Set(new Microsoft.AspNetCore.Http.Features.FormFeature(form));
            }

            return ctx;
        }

        [Fact]
        public async Task Handle_ValidateNhsCheckDigitTrue_UsesCheckDigitValidation()
        {
            // Arrange: use a 10-digit NHS with an invalid check digit (last digit different)
            var invalidNhs = "1111111112"; // calculated check digit for 111111111? is 1, so 2 makes it invalid

            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", invalidNhs},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);

            var parserMock = new Mock<ILandingFormParser>();
            parserMock.Setup(p => p.Parse(It.IsAny<IFormCollection>())).Returns(new LandingFormModel
            {
                nhs = invalidNhs,
                surname = "Smith",
                day = "01",
                month = "02",
                year = "1990"
            });

            var config = new AppSettings { ValidateNhsCheckDigit = true, InformUserWhenNhsNumberFormatIncorrect = true };

            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), new AgeBandCalculator(config), null, config, parserMock.Object);

            // Act
            var result = await handler.Handle(ctx);

            // Assert: when check digit validation is enabled, the invalid NHS triggers the helpful invalid format response
            var json = Assert.IsType<LandingSubmitHandlerResponseJson>(result);
            Assert.Equal("NHS number format is incorrect", json.Message);
        }

        [Fact]
        public async Task Handle_ValidateNhsCheckDigitFalse_SkipsCheckDigitValidation()
        {
            // Arrange: same NHS but check digit validation disabled
            var nhs = "1111111112";

            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", nhs},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);

            var parserMock = new Mock<ILandingFormParser>();
            parserMock.Setup(p => p.Parse(It.IsAny<IFormCollection>())).Returns(new LandingFormModel
            {
                nhs = nhs,
                surname = "Smith",
                day = "01",
                month = "02",
                year = "1990"
            });

            var config = new AppSettings { ValidateNhsCheckDigit = false, InformUserWhenNhsNumberFormatIncorrect = false, PatientNotFoundMessage = "Your details could not be found" };

            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), new AgeBandCalculator(config), null, config, parserMock.Object);

            // Act
            var result = await handler.Handle(ctx);

            // Assert: when check digit validation is disabled, the handler proceeds to API lookup and (stub) returns patient-not-found
            var json = Assert.IsType<LandingSubmitHandlerResponseJson>(result);
            Assert.Equal("Your details could not be found", json.Message);
        }
    }
}
