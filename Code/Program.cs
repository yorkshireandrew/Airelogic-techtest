using HealthTest;

var builder = WebApplication.CreateBuilder(args);

// Prefer a physical appsettings.json in the output folder, then fall back to the embedded JSON
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);
builder.Services.AddSingleton(appSettings);

// Landing Submit Handler dependencies
builder.Services.AddHttpClient<IApiClient, ApiClient>();
builder.Services.AddSingleton<IAgeBandCalculator, AgeBandCalculator>();
builder.Services.AddSingleton<ILandingFormParser, LandingFormParser>();
builder.Services.AddSingleton<LandingSubmitHandler>();

// Questionnaire Submit Handler dependencies
builder.Services.AddSingleton<QuestionnaireFormParser>();
builder.Services.AddSingleton<QuestionnaireScorer>();
builder.Services.AddSingleton<QuestionnaireSubmitHandler>();

builder.Services.AddSingleton(provider =>
	new LandingPageGenerator(Path.Combine(builder.Environment.ContentRootPath, "Templates"), "Landing.html"	)
);

builder.Services.AddSingleton<QuestionnairePageGenerator>(provider =>
	new QuestionnairePageGenerator(Path.Combine(builder.Environment.ContentRootPath, "Templates"), appSettings)
);

builder.Services.AddSingleton<AnswerPageGenerator>(provider =>
	new AnswerPageGenerator(Path.Combine(builder.Environment.ContentRootPath, "Templates"))
);

builder.Services.AddSingleton<AppPageGenerator>(provider =>
	new AppPageGenerator(Path.Combine(builder.Environment.ContentRootPath, "Templates"))
);

// Register configuration validator
builder.Services.AddSingleton<ConfigValidator>();

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

var app = builder.Build(); // Instantiate the app

// Run config validation at startup
var configValidator = app.Services.GetRequiredService<ConfigValidator>();
configValidator.ValidateSettings();

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
  app.UseHttpsRedirection();
}

// Serve the static HTML landing page
// Serve SPA app at root
app.MapGet("/", (AppPageGenerator generator, HttpContext ctx) =>
{
	return generator.Generate();
});

// Serve the Questionnaire page
app.MapGet("/Questionnaire", async (QuestionnairePageGenerator generator, HttpContext ctx) =>
{
	var ageBand = ctx.Request.Query["ab"].ToString() ?? string.Empty;
	var html = generator.Generate(ageBand);
	ctx.Response.ContentType = "text/html";
	await ctx.Response.WriteAsync(html);
});

// Serve Answer page (GET redirects)
app.MapGet("/Answer", (AnswerPageGenerator generator, HttpContext ctx) =>
{
	var message = ctx.Request.Query["message"].ToString() ?? string.Empty;
	return generator.Generate(message);
});

// Serve questions as JSON for SPA/frontend
app.MapGet("/api/questions", (AppSettings settings, HttpContext ctx) =>
{
	var ageBand = ctx.Request.Query["ab"].ToString() ?? string.Empty;
	return Results.Json(new { questions = settings.Questions, ageBand = ageBand });
});

// Serve browser-ready frontend JSX files
app.MapGet("/frontend/browser/{file}", (string file) =>
{
	// Sanitize the requested filename - only allow .jsx files
	if (string.IsNullOrEmpty(file) || !file.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase))
		return Results.NotFound();

	var path = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Browser", file);
	if (!System.IO.File.Exists(path)) return Results.NotFound();

	var content = System.IO.File.ReadAllText(path);
	return Results.Text(content, "text/babel");
});

app.MapPost("/landing-submit", async (LandingSubmitHandler handler, HttpContext ctx) =>
{
	return await handler.Handle(ctx);
});

app.MapPost("/questionnaire-submit", async (QuestionnaireSubmitHandler handler, HttpContext ctx) =>
{
	return await handler.Handle(ctx);
});

app.Logger.LogInformation("Listening on {Url}", url);
app.Run();
