namespace HealthTest;

// Serves the developer/demo single-page app template
public class AppPageGenerator
{
    private readonly string _template;

    public AppPageGenerator(string templatePath)
    {
        var file = Path.Combine(templatePath, "App.html");
        _template = File.ReadAllText(file);
    }

    public IResult Generate()
    {
        return Results.Content(_template, "text/html");
    }
}
