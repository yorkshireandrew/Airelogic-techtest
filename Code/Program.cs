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

// Serve the browser test runner page (manual tests)
app.MapGet("/frontend/test-runner", (HttpContext ctx) =>
{
	var path = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Browser", "test-runner.html");
	if (!System.IO.File.Exists(path)) return Results.NotFound();
	var content = System.IO.File.ReadAllText(path);
	return Results.Text(content, "text/html");
});

// (frontend asset catch-all moved below so specific routes work)

// Debug endpoint: list files under Frontend/Browser for diagnostics
app.MapGet("/frontend/debug-list", () =>
{
	var dir = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Browser");
	if (!Directory.Exists(dir))
	{
		return Results.Json(new { exists = false, path = dir });
	}

	var files = System.IO.Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
	var rel = new System.Collections.Generic.List<string>();
	foreach (var f in files)
	{
		var r = Path.GetRelativePath(dir, f).Replace('\\', '/');
		rel.Add(r);
	}

	return Results.Json(new { exists = true, path = dir, files = rel });
});

// Diagnostic: check how a requested frontend file would be resolved
app.MapGet("/frontend/check-file", (HttpContext ctx) =>
{
	var file = ctx.Request.Query["f"].ToString() ?? string.Empty;
	if (string.IsNullOrEmpty(file)) return Results.Json(new { ok = false, reason = "missing f query" });

	var baseDir = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Browser");
	var safeFile = file.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	var candidate = Path.GetFullPath(Path.Combine(baseDir, safeFile.Replace('/', Path.DirectorySeparatorChar)));
	var starts = candidate.StartsWith(Path.GetFullPath(baseDir), StringComparison.OrdinalIgnoreCase);
	var exists = System.IO.File.Exists(candidate);

	return Results.Json(new { ok = true, file = file, safeFile = safeFile, baseDir = baseDir, candidate = candidate, startsWithBase = starts, exists = exists });
});

// Serve individual frontend assets (JS/CSS/HTML/JSX) used by the browser runner
// catch-all so paths like /frontend/utils/validateDob.js work
app.MapGet("/frontend/{*file}", (string file) =>
{
	if (string.IsNullOrEmpty(file)) return Results.NotFound();

	// Basic sanitization: disallow path traversal, allow subpaths
	if (string.IsNullOrEmpty(file) || file.Contains(".."))
		return Results.NotFound();

	var ext = Path.GetExtension(file);
	var allowed = new[] { ".js", ".css", ".html", ".jsx", ".map" };
	if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
		return Results.NotFound();

	var baseDir = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Browser");
	// Normalize and prevent absolute paths from bypassing baseDir
	var safeFile = file.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	var candidate = Path.GetFullPath(Path.Combine(baseDir, safeFile.Replace('/', Path.DirectorySeparatorChar)));
	if (!candidate.StartsWith(Path.GetFullPath(baseDir), StringComparison.OrdinalIgnoreCase))
		return Results.NotFound();

	if (!System.IO.File.Exists(candidate)) return Results.NotFound();

	var content = System.IO.File.ReadAllText(candidate);
	var contentType = ext.ToLowerInvariant() switch
	{
		".js" => "application/javascript",
		".css" => "text/css",
		".html" => "text/html",
		".jsx" => "text/babel",
		".map" => "application/json",
		_ => "application/octet-stream"
	};

	return Results.Text(content, contentType);
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
