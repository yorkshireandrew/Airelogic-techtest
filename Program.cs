using HealthTest;

var builder = WebApplication.CreateBuilder(args);

// Prefer a physical appsettings.json in the output folder, then fall back to the embedded JSON
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

// Bind configuration into the AppSettings POCO
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

var host = string.IsNullOrWhiteSpace(appSettings.Host) ? "localhost" : appSettings.Host;
var port = appSettings.Port <= 0 ? 5000 : appSettings.Port;

var url = $"http://{host}:{port}";
builder.WebHost.UseUrls(url);

var app = builder.Build();

app.MapGet("/", () => Results.Text("Hello, world!", "text/plain"));

app.Logger.LogInformation("Starting Kestrel on {Url}", url);
app.Run();
