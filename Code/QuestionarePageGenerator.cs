namespace HealthTest;

  public class QuestionarePageGenerator
  {
      private readonly string _template;
      public QuestionarePageGenerator(string templatePath, AppSettings config)
      {
        	var questionareTemplatePath = Path.Combine(templatePath, "Questionare.html");
            var questionTemplatePath = Path.Combine(templatePath, "Question.html");

            var questions = config?.Questions;
            var questionHtml = "";
            for(int i = 0; i < questions?.Count; i++)
            {
                var questionTemplate = System.IO.File.ReadAllText(questionTemplatePath);
                questionHtml += questionTemplate
                    .Replace("{{QuestionText}}", questions[i])
                    .Replace("{{QuestionNumber}}", (i + 1).ToString());
            }


            var questionareTemplate = System.IO.File.ReadAllText(questionareTemplatePath);
            _template = questionareTemplate
            .Replace("{{Questions}}", questionHtml)
            .Replace("{{TotalQuestions}}", questions?.Count.ToString() ?? "0");
      }

      public string Generate(string ageBand)
      {
          return _template.Replace("{{AgeBand}}", ageBand.ToString());
      }
  }

