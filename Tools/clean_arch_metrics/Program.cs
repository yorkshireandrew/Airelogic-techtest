using System.Text;
using System.Text.Json;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Roslyn-based clean architecture analyzer. This is the higher-fidelity analyzer that requires package restore.

async Task<int> MainAsync()
{
    var repoRoot = Directory.GetCurrentDirectory();
    var slnPath = Directory.GetFiles(repoRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
    if (slnPath is null)
    {
        Console.Error.WriteLine("No solution file found at repo root; Roslyn analyzer requires a .sln file.");
        return 1;
    }

    MSBuildLocator.RegisterDefaults();

    using var workspace = MSBuildWorkspace.Create();
    workspace.WorkspaceFailed += (s,e) => Console.Error.WriteLine($"Workspace: {e.Diagnostic}");

    Console.WriteLine($"Opening solution {slnPath} ...");
    var solution = await workspace.OpenSolutionAsync(slnPath).ConfigureAwait(false);

    var projects = solution.Projects.Where(p => p.Language == "C#").ToList();
    // filter test projects by folder/name
    projects = projects.Where(p => !IsTestProject(p.FilePath ?? p.Name)).ToList();

    var components = projects.Select(p => p.Name).OrderBy(n=>n).ToList();

    var dependencies = components.ToDictionary(c=>c, c=> new HashSet<string>());
    foreach (var proj in projects)
    {
        var name = proj.Name;
        foreach (var pref in proj.ProjectReferences)
        {
            var target = proj.Solution.GetProject(pref.ProjectId)?.Name;
            if (!string.IsNullOrEmpty(target) && dependencies.ContainsKey(name) && dependencies.ContainsKey(target)) dependencies[name].Add(target);
        }
    }

    var dependents = components.ToDictionary(c=>c, c=> new HashSet<string>());
    foreach (var kv in dependencies) foreach (var to in kv.Value) if (dependents.ContainsKey(to)) dependents[to].Add(kv.Key);

    var declared = new List<INamedTypeSymbol>();
    var projectByType = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

    foreach (var proj in projects)
    {
        var compName = proj.Name;
        var compilation = await proj.GetCompilationAsync().ConfigureAwait(false);
        if (compilation is null) continue;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var nodes = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var nd in nodes)
            {
                var sym = model.GetDeclaredSymbol(nd) as INamedTypeSymbol;
                if (sym==null) continue;
                declared.Add(sym);
                projectByType[sym.ToDisplayString()] = compName;
            }
        }
    }

    // component metrics
    var compMetrics = new List<ComponentMetric>();
    foreach (var comp in components)
    {
        var typesInComp = declared.Where(d => projectByType.TryGetValue(d.ToDisplayString(), out var c) && c==comp).ToList();
        var abstracts = typesInComp.Count(t => t.TypeKind == TypeKind.Interface || t.IsAbstract);
        var totals = typesInComp.Count;
        var Ce = dependencies[comp].Count;
        var Ca = dependents[comp].Count;
        var I = (Ca + Ce) == 0 ? 0.0 : (double)Ce / (Ca + Ce);
        var A = totals==0 ? 0.0 : (double)abstracts / totals;
        var D = Math.Abs(A + I - 1.0);
        compMetrics.Add(new ComponentMetric{ name=comp, Ca=Ca, Ce=Ce, I=I, A=A, D=D, Abstracts=abstracts, Totals=totals });
    }

    // type-level metrics
    var typeMetrics = new List<TypeMetricRoslyn>();
    foreach (var t in declared.OrderBy(d=>d.ToDisplayString()))
    {
        var comp = projectByType.TryGetValue(t.ToDisplayString(), out var c) ? c : "";
        var outgoing = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // base types and interfaces
        if (t.BaseType != null) outgoing.Add(t.BaseType);
        foreach (var inf in t.Interfaces) outgoing.Add(inf);

        // members: parameters and return types
        foreach (var mem in t.GetMembers())
        {
            if (mem is IMethodSymbol m && m.MethodKind==MethodKind.Ordinary)
            {
                if (m.ReturnType is INamedTypeSymbol rtn) outgoing.Add(rtn);
                foreach (var p in m.Parameters) if (p.Type is INamedTypeSymbol pn) outgoing.Add(pn);
            }
            if (mem is IPropertySymbol prop && prop.Type is INamedTypeSymbol pt) outgoing.Add(pt);
            if (mem is IFieldSymbol fld && fld.Type is INamedTypeSymbol ft) outgoing.Add(ft);
        }

        // (optional) could analyze method bodies for object creations and typeof usages here

        // compute Ci/Ca at type level relative to declared set
        var outgoingNames = outgoing.Where(x => x != null).Select(x=>x.ToDisplayString()).Where(n=>projectByType.ContainsKey(n)).Distinct().ToList();
        var outgoingSet = new HashSet<string>(outgoingNames);

        var tm = new TypeMetricRoslyn{ Component=comp, FullName=t.ToDisplayString(), TypeName=t.Name, Namespace=t.ContainingNamespace?.ToDisplayString() ?? "", Kind=t.TypeKind.ToString() };
        tm.Outgoing = outgoingSet;
        typeMetrics.Add(tm);
    }

    var typeByFull = typeMetrics.ToDictionary(t=>t.FullName, t=>t);
    // incoming
    foreach (var t in typeMetrics) foreach (var o in t.Outgoing) if (typeByFull.TryGetValue(o, out var target)) target.Incoming.Add(t.FullName);

    foreach (var t in typeMetrics)
    {
        t.Ce = t.Outgoing.Count;
        t.Ca = t.Incoming.Count;
        t.I = (t.Ca + t.Ce)==0 ? 0.0 : (double)t.Ce / (t.Ca + t.Ce);
        // A_api and D_api approximated by counting interface-typed parameters across methods
        int abstractParams=0, concreteParams=0;
        var project = projects.FirstOrDefault(p => p.Name==t.Component);
        if (project!=null)
        {
            var comp = await project.GetCompilationAsync().ConfigureAwait(false);
            if (comp!=null && comp.GetTypeByMetadataName(t.FullName) is INamedTypeSymbol sym)
            {
                foreach (var mem in sym.GetMembers().OfType<IMethodSymbol>())
                {
                    foreach (var p in mem.Parameters)
                    {
                        if (p.Type.TypeKind==TypeKind.Interface) abstractParams++; else concreteParams++;
                    }
                }
            }
        }
        t.AbstractParamCount = abstractParams; t.ConcreteParamCount = concreteParams;
        t.A_api = (abstractParams + concreteParams)==0 ? (double?)null : (double)abstractParams / (abstractParams + concreteParams);
        t.D_api = t.A_api.HasValue ? Math.Abs(t.A_api.Value + t.I - 1.0) : (double?)null;
    }

    // write artifacts
    Directory.CreateDirectory(Path.Combine(repoRoot,"artifacts"));
    var compCsv = Path.Combine(repoRoot,"artifacts","clean-arch-components.csv");
    var typeCsv = Path.Combine(repoRoot,"artifacts","clean-arch-types.csv");

    var sb = new StringBuilder();
    sb.AppendLine("Component,Ca,Ce,I,A,D,Abstracts,Totals");
    foreach (var c in compMetrics) sb.AppendLine($"{Escape(c.name)},{c.Ca},{c.Ce},{c.I:F4},{c.A:F4},{c.D:F4},{c.Abstracts},{c.Totals}");
    File.WriteAllText(compCsv, sb.ToString());

    var sbt = new StringBuilder();
    sbt.AppendLine("Component,Namespace,TypeName,Kind,Ca_type,Ce_type,I_type,abstract_param_count,concrete_param_count,ignored_param_count,A_api,D_api,CIC");
    foreach (var t in typeMetrics.OrderBy(t=>t.Component).ThenBy(t=>t.TypeName)) sbt.AppendLine($"{Escape(t.Component)},{Escape(t.Namespace)},{Escape(t.TypeName)},{t.Kind},{t.Ca},{t.Ce},{t.I:F4},{t.AbstractParamCount},{t.ConcreteParamCount},0,{(t.A_api.HasValue?t.A_api.Value.ToString("F4") : "")},{(t.D_api.HasValue?t.D_api.Value.ToString("F4") : "")},{0}");
    File.WriteAllText(typeCsv, sbt.ToString());

    Console.WriteLine($"Wrote artifacts to {Path.Combine(repoRoot,"artifacts")} (Roslyn analysis)");
    return 0;
}

MainAsync().GetAwaiter().GetResult();

static bool IsTestProject(string projPath)
{
    if (projPath==null) return false;
    var filename = Path.GetFileNameWithoutExtension(projPath);
    var nl = filename.ToLowerInvariant();
    var pathLower = projPath.ToLowerInvariant().Replace('/', '\\');
    if (pathLower.Contains("\\test\\") || pathLower.Contains("\\tests\\")) return true;
    if (nl.Contains(".test") || nl.Contains(".tests") || nl.Contains("-test") || nl.EndsWith("testproject")) return true;
    return false;
}

static string Escape(string s) => s?.Contains(',') == true ? '"'+s.Replace("\"","\"\"")+'"' : s ?? "";

class ComponentMetric { public string name=""; public int Ca; public int Ce; public double I; public double A; public double D; public int Abstracts; public int Totals; }
class TypeMetricRoslyn { public string Component=""; public string Namespace=""; public string TypeName=""; public string FullName=""; public string Kind=""; public HashSet<string> Outgoing = new(); public HashSet<string> Incoming = new(); public int Ce; public int Ca; public double I; public int AbstractParamCount; public int ConcreteParamCount; public double? A_api; public double? D_api; }
