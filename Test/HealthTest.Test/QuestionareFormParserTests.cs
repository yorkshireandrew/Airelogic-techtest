using System.Collections.Generic;
using HealthTest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace HealthTest.Test
{
    public class QuestionareFormParserTests
    {
        [Fact]
        public void Parse_WithTotalQuestionsAndAnswers_ReturnsExpectedModel()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"AgeBand", "2"},
                {"TotalQuestions", "3"},
                {"Y0", "on"},   // question 0 -> yes
                {"N1", "on"}    // question 1 -> no (Y1 absent)
                // question 2 unanswered
            };

            var form = new FormCollection(dict);
            var parser = new QuestionareFormParser();

            var model = parser.Parse(form);

            Assert.Equal(2, model.AgeGroup);
            Assert.Equal(3, model.Answers.Count);
            Assert.True(model.Answers[0]);
            Assert.False(model.Answers[1]);
            Assert.False(model.Answers[2]);
        }

        [Fact]
        public void Parse_InfersTotalQuestionsFromKeys_WhenTotalMissing()
        {
            var dict = new Dictionary<string, StringValues>
            {
                {"AgeBand", "1"},
                {"Y0", "on"},
                {"Y2", "on"}
            };

            var form = new FormCollection(dict);
            var parser = new QuestionareFormParser();

            var model = parser.Parse(form);

            // Keys present: Y0 and Y2 -> max index 2 -> total 3
            Assert.Equal(1, model.AgeGroup);
            Assert.Equal(3, model.Answers.Count);
            Assert.True(model.Answers[0]);
            Assert.False(model.Answers[1]);
            Assert.True(model.Answers[2]);
        }
    }
}
