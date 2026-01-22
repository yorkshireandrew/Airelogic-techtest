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
            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), logger, config);

            await handler.Handle(ctx);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("bad")); // Logged NHS number
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
            var config = new AppSettings { LogPersonallyIdentifiableData = false }; // PID logging disabled
            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), logger, config);

            await handler.Handle(ctx);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message == "Invalid NHS number format received."); // Logged generic message
        }

        [Fact]
        public async Task Handle_PatientNotFound_ReturnsRedirectWithEncodedMessage()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"nhs", "1112223339"}, // Valid format
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "02"},
                {"dob_year", "1990"}
            };

            var ctx = CreateContextWithForm(dict); 
            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub()); // Always returns null patient

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
        }

        [Fact]
        public async Task Handle_InvalidNhsNumber_ReturnsRedirect()
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
            var config = new AppSettings { InformUserWhenNhsNumberFormatIncorrect = false }; // Setting for generic message
            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), null, config); // Always returns null patient

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=Your%20details%20could%20not%20be%20found", redirect.Url);
        }

        [Fact]
        public async Task Handle_InvalidNhsNumber_And_InformUserWhenNhsNumberFormatIncorrectTrue_ReturnsRedirect()
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
            var config = new AppSettings { InformUserWhenNhsNumberFormatIncorrect = true }; // Setting for helpful message
            var handler = new LandingSubmitHandler(new AlwaysReturnsNullApiClientStub(), null, config); // Always returns null patient

            var result = await handler.Handle(ctx);

            var redirect = Assert.IsType<RedirectHttpResult>(result);
            Assert.Equal("/Answer?message=NHS%20number%20format%20is%20incorrect", redirect.Url);
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
