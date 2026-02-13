# Clean Architecture Metrics

Tool to compute component- and type-level Clean Architecture metrics.

Run locally:

```bash
dotnet run --project tools/clean_arch_metrics/CleanArchMetrics.csproj
```

Config: `tools/clean_arch_metrics/config.json` (optional). If absent sensible defaults are used.

Outputs will be written to `artifacts/`:
- `clean-arch-components.csv` / `.md`
- `clean-arch-types.csv` / `.md`

This tool uses Roslyn to perform semantic analysis — it requires the .NET SDK and access to restore NuGet packages in CI.
