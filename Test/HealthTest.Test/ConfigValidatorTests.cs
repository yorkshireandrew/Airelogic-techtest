using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Linq;
using Xunit;

namespace HealthTest.Test
{
    public class ConfigValidatorTests
    {
        private class CaptureLogger : ILogger<ConfigValidator>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];
            IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter != null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
                Entries.Add((logLevel, message));
            }
            private class NullScope : IDisposable { public static NullScope Instance { get; } = new NullScope(); public void Dispose() { } }
        }

        [Fact]
        public void ValidateApiSettings_Throws_When_ApiEndpointAndSecretMissing()
        {
            var settings = new AppSettings { ApiEndpoint = string.Empty, ApiSecret = string.Empty };
            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateApiSettings());
            Assert.Contains("ApiEndpoint", ex.Message);
            Assert.Contains("ApiSecret", ex.Message);
        }

        [Fact]
        public void ValidateApiSettings_Throws_When_ApiEndpointMissing()
        {
            var settings = new AppSettings { ApiEndpoint = "  ", ApiSecret = "secret" };
            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateApiSettings());
            Assert.Contains("ApiEndpoint", ex.Message);
            Assert.DoesNotContain("ApiSecret", ex.Message);
        }

        [Fact]
        public void ValidateApiSettings_DoesNotThrow_When_BothPresent()
        {
            var settings = new AppSettings { ApiEndpoint = "http://example", ApiSecret = "secret" };
            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            // Should not throw
            sut.ValidateApiSettings();
        }

        [Fact]
        public void ValidateMessageStrings_Throws_When_PatientNotFoundMessageMissing()
        {
            var settings = new AppSettings
            {
                PatientNotFoundMessage = "  ",
                NotEligibleMessage = "ok",
                WelldoneMessage = "ok",
                TellOffMessage = "ok"
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateMessageStrings());
            Assert.Contains("PatientNotFoundMessage", ex.Message);
        }

        [Fact]
        public void ValidateMessageStrings_Throws_When_NotEligibleMessageMissing()
        {
            var settings = new AppSettings
            {
                PatientNotFoundMessage = "ok",
                NotEligibleMessage = "  ",
                WelldoneMessage = "ok",
                TellOffMessage = "ok"
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateMessageStrings());
            Assert.Contains("NotEligibleMessage", ex.Message);
        }

        [Fact]
        public void ValidateMessageStrings_Throws_When_WelldoneMessageMissing()
        {
            var settings = new AppSettings
            {
                PatientNotFoundMessage = "ok",
                NotEligibleMessage = "ok",
                WelldoneMessage = "",
                TellOffMessage = "ok"
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateMessageStrings());
            Assert.Contains("WelldoneMessage", ex.Message);
        }

        [Fact]
        public void ValidateMessageStrings_Throws_When_TellOffMessageMissing()
        {
            var settings = new AppSettings
            {
                PatientNotFoundMessage = "ok",
                NotEligibleMessage = "ok",
                WelldoneMessage = "ok",
                TellOffMessage = "\t"
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateMessageStrings());
            Assert.Contains("TellOffMessage", ex.Message);
        }

        [Fact]
        public void ValidateMessageStrings_DoesNotThrow_When_AllPresent()
        {
            var settings = new AppSettings
            {
                PatientNotFoundMessage = "Not found",
                NotEligibleMessage = "Not eligible",
                WelldoneMessage = "Well done",
                TellOffMessage = "Please seek help"
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            // Should not throw
            sut.ValidateMessageStrings();
        }

        [Fact]
        public void ValidateAgeBands_Throws_When_AgeBandsIsNull()
        {
            var settings = new AppSettings
            {
                AgeBands = null
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateAgeBands());
            Assert.Contains("AgeBands", ex.Message);
        }

        [Fact]
        public void ValidateAgeBands_Throws_When_AgeBandsEmpty()
        {
            var settings = new AppSettings
            {
                AgeBands = []
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateAgeBands());
            Assert.Contains("AgeBands", ex.Message);
        }

        [Fact]
        public void ValidateAgeBands_DoesNotThrow_When_AgeBandPresent()
        {
            var settings = new AppSettings
            {
                AgeBands = [new() { 0, 10 }]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            sut.ValidateAgeBands();
        }

        [Fact]
        public void ValidateQuestions_Throws_When_QuestionsIsNull()
        {
            var settings = new AppSettings
            {
                Questions = null
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestions());
            Assert.Contains("Questions", ex.Message);
        }

        [Fact]
        public void ValidateQuestions_Throws_When_QuestionsEmpty()
        {
            var settings = new AppSettings
            {
                Questions = []
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestions());
            Assert.Contains("Questions", ex.Message);
        }

        [Fact]
        public void ValidateQuestions_Throws_When_QuestionWhitespaceEntry()
        {
            var settings = new AppSettings
            {
                Questions = ["Yes", "  "]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestions());
            Assert.Contains("Question at index 1", ex.Message);
        }

        [Fact]
        public void ValidateQuestions_DoesNotThrow_When_AllValid()
        {
            var settings = new AppSettings
            {
                Questions = ["First question", "Second question"]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            sut.ValidateQuestions();
        }

        [Fact]
        public void ValidateQuestionScoreSettings_Throws_When_Null()
        {
            var settings = new AppSettings
            {
                QuestionScoreSettings = null
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestionScoreSettings());
            Assert.Contains("QuestionScoreSettings", ex.Message);
        }

        [Fact]
        public void ValidateQuestionScoreSettings_Throws_When_QuestionsCountMismatch()
        {
            var settings = new AppSettings
            {
                Questions = ["q1"],
                QuestionScoreSettings =
                [
                    new QuestionScoreSetting { AgeGroupScores = [] },
                    new QuestionScoreSetting { AgeGroupScores = [] }
                ]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestionScoreSettings());
            Assert.Contains("Questions count", ex.Message);
        }

        [Fact]
        public void ValidateQuestionScoreSettings_Throws_When_QuestionScoreSettingIsNull()
        {
            var settings = new AppSettings
            {
                Questions = ["q1"],
                QuestionScoreSettings = [null]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestionScoreSettings());
            Assert.Contains("QuestionScoreSetting at index 0 is null", ex.Message);
        }

        [Fact]
        public void ValidateQuestionScoreSettings_Throws_When_AgeGroupScoresNull()
        {
            var settings = new AppSettings
            {
                Questions = ["q1"],
                AgeBands = [[0, 10]],
                QuestionScoreSettings = [new QuestionScoreSetting { AgeGroupScores = null }]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestionScoreSettings());
            Assert.Contains("AgeGroupScores for question index 0 is null", ex.Message);
        }

        [Fact]
        public void ValidateQuestionScoreSettings_Throws_When_AgeGroupScoresCountMismatch()
        {
            var settings = new AppSettings
            {
                Questions = ["q1"],
                AgeBands = [[0, 10], [11, 20]],
                QuestionScoreSettings = [new QuestionScoreSetting { AgeGroupScores = [1] }]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateQuestionScoreSettings());
            Assert.Contains("does not match AgeBands count", ex.Message);
        }

        [Fact]
        public void ValidateQuestionScoreSettings_DoesNotThrow_When_Valid()
        {
            var settings = new AppSettings
            {
                Questions = ["q1"],
                AgeBands = [[0, 10], [11, 20]],
                QuestionScoreSettings = [new QuestionScoreSetting { AgeGroupScores = [1, 0] }]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            sut.ValidateQuestionScoreSettings();
        }

        [Fact]
        public void ValidateAgeBandsNonOverlapping_Throws_OnOverlappingBands()
        {
            var settings = new AppSettings
            {
                AgeBands = [[0, 10], [5, 15]]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateAgeBandsNonOverlapping());
            Assert.Contains("overlapping AgeBands detected", ex.Message);
        }

        [Fact]
        public void ValidateAgeBandsNonOverlapping_Throws_OnNullBand()
        {
            var settings = new AppSettings
            {
                AgeBands = [null]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateAgeBandsNonOverlapping());
            Assert.Contains("AgeBand at index 0 is null", ex.Message);
        }

        [Fact]
        public void ValidateAgeBandsNonOverlapping_Throws_OnMalformedBand()
        {
            var settings = new AppSettings
            {
                AgeBands = [[0]]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.ValidateAgeBandsNonOverlapping());
            Assert.Contains("must contain exactly 2 elements", ex.Message);
        }

        [Fact]
        public void ValidateAgeBandsNonOverlapping_DoesNotThrow_When_NonOverlapping()
        {
            var settings = new AppSettings
            {
                AgeBands = [[0, 10], [11, 20]]
            };

            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            sut.ValidateAgeBandsNonOverlapping();
        }

        [Fact]
        public void ValidateAgeBandsContinuity_DoesNotWarn_When_Continuous()
        {
            var logger = new CaptureLogger();
            var settings = new AppSettings
            {
                AgeBands = [[0, 5], [6, 200]]
            };

            var sut = new ConfigValidator(logger, settings);

            sut.ValidateAgeBandsContinuity();

            Assert.Empty(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        }

        [Fact]
        public void ValidateAgeBandsContinuity_LogsWarning_When_GapExists()
        {
            var logger = new CaptureLogger();
            var settings = new AppSettings
            {
                AgeBands = [[0, 5], [7, 200]]
            };

            var sut = new ConfigValidator(logger, settings);

            sut.ValidateAgeBandsContinuity();

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Single(warnings);
            Assert.Contains("Gap detected", warnings[0].Message);
        }

        [Fact]
        public void ValidateLogPersonallyIdentifiableData_LogsWarning_When_Enabled()
        {
            var logger = new CaptureLogger();
            var settings = new AppSettings
            {
                LogPersonallyIdentifiableData = true
            };

            var sut = new ConfigValidator(logger, settings);

            sut.ValidateLogPersonallyIdentifiableData();

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
            Assert.Single(warnings);
            Assert.Contains("LogPersonallyIdentifiableData is enabled", warnings[0].Message);
        }

        [Fact]
        public void ValidateLogPersonallyIdentifiableData_DoesNotLog_When_Disabled()
        {
            var logger = new CaptureLogger();
            var settings = new AppSettings
            {
                LogPersonallyIdentifiableData = false
            };

            var sut = new ConfigValidator(logger, settings);

            sut.ValidateLogPersonallyIdentifiableData();

            Assert.Empty(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        }

        [Fact]
        public void ValidateTellOffIfScoreExceeds_Throws_When_ZeroOrNegative()
        {
            var settingsZero = new AppSettings { TellOffIfScoreExceeds = 0 };
            var sutZero = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settingsZero);
            Assert.Throws<InvalidOperationException>(() => sutZero.ValidateTellOffIfScoreExceeds());

            var settingsNegative = new AppSettings { TellOffIfScoreExceeds = -1 };
            var sutNegative = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settingsNegative);
            Assert.Throws<InvalidOperationException>(() => sutNegative.ValidateTellOffIfScoreExceeds());
        }

        [Fact]
        public void ValidateTellOffIfScoreExceeds_DoesNotThrow_When_Positive()
        {
            var settings = new AppSettings { TellOffIfScoreExceeds = 5 };
            var sut = new ConfigValidator(NullLogger<ConfigValidator>.Instance, settings);

            sut.ValidateTellOffIfScoreExceeds();
        }
    }
}
