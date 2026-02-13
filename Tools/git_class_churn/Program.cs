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

static string Escape(string s) => s?.Contains(',') == true ? '"'+s.Replace("\"","\"\"")+'"' : s ?? "";
