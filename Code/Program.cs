using HealthTest;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Prefer a physical appsettings.json in the output folder, then fall back to the embedded JSON
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

// Bind configuration into the AppSettings POCO
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

// Register AppSettings for DI
builder.Services.AddSingleton(appSettings);

// Register ApiClient and its interface with an HttpClient
builder.Services.AddHttpClient<IApiClient, ApiClient>();

var host = string.IsNullOrWhiteSpace(appSettings.Host) ? "localhost" : appSettings.Host;
var port = appSettings.Port <= 0 ? 5000 : appSettings.Port;

var scheme = builder.Environment.IsDevelopment() ? "http" : "https";
var url = $"{scheme}://{host}:{port}";

if (builder.Environment.IsDevelopment())
{
	builder.WebHost.ConfigureKestrel(options =>
	{
		options.ListenAnyIP(port); // HTTP for local/dev
	});
}
else
{
	builder.WebHost.UseUrls(url);
}

// Register handler with DI
builder.Services.AddSingleton<LandingSubmitHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
  app.UseHttpsRedirection();
}

// Serve the static HTML landing page
app.MapGet("/", async (HttpContext ctx) =>
{
	var file = Path.Combine(builder.Environment.ContentRootPath, "Templates", "Landing.html");
	ctx.Response.ContentType = "text/html";
	await ctx.Response.SendFileAsync(file);
});

// Delegate POST to handler from DI
app.MapPost("/landing-submit", async (LandingSubmitHandler handler, HttpContext ctx) =>
{
	return await handler.Handle(ctx);
});

app.Logger.LogInformation("Starting Kestrel on {Url}", url);
app.Run();
