namespace HealthTest;

// Renders a static landing page from a specified template file
public class LandingPageGenerator
{
    private readonly string _template;

    public LandingPageGenerator(string templatePath, string templateFileName)
    {
        var file = Path.Combine(templatePath, templateFileName);
        _template = File.ReadAllText(file);
    }
    public IResult Generate()
    {
        return Results.Content(_template, "text/html");
    }
}
