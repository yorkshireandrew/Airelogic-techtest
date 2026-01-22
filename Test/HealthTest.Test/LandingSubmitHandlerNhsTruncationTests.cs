using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using HealthTest;
using Microsoft.AspNetCore.Http.Features;
using System.Threading;

namespace HealthTest.Test
{
    public class LandingSubmitHandlerNhsTruncationTests
    {
        [Fact]
        public async Task Handle_Truncates10DigitNhsNumberTo9Digits()
        {
            // Arrange
            var mockApiClient = new Mock<IApiClient>();
            var mockLogger = new Mock<ILogger<LandingSubmitHandler>>();
            var config = new AppSettings();
            var handler = new LandingSubmitHandler(mockApiClient.Object, mockLogger.Object, config);

            // Use a valid 10-digit NHS number (Modulus 11): 9434765919
            var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                {"nhs", "9434765919"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "01"},
                {"dob_year", "1990"}
            });

            var context = new DefaultHttpContext();
            context.Features.Set<IFormFeature>(new TestFormFeature(formCollection));

            string? sentNhs = null;
            mockApiClient.Setup(x => x.GetPatientFromNhsNumberAsync(It.IsAny<string>()))
                .Callback<string>(nhs => sentNhs = nhs)
                .ReturnsAsync(new PatientModel());

            // Act
            await handler.Handle(context);

            // Assert
            Assert.Equal("943476591", sentNhs);
        }

        [Fact]
        public async Task Handle_DoesNotTruncateIfNhsNumberIs9Digits()
        {
            // Arrange
            var mockApiClient = new Mock<IApiClient>();
            var mockLogger = new Mock<ILogger<LandingSubmitHandler>>();
            var config = new AppSettings();
            var handler = new LandingSubmitHandler(mockApiClient.Object, mockLogger.Object, config);

            var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                {"nhs", "987654321"},
                {"surname", "Smith"},
                {"dob_day", "01"},
                {"dob_month", "01"},
                {"dob_year", "1990"}
            });

            var context = new DefaultHttpContext();
            context.Features.Set<IFormFeature>(new TestFormFeature(formCollection));

            string? sentNhs = null;
            mockApiClient.Setup(x => x.GetPatientFromNhsNumberAsync(It.IsAny<string>()))
                .Callback<string>(nhs => sentNhs = nhs)
                .ReturnsAsync(new PatientModel());

            // Act
            await handler.Handle(context);

            // Assert
            Assert.Equal("987654321", sentNhs);
        }

        // Helper class to mock IFormFeature for tests
        public class TestFormFeature : IFormFeature
        {
            private readonly IFormCollection _form;
            public TestFormFeature(IFormCollection form) { _form = form; }
            public IFormCollection Form { get => _form; set { } }
            public IFormCollection ReadForm() => _form;
            public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default) => Task.FromResult(_form);
            public bool HasFormContentType => true;
        }
    }
}
