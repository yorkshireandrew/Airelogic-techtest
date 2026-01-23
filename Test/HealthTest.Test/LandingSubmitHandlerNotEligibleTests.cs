using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthTest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HealthTest.Test
{
    public class LandingSubmitHandlerNotEligibleTests
    {
        [Fact]
        public async Task Handle_AgeBandMinusOne_ReturnsNotEligibleRedirect()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1112223339"}, // 10-digit input; handler will trim to 9
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "2000"}
            };

            var ctx = CreateContextWithForm(dict);

            var patient = new PatientModel
            {
                nhsNumber = "111222333",
                name = "Smith, John",
                born = "01-02-2000"
            };

            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(c => c.GetPatientFromNhsNumberAsync(It.IsAny<string>())).ReturnsAsync(patient);

            var ageBandMock = new Mock<IAgeBandCalculator>();
            ageBandMock.Setup(a => a.CalculateAgeBand(It.IsAny<int>())).Returns(-1);

            var handler = new LandingSubmitHandler(apiMock.Object, ageBandMock.Object);

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=You%20are%20not%20eligible%20for%20this%20service", redirect.Url);
        }

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
    }
}
