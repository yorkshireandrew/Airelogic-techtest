using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthTest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Xunit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HealthTest.Test
{
    // Tests for LandingSubmitHandler.Handle method related to logging behavior
    public class LandingSubmitHandlerHandleTests
    {
        [Fact]
        public async Task Handle_LogsInvalidNhs_LogPersonallyIdentifiableData_True()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "bad"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);
            var logger = new TestLogger();
            var config = new AppSettings { LogPersonallyIdentifiableData = true };
            var handler = new LandingSubmitHandler(new StubApiClient(), logger, config);

            await handler.Handle(ctx);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("bad"));
        }

        [Fact]
        public async Task Handle_LogsInvalidNhs_LogPersonallyIdentifiableData_False()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "bad"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);
            var logger = new TestLogger();
            var config = new AppSettings { LogPersonallyIdentifiableData = false };
            var handler = new LandingSubmitHandler(new StubApiClient(), logger, config);

            await handler.Handle(ctx);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message == "Invalid NHS number format received.");
        }

        [Fact]
        public async Task Handle_PatientNotFound_ReturnsRedirectWithEncodedMessage()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1234567890"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict);
            var handler = new LandingSubmitHandler(new StubApiClient());

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
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
