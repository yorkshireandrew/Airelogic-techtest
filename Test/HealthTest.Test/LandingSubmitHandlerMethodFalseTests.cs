using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HealthTest.Test
{
    public class LandingSubmitHandlerMethodFalseTests
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
        public async Task Handle_SurnameMismatch_ReturnsPatientNotFoundRedirect()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1112223339"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);

            var patientMock = new Mock<IPatientModel>();
            patientMock.Setup(p => p.SurnameMatches(It.IsAny<string>())).Returns(false);
            patientMock.Setup(p => p.NhsNumberMatches(It.IsAny<string>())).Returns(true);
            patientMock.Setup(p => p.DateOfBirthMatches(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(a => a.GetPatientFromNhsNumberAsync(It.IsAny<string>())).ReturnsAsync(patientMock.Object);

            var handler = new LandingSubmitHandler(apiMock.Object, new AgeBandCalculator(new AppSettings()));

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
        }

        [Fact]
        public async Task Handle_NhsNumberMismatch_ReturnsPatientNotFoundRedirect()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1112223339"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);

            var patientMock = new Mock<IPatientModel>();
            patientMock.Setup(p => p.SurnameMatches(It.IsAny<string>())).Returns(true);
            patientMock.Setup(p => p.NhsNumberMatches(It.IsAny<string>())).Returns(false);
            patientMock.Setup(p => p.DateOfBirthMatches(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(a => a.GetPatientFromNhsNumberAsync(It.IsAny<string>())).ReturnsAsync(patientMock.Object);

            var handler = new LandingSubmitHandler(apiMock.Object, new AgeBandCalculator(new AppSettings()));

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
        }

        [Fact]
        public async Task Handle_DateOfBirthMismatch_ReturnsPatientNotFoundRedirect()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1112223339"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);

            var patientMock = new Mock<IPatientModel>();
            patientMock.Setup(p => p.SurnameMatches(It.IsAny<string>())).Returns(true);
            patientMock.Setup(p => p.NhsNumberMatches(It.IsAny<string>())).Returns(true);
            patientMock.Setup(p => p.DateOfBirthMatches(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(a => a.GetPatientFromNhsNumberAsync(It.IsAny<string>())).ReturnsAsync(patientMock.Object);

            var handler = new LandingSubmitHandler(apiMock.Object, new AgeBandCalculator(new AppSettings()));

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
        }
    }
}
