namespace HealthTest;

using System.Net;

public class QuestionnairePageGenerator
{
    private readonly string _template;
    public QuestionnairePageGenerator(string templatePath, AppSettings config)
    {
        var questionnaireTemplatePath = Path.Combine(templatePath, "Questionnaire.html");
        var questionTemplatePath = Path.Combine(templatePath, "Question.html");

        var questions = config?.Questions;
        var questionHtml = "";
        for (int i = 0; i < questions?.Count; i++)
        {
            var questionTemplate = System.IO.File.ReadAllText(questionTemplatePath);
            var safeQuestion = WebUtility.HtmlEncode(questions[i] ?? string.Empty);
            questionHtml += questionTemplate
                .Replace("{{QuestionText}}", safeQuestion)
                .Replace("{{QuestionNumber}}", (i).ToString());
        }

        var questionnaireTemplate = System.IO.File.ReadAllText(questionnaireTemplatePath);
        _template = questionnaireTemplate
            .Replace("{{Questions}}", questionHtml)
            .Replace("{{TotalQuestions}}", questions?.Count.ToString() ?? "0");
    }

    public string Generate(string ageBand)
    {
        return _template.Replace("{{AgeBand}}", ageBand.ToString());
    }
}
