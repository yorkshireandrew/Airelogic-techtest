using Xunit;

namespace HealthTest.Test
{
    public class QuestionnaireScorerThresholdTests
    {
        [Fact]
        public void IsOverThreshold_ReturnsFalse_WhenScoreBelowThreshold()
        {
            var settings = new AppSettings { TellOffIfScoreExceeds = 10 };
            var scorer = new QuestionnaireScorer(settings);

            Assert.False(scorer.IsOverThreshold(9));
        }

        [Fact]
        public void IsOverThreshold_ReturnsFalse_WhenScoreEqualsThreshold()
        {
            var settings = new AppSettings { TellOffIfScoreExceeds = 10 };
            var scorer = new QuestionnaireScorer(settings);

            Assert.False(scorer.IsOverThreshold(10));
        }

        [Fact]
        public void IsOverThreshold_ReturnsTrue_WhenScoreAboveThreshold()
        {
            var settings = new AppSettings { TellOffIfScoreExceeds = 10 };
            var scorer = new QuestionnaireScorer(settings);

            Assert.True(scorer.IsOverThreshold(11));
        }
    }
}
