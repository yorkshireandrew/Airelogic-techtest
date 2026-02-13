using System.Text;
using System.Text.Json;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Simplified Clean Architecture metrics tool.
// - loads projects per config
// - computes component-level and type-level metrics
// - writes CSV and markdown reports to artifacts/

var repoRoot = Directory.GetCurrentDirectory();
var configPath = Path.Combine(repoRoot, "tools", "clean_arch_metrics", "config.json");
var cfg = new Config();
if (File.Exists(configPath))
{
    try
    {
        var txt = File.ReadAllText(configPath);
        cfg = JsonSerializer.Deserialize<Config>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? cfg;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to read config.json: {ex.Message}");
    }
}

// find csproj components under src/ or all
var csprojFiles = new List<string>();
var srcDir = Path.Combine(repoRoot, "src");
if (Directory.Exists(srcDir)) csprojFiles.AddRange(Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories));
if (!csprojFiles.Any()) csprojFiles.AddRange(Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories));

// default exclude test projects by name
csprojFiles = csprojFiles.Where(p => !IsTestProject(p)).ToList();

// apply include/exclude globs (simple wildcard *)
if (cfg.IncludeProjects != null && cfg.IncludeProjects.Length > 0)
{
    csprojFiles = csprojFiles.Where(p => cfg.IncludeProjects.Any(glob => GlobMatch(glob, Path.GetRelativePath(repoRoot, p)))).ToList();
}
if (cfg.ExcludeProjects != null && cfg.ExcludeProjects.Length > 0)
{
    csprojFiles = csprojFiles.Where(p => !cfg.ExcludeProjects.Any(glob => GlobMatch(glob, Path.GetRelativePath(repoRoot, p)))).ToList();
}

if (!csprojFiles.Any())
{
    Console.Error.WriteLine("No projects found for analysis.");
    return 1;
}

MSBuildLocator.RegisterDefaults();
using var workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += (s,e) => Console.Error.WriteLine($"Workspace: {e.Diagnostic}");

Console.WriteLine("Loading projects...");
var projects = new List<Project>();
foreach (var p in csprojFiles.OrderBy(x => x))
{
    try
    {
        var proj = await workspace.OpenProjectAsync(p).ConfigureAwait(false);
        projects.Add(proj);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load project {p}: {ex.Message}");
    }
}

// map project name to project
var compNames = projects.Select(p => p.Name).OrderBy(n=>n).ToList();
var projectByName = projects.ToDictionary(p => p.Name, p => p);

// component-level: Ce (outgoing), Ca (incoming)
var dependencies = compNames.ToDictionary(n => n, n => new HashSet<string>());
foreach (var proj in projects)
{
    var name = proj.Name;
    foreach (var r in proj.ProjectReferences)
    {
        var target = proj.Solution.GetProject(r.ProjectId);
        if (target != null && projectByName.ContainsKey(target.Name) && target.Name != name) dependencies[name].Add(target.Name);
    }
}

var dependents = compNames.ToDictionary(n => n, n => new HashSet<string>());
foreach (var kv in dependencies)
    foreach (var to in kv.Value)
        dependents[to].Add(kv.Key);

// cache compilations
var compilations = new Dictionary<string, Compilation>();
foreach (var proj in projects)
{
    var comp = await proj.GetCompilationAsync().ConfigureAwait(false);
    if (comp != null) compilations[proj.Name] = comp;
}

// component abstractness: count abstract types and total types
var compMetrics = new List<ComponentMetric>();
foreach (var name in compNames)
{
    var comp = compilations.TryGetValue(name, out var c) ? c : null;
    int abstracts = 0, totals = 0;
    if (comp != null)
    {
        foreach (var tree in comp.SyntaxTrees)
        {
            var model = comp.GetSemanticModel(tree);
            var root = await tree.GetRootAsync().ConfigureAwait(false);
            var typeDecls = root.DescendantNodes().Where(n => n is TypeDeclarationSyntax || n is RecordDeclarationSyntax);
            foreach (var td in typeDecls)
            {
                var sym = model.GetDeclaredSymbol(td) as INamedTypeSymbol;
                if (sym == null) continue;
                if (!IsSymbolInProject(sym, name)) continue;
                totals++;
                if (sym.TypeKind == TypeKind.Interface || (sym.TypeKind == TypeKind.Class && sym.IsAbstract)) abstracts++;
            }
        }
    }
    var Ce = dependencies[name].Count;
    var Ca = dependents[name].Count;
    var I = (Ca + Ce) == 0 ? 0.0 : (double)Ce / (Ca + Ce);
    var A = totals == 0 ? 0.0 : (double)abstracts / totals;
    var D = Math.Abs(A + I - 1.0);
    compMetrics.Add(new ComponentMetric{name=name,Ca=Ca,Ce=Ce,I=I,A=A,D=D,Abstracts=abstracts,Totals=totals});
}

// sort components by D desc then Ce desc
compMetrics = compMetrics.OrderByDescending(c=>c.D).ThenByDescending(c=>c.Ce).ToList();

// TYPE-LEVEL: collect all declared named types in the included projects
var declaredTypes = new List<INamedTypeSymbol>();
var typeToComponent = new Dictionary<INamedTypeSymbol,string>(SymbolEqualityComparer.Default);
foreach (var proj in projects)
{
    if (!compilations.TryGetValue(proj.Name, out var comp)) continue;
    foreach (var tree in comp.SyntaxTrees)
    {
        var model = comp.GetSemanticModel(tree);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var typeDecls = root.DescendantNodes().Where(n => n is TypeDeclarationSyntax || n is RecordDeclarationSyntax);
        foreach (var td in typeDecls)
        {
            var sym = model.GetDeclaredSymbol(td) as INamedTypeSymbol;
            if (sym == null) continue;
            if (!IsSymbolInProject(sym, proj.Name)) continue;
            if (!declaredTypes.Contains(sym, SymbolEqualityComparer.Default))
            {
                declaredTypes.Add(sym);
                typeToComponent[sym] = proj.Name;
            }
        }
    }
}

// build index by metadata name to match declared types
var declaredByName = declaredTypes.ToDictionary(t => t.ToDisplayString(), t => t, SymbolEqualityComparer.Default);

// prepare type-level metrics containers
var typeMetrics = new List<TypeMetric>();

// Precompute a lookup of declared types per compilation to check external/internal
var declaredSet = new HashSet<INamedTypeSymbol>(declaredTypes, SymbolEqualityComparer.Default);

// For each declared type, compute outgoing references (Ce_type) and API abstraction and CIC
foreach (var type in declaredTypes.OrderBy(t=>t.ToDisplayString()))
{
    var compName = typeToComponent[type];
    var outgoing = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    // base type and interfaces
    if (type.BaseType != null && declaredSet.Contains(type.BaseType)) outgoing.Add(type.BaseType);
    foreach (var ints in type.Interfaces) if (declaredSet.Contains(ints)) outgoing.Add(ints);

    // members
    foreach (var m in type.GetMembers())
    {
        switch (m)
        {
            case IMethodSymbol ms:
                // return type
                AddTypeAndTypeArgs(ms.ReturnType, declaredSet, outgoing);
                foreach (var p in ms.Parameters) AddTypeAndTypeArgs(p.Type, declaredSet, outgoing);
                break;
            case IPropertySymbol ps:
                AddTypeAndTypeArgs(ps.Type, declaredSet, outgoing);
                break;
            case IFieldSymbol fs:
                AddTypeAndTypeArgs(fs.Type, declaredSet, outgoing);
                break;
            case INamedTypeSymbol nts:
                break;
        }
    }

    // attributes on type
    foreach (var attr in type.GetAttributes()) AddTypeAndTypeArgs(attr.AttributeClass, declaredSet, outgoing);

    // For syntax-level constructs: object creation and typeof and member accesses
    foreach (var decl in type.DeclaringSyntaxReferences)
    {
        var node = await decl.GetSyntaxAsync().ConfigureAwait(false);
        var tree = node.SyntaxTree;
        var comp = compilations[compName];
        var model = comp.GetSemanticModel(tree);
        // object creations
        foreach (var oc in node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var tinfo = model.GetTypeInfo(oc);
            if (tinfo.Type is INamedTypeSymbol nts && declaredSet.Contains(nts)) outgoing.Add(nts);
        }
        // typeof
        foreach (var tof in node.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            var tinfo = model.GetTypeInfo(tof.Type);
            if (tinfo.Type is INamedTypeSymbol nts && declaredSet.Contains(nts)) outgoing.Add(nts);
        }
        // member access
        foreach (var ma in node.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var sym = model.GetSymbolInfo(ma).Symbol;
            if (sym != null)
            {
                var container = sym.ContainingType;
                if (container != null && declaredSet.Contains(container)) outgoing.Add(container);
            }
        }
    }

    // Ce_type is outgoing distinct declared types
    var Ce_type = outgoing.Count;

    // we'll compute Ca_type (incoming) after building map
    typeMetrics.Add(new TypeMetric{Symbol=type, Component=compName, Namespace=type.ContainingNamespace?.ToDisplayString() ?? "", TypeName=type.Name, Kind=type.TypeKind.ToString(), Outgoing=outgoing});
}

// compute incoming counts (Ca_type)
var symbolToMetric = typeMetrics.ToDictionary(t => t.Symbol, t => t, SymbolEqualityComparer.Default);
foreach (var tm in typeMetrics)
{
    foreach (var outSym in tm.Outgoing)
    {
        if (symbolToMetric.TryGetValue(outSym, out var target)) target.Incoming.Add(tm.Symbol);
    }
}

// finalize metrics and compute API abstraction and CIC
foreach (var tm in typeMetrics)
{
    tm.Ce = tm.Outgoing.Count;
    tm.Ca = tm.Incoming.Count;
    tm.I = (tm.Ca + tm.Ce) == 0 ? 0.0 : (double)tm.Ce / (tm.Ca + tm.Ce);

    // API abstraction: public constructors and methods parameters
    int abstractParams = 0, concreteParams = 0, ignoredParams = 0;
    if (compilations.TryGetValue(tm.Component, out var comp))
    {
        foreach (var syntaxRef in tm.Symbol.DeclaringSyntaxReferences)
        {
            var node = await syntaxRef.GetSyntaxAsync().ConfigureAwait(false);
            var model = comp.GetSemanticModel(node.SyntaxTree);
            // constructors
            foreach (var ctor in tm.Symbol.Constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
            {
                foreach (var p in ctor.Parameters) ClassifyParam(p.Type, ref abstractParams, ref concreteParams, ref ignoredParams, cfg, declaredSet);
            }
            // public methods
            foreach (var m in tm.Symbol.GetMembers().OfType<IMethodSymbol>().Where(m=>m.DeclaredAccessibility==Accessibility.Public))
            {
                foreach (var p in m.Parameters) ClassifyParam(p.Type, ref abstractParams, ref concreteParams, ref ignoredParams, cfg, declaredSet);
            }
            if (cfg.AnalyzePropertyParameters)
            {
                foreach (var prop in tm.Symbol.GetMembers().OfType<IPropertySymbol>().Where(p=>p.DeclaredAccessibility==Accessibility.Public))
                {
                    if (prop.SetMethod != null && prop.SetMethod.DeclaredAccessibility == Accessibility.Public) ClassifyParam(prop.Type, ref abstractParams, ref concreteParams, ref ignoredParams, cfg, declaredSet);
                }
            }
        }
    }

    tm.AbstractParamCount = abstractParams;
    tm.ConcreteParamCount = concreteParams;
    tm.IgnoredParamCount = ignoredParams;
    tm.A_api = (abstractParams + concreteParams) == 0 ? (double?)null : (double)abstractParams / (abstractParams + concreteParams);
    tm.D_api = tm.A_api.HasValue ? Math.Abs(tm.A_api.Value + tm.I - 1.0) : (double?)null;

    // CIC: count distinct concrete instantiations (object creation) inside the type
    var cicSet = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    if (compilations.TryGetValue(tm.Component, out var comp2))
    {
        foreach (var decl in tm.Symbol.DeclaringSyntaxReferences)
        {
            var node = await decl.GetSyntaxAsync().ConfigureAwait(false);
            var model = comp2.GetSemanticModel(node.SyntaxTree);
            foreach (var oc in node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var tinfo = model.GetTypeInfo(oc);
                if (tinfo.Type is INamedTypeSymbol nts && !IsIgnoredType(nts, cfg) && declaredSet.Contains(nts) && nts.TypeKind == TypeKind.Class && !nts.IsAbstract) cicSet.Add(nts);
            }
        }
    }
    tm.CIC = cicSet.Count;

}

// finalize CSVs and MD
Directory.CreateDirectory(Path.Combine(repoRoot,"artifacts"));
var compCsv = Path.Combine(repoRoot,"artifacts","clean-arch-components.csv");
var compMd = Path.Combine(repoRoot,"artifacts","clean-arch-components.md");
var typeCsv = Path.Combine(repoRoot,"artifacts","clean-arch-types.csv");
var typeMd = Path.Combine(repoRoot,"artifacts","clean-arch-types.md");

// write components CSV
var sbComp = new StringBuilder();
sbComp.AppendLine("Component,Ca,Ce,I,A,D,Abstracts,Totals");
foreach (var c in compMetrics)
    sbComp.AppendLine($"{Escape(c.name)},{c.Ca},{c.Ce},{c.I:F4},{c.A:F4},{c.D:F4},{c.Abstracts},{c.Totals}");
File.WriteAllText(compCsv, sbComp.ToString());

// components MD with explanations and hotspot
var sbCompMd = new StringBuilder();
sbCompMd.AppendLine("# Component-level Clean Architecture Metrics\n");
sbCompMd.AppendLine("Formulas:\n");
sbCompMd.AppendLine("- I = Ce / (Ca + Ce) — Instability\n- A = (abstract classes + interfaces) / total types — Abstractness\n- D = |A + I - 1| — Distance from the ideal zone\n");
sbCompMd.AppendLine("## Components (sorted by D desc, then Ce desc)\n");
sbCompMd.AppendLine("|Component|Ca|Ce|I|A|D|Abstracts|Totals|");
sbCompMd.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
foreach (var c in compMetrics)
    sbCompMd.AppendLine($"|{c.name}|{c.Ca}|{c.Ce}|{c.I:F4}|{c.A:F4}|{c.D:F4}|{c.Abstracts}|{c.Totals}|");

// hotspots
sbCompMd.AppendLine("\n## Hotspots\n");
sbCompMd.AppendLine("- D >= 0.5:\n");
foreach (var c in compMetrics.Where(x=>x.D>=0.5)) sbCompMd.AppendLine($"  - {c.name} (D={c.D:F2})");
sbCompMd.AppendLine("\n- Stable concrete (I near 0 with low A):\n");
foreach (var c in compMetrics.Where(x=>x.I < 0.2 && x.A < 0.25)) sbCompMd.AppendLine($"  - {c.name} (I={c.I:F2}, A={c.A:F2})");
sbCompMd.AppendLine("\n- Volatile concrete (I near 1 with low A):\n");
foreach (var c in compMetrics.Where(x=>x.I > 0.8 && x.A < 0.25)) sbCompMd.AppendLine($"  - {c.name} (I={c.I:F2}, A={c.A:F2})");

File.WriteAllText(compMd, sbCompMd.ToString());

// write types CSV
var sbType = new StringBuilder();
sbType.AppendLine("Component,Namespace,TypeName,Kind,Ca_type,Ce_type,I_type,abstract_param_count,concrete_param_count,ignored_param_count,A_api,D_api,CIC");
foreach (var t in typeMetrics.OrderBy(t=>t.Component).ThenBy(t=>t.Namespace).ThenBy(t=>t.TypeName))
{
    sbType.AppendLine($"{Escape(t.Component)},{Escape(t.Namespace)},{Escape(t.TypeName)},{t.Kind},{t.Ca},{t.Ce},{t.I:F4},{t.AbstractParamCount},{t.ConcreteParamCount},{t.IgnoredParamCount},{(t.A_api.HasValue?t.A_api.Value.ToString("F4"):"")},{(t.D_api.HasValue?t.D_api.Value.ToString("F4"):"")},{t.CIC}");
}
File.WriteAllText(typeCsv, sbType.ToString());

// types MD with explanations, hotspots and ASCII plot of A_api vs D_api
var sbTypeMd = new StringBuilder();
sbTypeMd.AppendLine("# Type-level Clean Architecture Metrics\n");
sbTypeMd.AppendLine("Formulas:\n");
sbTypeMd.AppendLine("- I_type = Ce_type / (Ca_type + Ce_type)\n- A_api = abstract_param_count / (abstract_param_count + concrete_param_count) (null if denominator=0)\n- D_api = |A_api + I_type - 1|\n");

// hotspots
sbTypeMd.AppendLine("## Hotspots\n");
sbTypeMd.AppendLine("- D_api >= 0.5:\n");
foreach (var t in typeMetrics.Where(x=>x.D_api.HasValue && x.D_api.Value>=0.5).OrderByDescending(x=>x.D_api)) sbTypeMd.AppendLine($"  - {t.Component}.{t.TypeName} (D_api={t.D_api:F2})");

// top 5% Ce_type
var topCeThreshold = (int)Math.Ceiling(typeMetrics.Count * 0.05);
if (topCeThreshold < 1) topCeThreshold = 1;
foreach (var t in typeMetrics.OrderByDescending(x=>x.Ce).Take(topCeThreshold)) sbTypeMd.AppendLine($"- High Ce_type: {t.Component}.{t.TypeName} (Ce={t.Ce})");

// A_api <= 0.2 with high Ca
foreach (var t in typeMetrics.Where(x=>x.A_api.HasValue && x.A_api.Value<=0.2).OrderByDescending(x=>x.Ca).Take(10)) sbTypeMd.AppendLine($"- Low A_api with high Ca: {t.Component}.{t.TypeName} (A_api={t.A_api:F2}, Ca={t.Ca})");

// A_api >= 0.8 and CIC > 0
foreach (var t in typeMetrics.Where(x=>x.A_api.HasValue && x.A_api.Value>=0.8 && x.CIC>0)) sbTypeMd.AppendLine($"- High A_api but constructs concretes: {t.Component}.{t.TypeName} (A_api={t.A_api:F2}, CIC={t.CIC})");

// ASCII plot A_api vs D_api (50x50)
int size = 50;
var grid = Enumerable.Range(0,size).Select(_ => Enumerable.Repeat('.', size).ToArray()).ToArray();
var palette = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
var key = new List<(char,string)>();
var plotList = typeMetrics.Where(t=>t.A_api.HasValue && t.D_api.HasValue).OrderBy(t=>t.Component).ThenBy(t=>t.TypeName).ToList();
for (int i=0;i<plotList.Count;i++)
{
    var t = plotList[i];
    double xa = t.A_api!.Value; // 0..1
    double ya = t.D_api!.Value; // 0..1
    int x = Math.Clamp((int)Math.Round(xa * (size-1)), 0, size-1);
    int y = Math.Clamp((int)Math.Round((1.0 - ya) * (size-1)), 0, size-1);
    char ch = i < palette.Length ? palette[i] : (char)('!' + (i - palette.Length) % 80);
    if (grid[y][x] == '.') grid[y][x] = ch; else grid[y][x] = '*';
    key.Add((ch, $"{t.Component}.{t.TypeName}"));
}

sbTypeMd.AppendLine("\n## A_api vs D_api plot (A_api on X, D_api on Y)\n");
for (int r=0;r<size;r++) sbTypeMd.AppendLine(new string(grid[r]));
sbTypeMd.AppendLine("\nKey:\n");
foreach (var (ch,name) in key) sbTypeMd.AppendLine($"- {ch} : {name}");

File.WriteAllText(typeMd, sbTypeMd.ToString());

Console.WriteLine($"Wrote artifacts to {Path.Combine(repoRoot,"artifacts")}");
return 0;

// ---------- helpers and models ----------
static bool IsTestProject(string projPath)
{
    var name = Path.GetFileNameWithoutExtension(projPath);
    var lowered = name.ToLowerInvariant();
    return lowered.Contains("test") || lowered.Contains("tests") || lowered.Contains("integrationtest");
}

static bool GlobMatch(string pattern, string path)
{
    // simple wildcard * matching
    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
    return Regex.IsMatch(path.Replace('\\','/'), regex, RegexOptions.IgnoreCase);
}

static bool IsSymbolInProject(INamedTypeSymbol sym, string projectName)
{
    foreach (var loc in sym.Locations)
    {
        if (loc.IsInSource && loc.SourceTree != null && loc.SourceTree.FilePath.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0) return true;
    }
    // fallback: nearest syntax reference
    return sym.DeclaringSyntaxReferences.Length > 0;
}

static void AddTypeAndTypeArgs(ITypeSymbol? t, HashSet<INamedTypeSymbol> declaredSet, HashSet<INamedTypeSymbol> outgoing)
{
    if (t == null) return;
    if (t is INamedTypeSymbol nts)
    {
        if (declaredSet.Contains(nts)) outgoing.Add(nts);
        foreach (var arg in nts.TypeArguments) AddTypeAndTypeArgs(arg, declaredSet, outgoing);
    }
    else if (t is IArrayTypeSymbol ats) AddTypeAndTypeArgs(ats.ElementType, declaredSet, outgoing);
}

static void ClassifyParam(ITypeSymbol t, ref int abstractCount, ref int concreteCount, ref int ignoredCount, Config cfg, HashSet<INamedTypeSymbol> declaredSet)
{
    if (IsIgnoredType(t, cfg)) { ignoredCount++; return; }
    if (t is INamedTypeSymbol nts)
    {
        if (nts.TypeKind == TypeKind.Interface || (nts.TypeKind == TypeKind.Class && nts.IsAbstract)) { abstractCount++; return; }
        if (nts.TypeKind == TypeKind.TypeParameter)
        {
            // check constraints
            foreach (var c in nts.ConstraintTypes) if (c.TypeKind==TypeKind.Interface || (c.TypeKind==TypeKind.Class && ((INamedTypeSymbol)c).IsAbstract)) { abstractCount++; return; }
        }
        // otherwise concrete
        concreteCount++;
        foreach (var arg in nts.TypeArguments) ClassifyParam(arg, ref abstractCount, ref concreteCount, ref ignoredCount, cfg, declaredSet);
        return;
    }
    // arrays and others
    if (t is IArrayTypeSymbol ats) { ClassifyParam(ats.ElementType, ref abstractCount, ref concreteCount, ref ignoredCount, cfg, declaredSet); return; }
    // fallback
    ignoredCount++;
}

static bool IsIgnoredType(ITypeSymbol t, Config cfg)
{
    if (t == null) return true;
    if (t.TypeKind == TypeKind.Enum) return true;
    if (t.IsValueType && cfg.TreatStructsAsIgnored) return true;
    var full = t.ToDisplayString();
    foreach (var p in cfg.IgnoreTypePrefixes ?? Array.Empty<string>()) if (full.StartsWith(p)) return true;
    foreach (var s in cfg.IgnoreTypes ?? Array.Empty<string>()) if (string.Equals(t.Name, s, StringComparison.OrdinalIgnoreCase) || full.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
    // primitive types
    if (t.SpecialType != SpecialType.None) return true;
    return false;
}

static string Escape(string s) => s.Contains(',') ? '"'+s.Replace("\"","\"\"")+'"' : s;

class Config
{
    public string[]? IncludeProjects { get; set; }
    public string[]? ExcludeProjects { get; set; }
    public string[]? IgnoreTypePrefixes { get; set; }
    public string[]? IgnoreTypes { get; set; }
    public bool TreatStructsAsIgnored { get; set; } = true;
    public bool AnalyzePropertyParameters { get; set; } = false;
}

class ComponentMetric { public string name=""; public int Ca; public int Ce; public double I; public double A; public double D; public int Abstracts; public int Totals; }

class TypeMetric
{
    public INamedTypeSymbol Symbol = default!;
    public string Component = "";
    public string Namespace = "";
    public string TypeName = "";
    public string Kind = "";
    public HashSet<INamedTypeSymbol> Outgoing = new(SymbolEqualityComparer.Default);
    public HashSet<INamedTypeSymbol> Incoming = new(SymbolEqualityComparer.Default);
    public int Ce => Outgoing.Count;
    public int Ca => Incoming.Count;
    public double I;
    public int AbstractParamCount;
    public int ConcreteParamCount;
    public int IgnoredParamCount;
    public double? A_api;
    public double? D_api;
    public int CIC;
}
