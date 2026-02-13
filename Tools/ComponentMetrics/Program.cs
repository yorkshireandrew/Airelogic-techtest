using System.Text.RegularExpressions;
using System.Xml.Linq;

// Offline-friendly component metrics tool.
var repoRoot = Directory.GetCurrentDirectory();

string? FindSolution(string start)
{
    var di = new DirectoryInfo(start);
    while (di != null)
    {
        var sln = di.GetFiles("*.sln").FirstOrDefault();
        if (sln != null) return sln.FullName;
        di = di.Parent;
    }
    return null;
}

var sln = FindSolution(repoRoot);
if (sln == null)
{
    Console.Error.WriteLine("Solution not found; running against discovered .csproj files in repo.");
}

// Discover all .csproj files under repo
var csprojFiles = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
    .Where(p => !p.Contains("/bin/") && !p.Contains("\\bin\\") && !p.Contains("/obj/") && !p.Contains("\\obj\\"))
    .ToList();

if (!csprojFiles.Any())
{
    Console.Error.WriteLine("No .csproj files found.");
    return 1;
}

// Each component is a project filename without path
var components = csprojFiles.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct().ToList();

// Build dependencies by reading ProjectReference includes
var dependencies = components.ToDictionary(c => c, c => new HashSet<string>());

foreach (var projPath in csprojFiles)
{
    var name = Path.GetFileNameWithoutExtension(projPath);
    try
    {
        var doc = XDocument.Load(projPath);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var refs = doc.Descendants(ns + "ProjectReference").Select(x => x.Attribute("Include")?.Value).Where(v => v != null).Select(v => Path.GetFileNameWithoutExtension(v!)).Distinct();
        foreach (var r in refs)
        {
            if (dependencies.ContainsKey(name)) dependencies[name].Add(r);
        }
    }
    catch(Exception ex)
    {
        Console.Error.WriteLine($"Warning: failed to parse {projPath}: {ex.Message}");
    }
}

// Compute reverse deps (afferent coupling)
var dependents = components.ToDictionary(c => c, c => new HashSet<string>());
foreach (var kv in dependencies)
{
    foreach (var to in kv.Value)
    {
        if (dependents.ContainsKey(to)) dependents[to].Add(kv.Key);
    }
}

// Type counting: scan .cs files within each project folder. Simple comment stripping + regex scanning.
var metrics = new Dictionary<string, (int abstracts,int totals)>();

string StripComments(string code)
{
    // remove block comments
    code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
    // remove line comments
    code = Regex.Replace(code, @"//.*?$", string.Empty, RegexOptions.Multiline);
    return code;
}

var typeRegex = new Regex(@"\b(class|struct|interface|record)\s+([A-Za-z0-9_<>]+)", RegexOptions.Compiled);
var abstractClassRegex = new Regex(@"\babstract\s+class\b", RegexOptions.Compiled);

foreach (var projPath in csprojFiles)
{
    var projDir = Path.GetDirectoryName(projPath) ?? ".";
    var name = Path.GetFileNameWithoutExtension(projPath);
    var csFiles = Directory.GetFiles(projDir, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.Combine("bin", "")) && !f.Contains(Path.Combine("obj", ""))).ToList();

    var total = 0;
    var abstracts = 0;
    foreach (var f in csFiles)
    {
        try
        {
            var txt = File.ReadAllText(f);
            var code = StripComments(txt);
            foreach (Match m in typeRegex.Matches(code))
            {
                total++;
                var snippetStart = Math.Max(0, m.Index - 100);
                var ctx = code.Substring(snippetStart, Math.Min(200, code.Length - snippetStart));
                if (m.Groups[1].Value == "interface") { abstracts++; continue; }
                if (m.Groups[1].Value == "class")
                {
                    if (abstractClassRegex.IsMatch(ctx)) abstracts++;
                }
                // records/structs counted as concrete unless declared abstract (rare)
            }
        }
        catch { }
    }

    metrics[name] = (abstracts, total);
}

// Compute component data
var compData = new List<(string name,int Ca,int Ce,double I,double A,int abstracts,int totals)>();
foreach (var name in components.OrderBy(n => n))
{
    var Ce = dependencies.TryGetValue(name, out var deps) ? deps.Count : 0;
    var Ca = dependents.TryGetValue(name, out var deps2) ? deps2.Count : 0;
    var I = (Ca + Ce) == 0 ? 0.0 : (double)Ce / (Ca + Ce);
    var (abstracts, totals) = metrics.TryGetValue(name, out var v) ? v : (0,0);
    var A = totals == 0 ? 0.0 : (double)abstracts / totals;
    compData.Add((name,Ca,Ce,I,A,abstracts,totals));
}

// ASCII plot with gridlines at 25%, 50%, 75%
int size = 50;
var grid = Enumerable.Range(0, size).Select(_ => Enumerable.Repeat('.', size).ToArray()).ToArray();
var palette = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
var key = new List<(char, string)>();

// pre-draw grid lines (vertical at I=25/50/75 and horizontal at A=25/50/75)
int x25 = Math.Clamp((int)Math.Round(0.25 * (size - 1)), 0, size - 1);
int x50 = Math.Clamp((int)Math.Round(0.50 * (size - 1)), 0, size - 1);
int x75 = Math.Clamp((int)Math.Round(0.75 * (size - 1)), 0, size - 1);
int y25 = Math.Clamp((int)Math.Round((1.0 - 0.25) * (size - 1)), 0, size - 1);
int y50 = Math.Clamp((int)Math.Round((1.0 - 0.50) * (size - 1)), 0, size - 1);
int y75 = Math.Clamp((int)Math.Round((1.0 - 0.75) * (size - 1)), 0, size - 1);

var xGrid = new[] { x25, x50, x75 };
var yGrid = new[] { y25, y50, y75 };

foreach (var xg in xGrid)
{
    for (int r = 0; r < size; r++)
    {
        if (grid[r][xg] == '.') grid[r][xg] = '|';
        else if (grid[r][xg] == '-') grid[r][xg] = '+';
    }
}

foreach (var yg in yGrid)
{
    for (int c = 0; c < size; c++)
    {
        if (grid[yg][c] == '.') grid[yg][c] = '-';
        else if (grid[yg][c] == '|') grid[yg][c] = '+';
    }
}

// place component markers (override grid lines)
for (int i = 0; i < compData.Count; i++)
{
    var (name, Ca, Ce, I, A, abstracts, totals) = compData[i];
    int x = Math.Clamp((int)Math.Round(I * (size - 1)), 0, size - 1);
    int y = Math.Clamp((int)Math.Round((1.0 - A) * (size - 1)), 0, size - 1);
    char ch = i < palette.Length ? palette[i] : (char)('!' + (i - palette.Length) % 80);
    var current = grid[y][x];
    if (current == '.' || current == '-' || current == '|' || current == '+') grid[y][x] = ch;
    else if (current == ch) { /* leave as is */ }
    else grid[y][x] = '*';
    key.Add((ch, name));
}

Console.WriteLine();
Console.WriteLine("Abstractness vs Instability (A vs I) — 50x50");
for (int r = 0; r < size; r++) Console.WriteLine(new string(grid[r]));

Console.WriteLine();
Console.WriteLine("Key:");
// Align the A= column by padding names
int maxName = key.Select(k => k.Item2.Length).DefaultIfEmpty(0).Max();
foreach (var (ch, name) in key)
{
    var d = compData.First(c => c.name == name);
    Console.WriteLine($"{ch} : {name.PadRight(maxName)}  A={d.A:F2} I={d.I:F2} Ca={d.Ca} Ce={d.Ce} abstracts={d.abstracts} totals={d.totals}");
}

var highBoth = compData.Where(c => c.A > 0.75 && c.I > 0.75).ToList();
if (highBoth.Any())
{
    Console.WriteLine();
    Console.WriteLine("Components with high Abstractness and Instability (both > 0.75) — might be largely useless:");
    foreach (var c in highBoth) Console.WriteLine($" - {c.name} (A={c.A:F2}, I={c.I:F2})");
}

var lowBoth = compData.Where(c => c.A < 0.25 && c.I < 0.25).ToList();
if (lowBoth.Any())
{
    Console.WriteLine();
    Console.WriteLine("Components with low Abstractness and low Instability (both < 0.25) — high coupling, few abstractions:");
    foreach (var c in lowBoth) Console.WriteLine($" - {c.name} (A={c.A:F2}, I={c.I:F2})");
}

return 0;
