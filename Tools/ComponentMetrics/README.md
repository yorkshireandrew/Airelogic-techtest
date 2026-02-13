# Component Metrics Tool

This small tool computes Robert C. Martin component metrics (Ca, Ce, A, I) for the C# projects in the repository and renders a 50x50 ASCII plot of Abstractness vs Instability.

Run from the repo root:

```bash
dotnet run --project Tools/ComponentMetrics/ComponentMetrics.csproj
```

Notes:
- This implementation is offline-friendly and does NOT use Roslyn or external NuGet packages. It parses `.csproj` files for `ProjectReference` entries and scans `.cs` files with a lightweight regex-based approach to estimate type counts.
- Abstractness `A` is computed as (abstract classes + interfaces) / total types. If a project has zero types A=0.
- Instability `I` uses project references (ProjectReference) to compute Ca and Ce.
- The scanner is an approximation (no semantic analysis). It strips comments and looks for `class`, `interface`, `struct`, `record` declarations and `abstract class` occurrences.

If you later prefer a more precise analysis (Roslyn), we can switch back to the Roslyn-based implementation, but that requires network access to restore NuGet packages or having those packages available in a local feed.
