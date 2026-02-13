using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Offline fallback clean architecture metrics tool.

var repoRoot = Directory.GetCurrentDirectory();
var configPath = Path.Combine(repoRoot, "tools", "clean_arch_metrics", "config.json");
var cfg = new Config();
if (File.Exists(configPath))
{
    try { cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath), new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? cfg; }
    catch (Exception e) { Console.Error.WriteLine($"Failed reading config.json: {e.Message}"); }
}

var srcDir = Path.Combine(repoRoot, "src");
var csprojFiles = new List<string>();
if (Directory.Exists(srcDir)) csprojFiles.AddRange(Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories));
if (!csprojFiles.Any()) csprojFiles.AddRange(Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories));

csprojFiles = csprojFiles.Where(p => !IsTestProject(p)).ToList();

if (cfg.IncludeProjects?.Length > 0) csprojFiles = csprojFiles.Where(p => cfg.IncludeProjects.Any(g => GlobMatch(g, Path.GetRelativePath(repoRoot,p)))).ToList();
if (cfg.ExcludeProjects?.Length > 0) csprojFiles = csprojFiles.Where(p => !cfg.ExcludeProjects.Any(g => GlobMatch(g, Path.GetRelativePath(repoRoot,p)))).ToList();

if (!csprojFiles.Any()) { Console.Error.WriteLine("No projects found for analysis."); return 1; }

var components = csprojFiles.Select(p => Path.GetFileNameWithoutExtension(p)).OrderBy(n=>n).ToList();

var dependencies = components.ToDictionary(c=>c, c=> new HashSet<string>());
foreach (var projPath in csprojFiles)
{
    var name = Path.GetFileNameWithoutExtension(projPath);
    try
    {
        var doc = XDocument.Load(projPath);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var refs = doc.Descendants(ns+"ProjectReference").Select(x => x.Attribute("Include")?.Value).Where(v=>v!=null).Select(v=>Path.GetFileNameWithoutExtension(v!));
        foreach (var r in refs) if (dependencies.ContainsKey(name) && dependencies.ContainsKey(r)) dependencies[name].Add(r);
    }
    catch { }
}

var dependents = components.ToDictionary(c=>c, c=> new HashSet<string>());
foreach (var kv in dependencies) foreach (var to in kv.Value) if (dependents.ContainsKey(to)) dependents[to].Add(kv.Key);

var declaredTypes = new List<DeclaredType>();
foreach (var projPath in csprojFiles)
{
    var compName = Path.GetFileNameWithoutExtension(projPath);
    var projDir = Path.GetDirectoryName(projPath) ?? ".";
    var csFiles = Directory.GetFiles(projDir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" ) && !f.Contains(Path.DirectorySeparatorChar + "obj")).ToList();
    foreach (var file in csFiles)
    {
        var txt = File.ReadAllText(file);
        var code = StripComments(txt);
        var ns = "";
        var nm = Regex.Match(code, @"namespace\s+([A-Za-z0-9_.]+)"); if (nm.Success) ns = nm.Groups[1].Value.Trim();
        var typeRegex = new Regex(@"(?<mods>public|internal|protected|private|static|abstract|sealed|partial|\s)+\s*(class|struct|interface|record)\s+(?<name>[A-Za-z0-9_<>]+)(\s*:\s*(?<bases>[^\{\n]+))?", RegexOptions.Compiled);
        foreach (Match m in typeRegex.Matches(code))
        {
            var name = m.Groups["name"].Value;
            var kind = m.Value.Contains("interface") ? "interface" : m.Value.Contains("struct") ? "struct" : m.Value.Contains("record") ? "record" : "class";
            var isAbstract = m.Value.Contains("abstract");
            var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            var bases = m.Groups["bases"].Value;
            declaredTypes.Add(new DeclaredType{Component=compName, Namespace=ns, Name=name, FullName=fullName, Kind=kind, IsAbstract=isAbstract, File=file, Source=code, Bases=bases});
        }
    }
}

var declaredByFull = declaredTypes.ToDictionary(d=>d.FullName, d=>d);
var declaredBySimple = declaredTypes.GroupBy(d=>d.Name).ToDictionary(g=>g.Key, g=>g.Select(x=>x).ToList());

var compMetrics = new List<ComponentMetric>();
foreach (var comp in components)
{
    var typesInComp = declaredTypes.Where(d=>d.Component==comp).ToList();
    var abstracts = typesInComp.Count(t=>t.IsAbstract || t.Kind=="interface");
    var totals = typesInComp.Count();
    var Ce = dependencies[comp].Count;
    var Ca = dependents[comp].Count;
    var I = (Ca + Ce) == 0 ? 0.0 : (double)Ce / (Ca + Ce);
    var A = totals==0 ? 0.0 : (double)abstracts / totals;
    var D = Math.Abs(A + I - 1.0);
    compMetrics.Add(new ComponentMetric{ name=comp, Ca=Ca, Ce=Ce, I=I, A=A, D=D, Abstracts=abstracts, Totals=totals });
}

compMetrics = compMetrics.OrderByDescending(c=>c.D).ThenByDescending(c=>c.Ce).ToList();

var typeMetrics = new List<TypeMetric>();
foreach (var t in declaredTypes.OrderBy(d=>d.FullName))
{
    var outgoing = new HashSet<string>();
    var code = t.Source;
    if (!string.IsNullOrWhiteSpace(t.Bases))
    {
        foreach (var part in t.Bases.Split(new[]{',','<'}, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = part.Trim().Split(' ').LastOrDefault();
            if (string.IsNullOrEmpty(candidate)) continue;
            if (declaredBySimple.TryGetValue(candidate, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
            if (declaredByFull.TryGetValue(candidate, out var df) && df.FullName!=t.FullName) outgoing.Add(df.FullName);
        }
    }
    foreach (Match m in Regex.Matches(code, @"new\s+([A-Za-z0-9_\.]+)\s*<|new\s+([A-Za-z0-9_\.]+)\s*\(", RegexOptions.Compiled))
    {
        var g = m.Groups[1].Value.Length>0 ? m.Groups[1].Value : m.Groups[2].Value;
        var simple = g.Split('.').Last();
        if (declaredBySimple.TryGetValue(simple, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
        if (declaredByFull.TryGetValue(g, out var df) && df.FullName!=t.FullName) outgoing.Add(df.FullName);
    }
    foreach (Match m in Regex.Matches(code, @"typeof\s*\(\s*([A-Za-z0-9_\.]+)\s*\)", RegexOptions.Compiled))
    {
        var g = m.Groups[1].Value; var simple=g.Split('.').Last();
        if (declaredBySimple.TryGetValue(simple, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
        if (declaredByFull.TryGetValue(g, out var df) && df.FullName!=t.FullName) outgoing.Add(df.FullName);
    }
    foreach (Match m in Regex.Matches(code, @"public\s+[A-Za-z0-9_<>\.\[\]]+\s+([A-Za-z0-9_]+)\s*\(([^\)]*)\)", RegexOptions.Compiled))
    {
        var paramList = m.Groups[2].Value;
        foreach (var p in paramList.Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0))
        {
            var parts = p.Split(' ');
            if (parts.Length==1) continue;
            var typeName = parts[0].Split('<')[0].Split('[')[0].Split('.').Last();
            if (declaredBySimple.TryGetValue(typeName, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
        }
    }
    foreach (Match m in Regex.Matches(code, @"\[([A-Za-z0-9_\.]+)\b", RegexOptions.Compiled))
    {
        var g = m.Groups[1].Value; var simple=g.Split('.').Last();
        if (declaredBySimple.TryGetValue(simple, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
    }
    foreach (Match m in Regex.Matches(code, @"([A-Za-z0-9_\.]+)\.[A-Za-z0-9_]+", RegexOptions.Compiled))
    {
        var g = m.Groups[1].Value; var simple=g.Split('.').Last();
        if (declaredBySimple.TryGetValue(simple, out var list)) foreach (var d in list) if (d.FullName!=t.FullName) outgoing.Add(d.FullName);
    }
    var outgoingFiltered = outgoing.Where(fn => !IsIgnoredTypeName(fn, cfg) && !cfg.IgnoreTypes.Contains(fn.Split('.').Last())).ToHashSet();
    typeMetrics.Add(new TypeMetric{ Component=t.Component, Namespace=t.Namespace, TypeName=t.Name, FullName=t.FullName, Kind=t.Kind, Outgoing=outgoingFiltered, File=t.File, Source= t.Source });
}

var typeByFull = typeMetrics.ToDictionary(t => t.FullName, t => t);
foreach (var t in typeMetrics) foreach (var o in t.Outgoing) if (typeByFull.TryGetValue(o, out var target)) target.Incoming.Add(t.FullName);

foreach (var t in typeMetrics)
{
    t.Ce = t.Outgoing.Count;
    t.Ca = t.Incoming.Count;
    t.I = (t.Ca + t.Ce)==0 ? 0.0 : (double)t.Ce / (t.Ca + t.Ce);
    int abstractParams=0, concreteParams=0, ignoredParams=0;
    foreach (Match m in Regex.Matches(t.Source, @"public\s+[A-Za-z0-9_<>\.\[\]]+\s+([A-Za-z0-9_]+)\s*\(([^\)]*)\)", RegexOptions.Compiled))
    {
        var paramList = m.Groups[2].Value;
        foreach (var p in paramList.Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0))
        {
            var parts = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length==0) continue;
            var typeName = parts[0].Split('<')[0].Split('[')[0].Split('.').Last();
            if (cfg.IgnoreTypePrefixes.Any(pref=>typeName.StartsWith(pref)) || cfg.IgnoreTypes.Contains(typeName)) { ignoredParams++; continue; }
            if (typeName.StartsWith("I") && typeName.Length>1 && char.IsUpper(typeName[1])) { abstractParams++; continue; }
            concreteParams++;
        }
    }
    t.AbstractParamCount = abstractParams; t.ConcreteParamCount = concreteParams; t.IgnoredParamCount = ignoredParams;
    t.A_api = (abstractParams + concreteParams)==0 ? (double?)null : (double)abstractParams / (abstractParams + concreteParams);
    t.D_api = t.A_api.HasValue ? Math.Abs(t.A_api.Value + t.I - 1.0) : (double?)null;
    var cic = new HashSet<string>();
    foreach (Match m in Regex.Matches(t.Source, @"new\s+([A-Za-z0-9_\.]+)\s*\(|new\s+([A-Za-z0-9_\.]+)\s*<", RegexOptions.Compiled))
    {
        var g = m.Groups[1].Value.Length>0 ? m.Groups[1].Value : m.Groups[2].Value;
        var simple = g.Split('.').Last();
        if (declaredBySimple.ContainsKey(simple)) cic.Add(simple);
    }
    t.CIC = cic.Count;
}

Directory.CreateDirectory(Path.Combine(repoRoot,"artifacts"));
var compCsv = Path.Combine(repoRoot,"artifacts","clean-arch-components.csv");
var compMd = Path.Combine(repoRoot,"artifacts","clean-arch-components.md");
var typeCsv = Path.Combine(repoRoot,"artifacts","clean-arch-types.csv");
var typeMd = Path.Combine(repoRoot,"artifacts","clean-arch-types.md");

var sb = new StringBuilder();
sb.AppendLine("Component,Ca,Ce,I,A,D,Abstracts,Totals");
foreach (var c in compMetrics) sb.AppendLine($"{Escape(c.name)},{c.Ca},{c.Ce},{c.I:F4},{c.A:F4},{c.D:F4},{c.Abstracts},{c.Totals}");
File.WriteAllText(compCsv, sb.ToString());

var sbmd = new StringBuilder();
sbmd.AppendLine("# Component-level Clean Architecture Metrics");
sbmd.AppendLine("Formulas:");
sbmd.AppendLine("- I = Ce / (Ca + Ce)");
sbmd.AppendLine("- A = (abstract classes + interfaces) / total types");
sbmd.AppendLine("- D = |A + I - 1|");
sbmd.AppendLine("");
sbmd.AppendLine("## Components");
sbmd.AppendLine("|Component|Ca|Ce|I|A|D|Abstracts|Totals|");
sbmd.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
foreach (var c in compMetrics) sbmd.AppendLine($"|{c.name}|{c.Ca}|{c.Ce}|{c.I:F4}|{c.A:F4}|{c.D:F4}|{c.Abstracts}|{c.Totals}|");
sbmd.AppendLine("");
sbmd.AppendLine("## Hotspots");
foreach (var c in compMetrics.Where(x=>x.D>=0.5)) sbmd.AppendLine($"- D>=0.5: {c.name} (D={c.D:F2})");
foreach (var c in compMetrics.Where(x=>x.I<0.2 && x.A<0.25)) sbmd.AppendLine($"- Stable concrete: {c.name} (I={c.I:F2}, A={c.A:F2})");
foreach (var c in compMetrics.Where(x=>x.I>0.8 && x.A<0.25)) sbmd.AppendLine($"- Volatile concrete: {c.name} (I={c.I:F2}, A={c.A:F2})");
File.WriteAllText(compMd, sbmd.ToString());

var sbt = new StringBuilder();
sbt.AppendLine("Component,Namespace,TypeName,Kind,Ca_type,Ce_type,I_type,abstract_param_count,concrete_param_count,ignored_param_count,A_api,D_api,CIC");
foreach (var t in typeMetrics.OrderBy(t=>t.Component).ThenBy(t=>t.Namespace).ThenBy(t=>t.TypeName)) sbt.AppendLine($"{Escape(t.Component)},{Escape(t.Namespace)},{Escape(t.TypeName)},{t.Kind},{t.Ca},{t.Ce},{t.I:F4},{t.AbstractParamCount},{t.ConcreteParamCount},{t.IgnoredParamCount},{(t.A_api.HasValue?t.A_api.Value.ToString("F4") : "")},{(t.D_api.HasValue?t.D_api.Value.ToString("F4") : "")},{t.CIC}");
File.WriteAllText(typeCsv, sbt.ToString());

var sbtmd = new StringBuilder();
sbtmd.AppendLine("# Type-level Clean Architecture Metrics");
sbtmd.AppendLine("Formulas:");
sbtmd.AppendLine("- I_type = Ce_type / (Ca_type + Ce_type)");
sbtmd.AppendLine("- A_api = abstract_param_count / (abstract_param_count + concrete_param_count)");
sbtmd.AppendLine("- D_api = |A_api + I_type - 1|");
sbtmd.AppendLine("");
sbtmd.AppendLine("## Hotspots");
foreach (var t in typeMetrics.Where(x=>x.D_api.HasValue && x.D_api.Value>=0.5).OrderByDescending(x=>x.D_api)) sbtmd.AppendLine($"- D_api>=0.5: {t.Component}.{t.TypeName} (D_api={t.D_api:F2})");
var topCeThreshold = Math.Max(1, (int)Math.Ceiling(typeMetrics.Count * 0.05));
foreach (var t in typeMetrics.OrderByDescending(x=>x.Ce).Take(topCeThreshold)) sbtmd.AppendLine($"- High Ce_type: {t.Component}.{t.TypeName} (Ce={t.Ce})");
foreach (var t in typeMetrics.Where(x=>x.A_api.HasValue && x.A_api.Value<=0.2).OrderByDescending(x=>x.Ca).Take(10)) sbtmd.AppendLine($"- Low A_api high Ca: {t.Component}.{t.TypeName} (A_api={t.A_api:F2}, Ca={t.Ca})");
foreach (var t in typeMetrics.Where(x=>x.A_api.HasValue && x.A_api.Value>=0.8 && x.CIC>0)) sbtmd.AppendLine($"- High A_api but constructs concretes: {t.Component}.{t.TypeName} (A_api={t.A_api:F2}, CIC={t.CIC})");

int psize=50; var pgrid = Enumerable.Range(0,psize).Select(_=>Enumerable.Repeat('.', psize).ToArray()).ToArray();
var palette2 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
var plotList = typeMetrics.Where(t=>t.A_api.HasValue && t.D_api.HasValue).OrderBy(t=>t.Component).ThenBy(t=>t.TypeName).ToList();
var pkey = new List<(char,string)>();
for (int i=0;i<plotList.Count;i++)
{
    var t = plotList[i]; int x = Math.Clamp((int)Math.Round(t.A_api.Value*(psize-1)),0,psize-1); int y = Math.Clamp((int)Math.Round((1.0 - t.D_api.Value)*(psize-1)),0,psize-1);
    char ch = i < palette2.Length ? palette2[i] : (char)('!' + (i - palette2.Length) % 80);
    if (pgrid[y][x]=='.') pgrid[y][x]=ch; else pgrid[y][x]='*';
    pkey.Add((ch,$"{t.Component}.{t.TypeName}"));
}
sbtmd.AppendLine("\n## A_api vs D_api plot\n");
for (int r=0;r<psize;r++) sbtmd.AppendLine(new string(pgrid[r]));
sbtmd.AppendLine("\nKey:\n"); foreach (var (ch,nm) in pkey) sbtmd.AppendLine($"- {ch} : {nm}");
File.WriteAllText(typeMd, sbtmd.ToString());

Console.WriteLine($"Wrote artifacts to {Path.Combine(repoRoot,"artifacts")} (approximate offline analysis)");
return 0;

static string StripComments(string code)
{
    code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
    code = Regex.Replace(code, @"//.*?$", "", RegexOptions.Multiline);
    return code;
}

static bool IsTestProject(string projPath)
{
    var filename = Path.GetFileNameWithoutExtension(projPath);
    var nl = filename.ToLowerInvariant();
    var pathLower = projPath.ToLowerInvariant().Replace('/', '\\');
    // Treat as test project when it's located in a test folder or uses common test naming (e.g. Project.Test, Project.Tests, project-test)
    if (pathLower.Contains("\\test\\") || pathLower.Contains("\\tests\\")) return true;
    if (nl.Contains(".test") || nl.Contains(".tests") || nl.Contains("-test") || nl.EndsWith("testproject")) return true;
    return false;
}

static bool GlobMatch(string pattern, string path)
{
    var regex = "^" + Regex.Escape(pattern).Replace("\\*",".*").Replace("\\?",".") + "$";
    return Regex.IsMatch(path.Replace('\\','/'), regex, RegexOptions.IgnoreCase);
}

static bool IsIgnoredTypeName(string fullName, Config cfg)
{
    foreach (var p in cfg.IgnoreTypePrefixes ?? Array.Empty<string>()) if (fullName.StartsWith(p)) return true;
    var simple = fullName.Split('.').Last();
    foreach (var s in cfg.IgnoreTypes ?? Array.Empty<string>()) if (string.Equals(s, simple, StringComparison.OrdinalIgnoreCase) || string.Equals(s, fullName, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

static string Escape(string s) => s.Contains(',') ? '"'+s.Replace("\"","\"\"")+'"' : s;

class Config { public string[]? IncludeProjects { get; set; } public string[]? ExcludeProjects { get; set; } public string[] IgnoreTypePrefixes { get; set; } = new[] { "System.", "Microsoft." }; public string[] IgnoreTypes { get; set; } = new[] { "string","DateTime","Guid","CancellationToken" }; public bool TreatStructsAsIgnored { get; set; } = true; public bool AnalyzePropertyParameters { get; set; } = false; }

class DeclaredType { public string Component=""; public string Namespace=""; public string Name=""; public string FullName=""; public string Kind=""; public bool IsAbstract=false; public string File=""; public string Source=""; public string Bases=""; }

class ComponentMetric { public string name=""; public int Ca; public int Ce; public double I; public double A; public double D; public int Abstracts; public int Totals; }

class TypeMetric { public string Component=""; public string Namespace=""; public string TypeName=""; public string FullName=""; public string Kind=""; public string File=""; public string Source=""; public HashSet<string> Outgoing = new(); public HashSet<string> Incoming = new(); public int Ce; public int Ca; public double I; public int AbstractParamCount; public int ConcreteParamCount; public int IgnoredParamCount; public double? A_api; public double? D_api; public int CIC; }
