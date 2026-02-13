using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

var repoRoot = Directory.GetCurrentDirectory();
Console.WriteLine($"Repository root: {repoRoot}");

// determine current branch
string RunGit(params string[] args)
{
    var psi = new ProcessStartInfo("git", string.Join(' ', args)) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = repoRoot };
    using var p = Process.Start(psi)!;
    var outp = p.StandardOutput.ReadToEnd();
    var err = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new Exception($"git {string.Join(' ', args)} failed: {err}");
    return outp;
}

string branch = "HEAD";
try { branch = RunGit("rev-parse --abbrev-ref HEAD").Trim(); } catch (Exception) { branch = "HEAD"; }
Console.WriteLine($"Current branch: {branch}");

// analyze full history (not limited by merge-base)
Console.WriteLine("Analyzing full history (no merge-base)");
string? mergeBase = null;

// collect changed .cs files across full history
var fileCounts = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
{
    // get list of changed files per commit (no merges) across full history
    var raw = RunGit($"log --name-only --pretty=format:%H --no-merges");
    var lines = raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
        if (Regex.IsMatch(line.Trim(), "^[0-9a-f]{7,40}$", RegexOptions.IgnoreCase)) continue; // commit hash
        var f = line.Trim();
        if (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(Path.Combine(repoRoot, f));
            if (!fileCounts.ContainsKey(full)) fileCounts[full]=0;
            fileCounts[full]++;
        }
    }
}

Console.WriteLine($"Found {fileCounts.Count} changed C# files in range.");

// map files to classes
var classCounts = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
var classFiles = new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
var typeRegex = new Regex(@"\b(?:public|internal|protected|private|static|abstract|sealed|partial|\s)+\s*(class|struct|interface|record)\s+([A-Za-z0-9_<>]+)", RegexOptions.Compiled);

foreach (var kv in fileCounts)
{
    var file = kv.Key; var count = kv.Value;
    string src = string.Empty;
    var exists = File.Exists(file);
    if (exists)
    {
        try { src = File.ReadAllText(file); } catch { src = string.Empty; }
    }
    src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
    src = Regex.Replace(src, @"//.*?$", "", RegexOptions.Multiline);
    var nsm = Regex.Match(src, @"namespace\s+([A-Za-z0-9_.]+)");
    var ns = nsm.Success ? nsm.Groups[1].Value.Trim() : "";
    var found = false;
    foreach (Match m in typeRegex.Matches(src))
    {
        found = true;
        var typeName = m.Groups[2].Value;
        var full = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName;
        if (!classCounts.ContainsKey(full)) classCounts[full]=0;
        classCounts[full]+=count;
        if (!classFiles.ContainsKey(full)) classFiles[full]=new List<string>();
        classFiles[full].Add(file);
    }
    if (!found)
    {
        // if file no longer exists or contains no type declarations, attribute counts to filename-based placeholder
        var baseName = Path.GetFileNameWithoutExtension(file);
        var key = string.IsNullOrEmpty(baseName) ? file : baseName;
        if (!classCounts.ContainsKey(key)) classCounts[key]=0;
        classCounts[key]+=count;
        if (!classFiles.ContainsKey(key)) classFiles[key]=new List<string>();
        classFiles[key].Add(file);
    }
}

var top = classCounts.OrderByDescending(kv=>kv.Value).Take(30).ToList();
Directory.CreateDirectory(Path.Combine(repoRoot, "artifacts"));
var csv = Path.Combine(repoRoot, "artifacts", "git-class-churn.csv");
var md = Path.Combine(repoRoot, "artifacts", "git-class-churn.md");
var sb = new StringBuilder(); sb.AppendLine("Class,ChangeCount,Files");

// Prepare rows and compute column widths for padded Markdown table (no full paths)
var rows = new List<(string Rank, string Class, string Changes, string AvgWeekly, string Files)>();
int rnk = 1;
foreach (var kv in top)
{
    var cls = kv.Key; var cnt = kv.Value;
    var filesList = classFiles.ContainsKey(cls) ? classFiles[cls].Distinct().ToList() : new List<string>();
    var filesOnly = filesList.Select(f => Path.GetFileName(f));
    var files = string.Join("; ", filesOnly);
    // compute earliest commit among associated files
    DateTimeOffset? earliest = null;
    foreach (var fpath in filesList)
    {
        try
        {
            var rel = Path.GetRelativePath(repoRoot, fpath).Replace('\\','/');
            var outp = RunGit($"log --follow --format=%at -- \"{rel}\"");
            var lines2 = outp.Split(new[] {'\n','\r'}, StringSplitOptions.RemoveEmptyEntries);
            if (lines2.Length>0)
            {
                // oldest is last
                var oldestSec = long.Parse(lines2.Last());
                var dt = DateTimeOffset.FromUnixTimeSeconds(oldestSec).ToUniversalTime();
                if (!earliest.HasValue || dt < earliest.Value) earliest = dt;
            }
        }
        catch { }
    }
    var created = earliest ?? DateTimeOffset.UtcNow;
    var weeks = (DateTimeOffset.UtcNow - created).TotalDays / 7.0;
    if (weeks <= 0) weeks = 1e-6;
    var avg = Math.Round((double)cnt / weeks, 3);
    sb.AppendLine($"{Escape(cls)},{cnt},{avg:F3},{Escape(files)}");
    rows.Add((rnk.ToString(), cls, cnt.ToString(), avg.ToString("F3"), files));
    rnk++;
}

// compute widths
var rankHeader = "Rank"; var classHeader = "Class"; var changesHeader = "Changes"; var avgHeader = "Avg/Week"; var filesHeader = "Files";
int wRank = Math.Max(rankHeader.Length, rows.Any() ? rows.Max(x => x.Rank.Length) : rankHeader.Length);
int wClass = Math.Max(classHeader.Length, rows.Any() ? rows.Max(x => x.Class.Length) : classHeader.Length);
int wChanges = Math.Max(changesHeader.Length, rows.Any() ? rows.Max(x => x.Changes.Length) : changesHeader.Length);
int wAvg = Math.Max(avgHeader.Length, rows.Any() ? rows.Max(x => x.AvgWeekly.Length) : avgHeader.Length);
int wFiles = Math.Max(filesHeader.Length, rows.Any() ? rows.Max(x => x.Files.Length) : filesHeader.Length);

var sbmd = new StringBuilder();
sbmd.AppendLine("# Top 30 changed classes/files on current branch");
sbmd.AppendLine("");
// header with padding (Avg after Changes)
sbmd.AppendLine($"| {rankHeader.PadLeft(wRank)} | {classHeader.PadRight(wClass)} | {changesHeader.PadLeft(wChanges)} | {avgHeader.PadLeft(wAvg)} | {filesHeader.PadRight(wFiles)} |");
// separator (using dashes matching widths)
sbmd.AppendLine($"| {new string('-', wRank)} | {new string('-', wClass)} | {new string('-', wChanges)} | {new string('-', wAvg)} | {new string('-', wFiles)} |");

// rows with aligned padding (rank, changes, avg right-aligned)
foreach (var row in rows)
{
    var rankCell = row.Rank.PadLeft(wRank);
    var classCell = row.Class.PadRight(wClass);
    var changesCell = row.Changes.PadLeft(wChanges);
    var avgCell = row.AvgWeekly.PadLeft(wAvg);
    var filesCell = row.Files.PadRight(wFiles);
    // escape pipe in files
    filesCell = filesCell.Replace("|", "\\|");
    sbmd.AppendLine($"| {rankCell} | {classCell} | {changesCell} | {avgCell} | {filesCell} |");
}

File.WriteAllText(csv, sb.ToString());
File.WriteAllText(md, sbmd.ToString());

Console.WriteLine($"Wrote {rows.Count} entries to {csv} and {md}");

// --- Density table: changes per current total lines (changes/lines) ---
var densityList = new List<(string Class, int Changes, int Lines, double Density)>();
foreach (var kv in classCounts)
{
    var cls = kv.Key; var cnt = kv.Value;
    var filesList = classFiles.ContainsKey(cls) ? classFiles[cls].Distinct().ToList() : new List<string>();
    int totalLines = 0;
    foreach (var f in filesList)
    {
        try
        {
            if (File.Exists(f))
            {
                var ln = File.ReadAllLines(f).Length;
                // exclude files smaller than 5 lines
                if (ln >= 5) totalLines += ln;
            }
        }
        catch { }
    }
    // skip classes that have no qualifying files (after excluding small files)
    if (totalLines < 5) continue;
    var density = Math.Round((double)cnt / (double)totalLines, 3);
    densityList.Add((cls, cnt, totalLines, density));
}

var topDensity = densityList.OrderByDescending(x => x.Density).ThenByDescending(x => x.Changes).Take(30).ToList();
var densityCsv = Path.Combine(repoRoot, "artifacts", "git-class-churn-density.csv");
var densitySb = new StringBuilder();
densitySb.AppendLine("Class,ChangeCount,TotalLines,ChangesPerLine");
foreach (var d in topDensity) densitySb.AppendLine($"{Escape(d.Class)},{d.Changes},{d.Lines},{d.Density:F3}");
File.WriteAllText(densityCsv, densitySb.ToString());

// Append a padded Markdown table for density
var rkH = "Rank"; var clsH = "Class"; var chH = "Changes"; var linesH = "Lines"; var densH = "Changes/Line";
int wR = Math.Max(rkH.Length, topDensity.Any() ? topDensity.Count.ToString().Length : rkH.Length);
int wC = Math.Max(clsH.Length, topDensity.Any() ? topDensity.Max(x => x.Class.Length) : clsH.Length);
int wCh = Math.Max(chH.Length, topDensity.Any() ? topDensity.Max(x => x.Changes.ToString().Length) : chH.Length);
int wL = Math.Max(linesH.Length, topDensity.Any() ? topDensity.Max(x => x.Lines.ToString().Length) : linesH.Length);
int wD = Math.Max(densH.Length, topDensity.Any() ? topDensity.Max(x => x.Density.ToString("F3").Length) : densH.Length);

var sbdens = new StringBuilder();
sbdens.AppendLine("");
sbdens.AppendLine("## Top 30 by changes relative to current size (changes/lines)");
sbdens.AppendLine("");
sbdens.AppendLine($"| {rkH.PadLeft(wR)} | {clsH.PadRight(wC)} | {chH.PadLeft(wCh)} | {linesH.PadLeft(wL)} | {densH.PadLeft(wD)} |");
sbdens.AppendLine($"| {new string('-', wR)} | {new string('-', wC)} | {new string('-', wCh)} | {new string('-', wL)} | {new string('-', wD)} |");
int rr=1;
foreach (var d in topDensity)
{
    var rankCell = rr.ToString().PadLeft(wR);
    var classCell = d.Class.PadRight(wC);
    var changesCell = d.Changes.ToString().PadLeft(wCh);
    var linesCell = d.Lines.ToString().PadLeft(wL);
    var densCell = d.Density.ToString("F3").PadLeft(wD);
    sbdens.AppendLine($"| {rankCell} | {classCell} | {changesCell} | {linesCell} | {densCell} |");
    rr++;
}

File.AppendAllText(md, sbdens.ToString());
Console.WriteLine($"Wrote density CSV to {densityCsv} and appended density table to {md}");

// --- Repeat the two tables but exclude test classes/projects ---
// Build caches for csproj -> isTestProject
var csprojTestCache = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
bool IsCsProjTest(string csprojPath)
{
    if (csprojPath == null) return false;
    if (csprojTestCache.TryGetValue(csprojPath, out var v)) return v;
    var text = string.Empty;
    try { text = File.ReadAllText(csprojPath); } catch { text = string.Empty; }
    var low = text.ToLowerInvariant();
    var tokens = new[] { "moq", "xunit", "nunit", "rhinomock", "nsubstitute" };
    var isTest = tokens.Any(t => low.Contains(t));
    csprojTestCache[csprojPath] = isTest;
    return isTest;
}

string? FindNearestCsproj(string filePath)
{
    try
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir) && dir.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            var projs = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projs.Length > 0) return projs[0];
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }
    }
    catch { }
    return null;
}

bool IsTestClass(string classFullName, List<string> files)
{
    if (string.IsNullOrEmpty(classFullName)) return false;
    if (classFullName.ToLowerInvariant().Contains(".test")) return true;
    foreach (var f in files)
    {
        var csproj = FindNearestCsproj(f);
        if (csproj != null && IsCsProjTest(csproj)) return true;
    }
    return false;
}

// prepare filtered top by changes
var topNoTests = new List<(string Key,int Count)>();
foreach (var kv in classCounts.OrderByDescending(kv=>kv.Value))
{
    var cls = kv.Key; var cnt = kv.Value;
    var filesList = classFiles.ContainsKey(cls) ? classFiles[cls].Distinct().ToList() : new List<string>();
    if (IsTestClass(cls, filesList)) continue;
    topNoTests.Add((cls,cnt));
    if (topNoTests.Count>=30) break;
}

// write CSV and MD for no-tests top (padded columns)
var csvNoTests = Path.Combine(repoRoot, "artifacts", "git-class-churn.no-tests.csv");
var mdNoTests = Path.Combine(repoRoot, "artifacts", "git-class-churn.no-tests.md");
var sbNo = new StringBuilder(); sbNo.AppendLine("Class,ChangeCount,AvgWeekly,Files");

// prepare rows for padding (include Avg/Week)
var noRows = new List<(string Rank, string Class, string Changes, string AvgWeekly, string Files)>();
int idx = 1;
foreach (var kv in topNoTests)
{
    var cls = kv.Key; var cnt = kv.Count;
    var filesListFull = classFiles.ContainsKey(cls) ? classFiles[cls].Distinct().ToList() : new List<string>();
    var filesList = filesListFull.Select(f => Path.GetFileName(f));
    var files = string.Join("; ", filesList);

    // compute earliest commit among associated files for avg/week
    DateTimeOffset? earliest = null;
    foreach (var fpath in filesListFull)
    {
        try
        {
            var rel = Path.GetRelativePath(repoRoot, fpath).Replace('\\','/');
            var outp = RunGit($"log --follow --format=%at -- \"{rel}\"");
            var lines2 = outp.Split(new[] {'\n','\r'}, StringSplitOptions.RemoveEmptyEntries);
            if (lines2.Length>0)
            {
                var oldestSec = long.Parse(lines2.Last());
                var dt = DateTimeOffset.FromUnixTimeSeconds(oldestSec).ToUniversalTime();
                if (!earliest.HasValue || dt < earliest.Value) earliest = dt;
            }
        }
        catch { }
    }
    var created = earliest ?? DateTimeOffset.UtcNow;
    var weeks = (DateTimeOffset.UtcNow - created).TotalDays / 7.0;
    if (weeks <= 0) weeks = 1e-6;
    var avg = Math.Round((double)cnt / weeks, 3);

    sbNo.AppendLine($"{Escape(cls)},{cnt},{avg:F3},{Escape(files)}");
    noRows.Add((idx.ToString(), cls, cnt.ToString(), avg.ToString("F3"), files));
    idx++;
}
File.WriteAllText(csvNoTests, sbNo.ToString());

// compute widths including Avg/Week
var rankH = "Rank"; var classH = "Class"; var changesH = "Changes"; var avgH = "Avg/Week"; var filesH = "Files";
int wRank2 = Math.Max(rankH.Length, noRows.Any() ? noRows.Max(x => x.Rank.Length) : rankH.Length);
int wClass2 = Math.Max(classH.Length, noRows.Any() ? noRows.Max(x => x.Class.Length) : classH.Length);
int wChanges2 = Math.Max(changesH.Length, noRows.Any() ? noRows.Max(x => x.Changes.Length) : changesH.Length);
int wAvg2 = Math.Max(avgH.Length, noRows.Any() ? noRows.Max(x => x.AvgWeekly.Length) : avgH.Length);
int wFiles2 = Math.Max(filesH.Length, noRows.Any() ? noRows.Max(x => x.Files.Length) : filesH.Length);

var sbNoMd = new StringBuilder();
sbNoMd.AppendLine("# Top 30 changed classes/files on current branch (excluding test projects/namespaces)");
sbNoMd.AppendLine("");
sbNoMd.AppendLine($"| {rankH.PadLeft(wRank2)} | {classH.PadRight(wClass2)} | {changesH.PadLeft(wChanges2)} | {avgH.PadLeft(wAvg2)} | {filesH.PadRight(wFiles2)} |");
sbNoMd.AppendLine($"| {new string('-', wRank2)} | {new string('-', wClass2)} | {new string('-', wChanges2)} | {new string('-', wAvg2)} | {new string('-', wFiles2)} |");
foreach (var row in noRows)
{
    var rankCell = row.Rank.PadLeft(wRank2);
    var classCell = row.Class.PadRight(wClass2);
    var changesCell = row.Changes.PadLeft(wChanges2);
    var avgCell = row.AvgWeekly.PadLeft(wAvg2);
    var filesCell = row.Files.PadRight(wFiles2).Replace("|","\\|");
    sbNoMd.AppendLine($"| {rankCell} | {classCell} | {changesCell} | {avgCell} | {filesCell} |");
}
File.WriteAllText(mdNoTests, sbNoMd.ToString());

// prepare filtered density list
var densityNoTests = densityList.Where(d =>
{
    var filesList = classFiles.ContainsKey(d.Class) ? classFiles[d.Class].Distinct().ToList() : new List<string>();
    return !IsTestClass(d.Class, filesList);
}).OrderByDescending(x=>x.Density).ThenByDescending(x=>x.Changes).Take(30).ToList();

var densityNoCsv = Path.Combine(repoRoot, "artifacts", "git-class-churn-density.no-tests.csv");
var sbDensityNo = new StringBuilder(); sbDensityNo.AppendLine("Class,ChangeCount,TotalLines,ChangesPerLine");
foreach (var d in densityNoTests) sbDensityNo.AppendLine($"{Escape(d.Class)},{d.Changes},{d.Lines},{d.Density:F3}");
File.WriteAllText(densityNoCsv, sbDensityNo.ToString());

// append no-tests density table to main md
var sbDensityNoMd = new StringBuilder();
sbDensityNoMd.AppendLine("");
sbDensityNoMd.AppendLine("## Top 30 by changes relative to current size (changes/lines) (excluding test projects/namespaces)");
sbDensityNoMd.AppendLine("");
sbDensityNoMd.AppendLine($"| Rank | Class | Changes | Lines | Changes/Line |");
sbDensityNoMd.AppendLine($"| ---: | --- | ---: | ---: | ---: |");
int r2=1;
foreach (var d in densityNoTests)
{
    sbDensityNoMd.AppendLine($"| {r2} | {d.Class} | {d.Changes} | {d.Lines} | {d.Density:F3} |");
    r2++;
}
// Do not append the no-tests density table to the main md (we write it to the no-tests md file only)

// Also append the no-tests density table to the no-tests md file (padded columns)
try
{
    if (densityNoTests.Any())
    {
        var rankH2 = "Rank"; var classH2 = "Class"; var chH2 = "Changes"; var linesH2 = "Lines"; var densH2 = "Changes/Line";
        int wR2 = Math.Max(rankH2.Length, densityNoTests.Count.ToString().Length);
        int wC2 = Math.Max(classH2.Length, densityNoTests.Max(x => x.Class.Length));
        int wCh2 = Math.Max(chH2.Length, densityNoTests.Max(x => x.Changes.ToString().Length));
        int wL2 = Math.Max(linesH2.Length, densityNoTests.Max(x => x.Lines.ToString().Length));
        int wD2 = Math.Max(densH2.Length, densityNoTests.Max(x => x.Density.ToString("F3").Length));

        var sbNoDensityMd2 = new StringBuilder();
        sbNoDensityMd2.AppendLine();
        sbNoDensityMd2.AppendLine("## Top 30 by changes relative to current size (changes/lines) (excluding test projects/namespaces)");
        sbNoDensityMd2.AppendLine();
        sbNoDensityMd2.AppendLine($"| {rankH2.PadLeft(wR2)} | {classH2.PadRight(wC2)} | {chH2.PadLeft(wCh2)} | {linesH2.PadLeft(wL2)} | {densH2.PadLeft(wD2)} |");
        sbNoDensityMd2.AppendLine($"| {new string('-', wR2)} | {new string('-', wC2)} | {new string('-', wCh2)} | {new string('-', wL2)} | {new string('-', wD2)} |");
        int r3 = 1;
        foreach (var d in densityNoTests)
        {
            var rankCell = r3.ToString().PadLeft(wR2);
            var classCell = d.Class.PadRight(wC2);
            var changesCell = d.Changes.ToString().PadLeft(wCh2);
            var linesCell = d.Lines.ToString().PadLeft(wL2);
            var densCell = d.Density.ToString("F3").PadLeft(wD2);
            sbNoDensityMd2.AppendLine($"| {rankCell} | {classCell} | {changesCell} | {linesCell} | {densCell} |");
            r3++;
        }
        File.AppendAllText(mdNoTests, sbNoDensityMd2.ToString());
        Console.WriteLine($"Appended no-tests density table to {mdNoTests}");
    }
}
catch (Exception) { }

static string Escape(string s) => s?.Contains(',') == true ? '"'+s.Replace("\"","\"\"")+'"' : s ?? "";
