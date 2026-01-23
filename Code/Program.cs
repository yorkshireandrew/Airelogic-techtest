using HealthTest;
//using System.IO;
//using Microsoft.AspNetCore.Http;
//using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Prefer a physical appsettings.json in the output folder, then fall back to the embedded JSON
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);
builder.Services.AddSingleton(appSettings);

// Register ApiClient and its interface with an HttpClient
builder.Services.AddHttpClient<IApiClient, ApiClient>();
builder.Services.AddHttpClient<IAgeBandCalculator, AgeBandCalculator>();
builder.Services.AddSingleton<LandingSubmitHandler>();

builder.Services.AddSingleton<QuestionarePageGenerator>(provider =>	
		new QuestionarePageGenerator(Path.Combine(builder.Environment.ContentRootPath, "Templates"), appSettings)
);

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
	var html = await System.IO.File.ReadAllTextAsync(file);
	ctx.Response.ContentType = "text/html";
	await ctx.Response.WriteAsync(html);
});

// Serve the Questionare page
app.MapGet("/Questionare", async (QuestionarePageGenerator generator, HttpContext ctx) =>
{
	var ageBand = ctx.Request.Query["ab"].ToString() ?? string.Empty;
	var html = generator.Generate(ageBand);
	ctx.Response.ContentType = "text/html";
	await ctx.Response.WriteAsync(html);
});

// Delegate POST to handler from DI
app.MapPost("/landing-submit", async (LandingSubmitHandler handler, HttpContext ctx) =>
{
	return await handler.Handle(ctx);
});

// Serve the response GET page with `message` query parameter
app.MapGet("/Answer", async (HttpContext ctx) =>
{
	var message = ctx.Request.Query["message"].ToString() ?? string.Empty;
	var file = Path.Combine(builder.Environment.ContentRootPath, "Templates", "Answer.html");
	var html = await System.IO.File.ReadAllTextAsync(file);
	html = html.Replace("{{message}}", System.Net.WebUtility.HtmlEncode(message)); // Insert message
	ctx.Response.ContentType = "text/html";
	await ctx.Response.WriteAsync(html);
});

app.Logger.LogInformation("Listening on {Url}", url);
app.Run();
