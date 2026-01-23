using System.IO;
using System.Net;

namespace HealthTest;

public class AnswerPageGenerator
{
    private readonly string _template;

    public AnswerPageGenerator(string templatePath)
    {
        var file = Path.Combine(templatePath, "Answer.html")  ;
        _template = File.ReadAllText(file);
    }

    public IResult Generate(string message)
    {
        var encoded = WebUtility.HtmlEncode(message ?? string.Empty);
        var content = _template.Replace("{{message}}", encoded);
        return Results.Content(content, "text/html");
    }
}
