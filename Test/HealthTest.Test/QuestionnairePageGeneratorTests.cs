using System;
using System.IO;
using Xunit;

namespace HealthTest.Test
{
    public class QuestionnairePageGeneratorTests
    {
        [Fact]
        public void Generate_RendersQuestionsAndAgeBand_WithHtmlEscaping()
        {
            var temp = Path.Combine(Path.GetTempPath(), "qpgtest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var questionTemplate = "<div class=\"q\">{{QuestionNumber}}: {{QuestionText}}</div>";
                var questionnaireTemplate = "<html><body>{{Questions}}<div>Total: {{TotalQuestions}}</div><div>AB: {{AgeBand}}</div></body></html>";
                File.WriteAllText(Path.Combine(temp, "Question.html"), questionTemplate);
                File.WriteAllText(Path.Combine(temp, "Questionnaire.html"), questionnaireTemplate);

                var settings = new AppSettings
                {
                    Questions = new System.Collections.Generic.List<string> { "<b>What?</b>" }
                };

                var generator = new QuestionnairePageGenerator(temp, settings);
                var output = generator.Generate("AB1");

                Assert.Contains("&lt;b&gt;What?&lt;/b&gt;", output);
                Assert.Contains("0:", output);
                Assert.Contains("Total: 1", output);
                Assert.Contains("AB: AB1", output);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
