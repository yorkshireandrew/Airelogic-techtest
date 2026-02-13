Git Class Churn
================

Tool that finds the top classes/files changed most on the current branch.

Usage:

```powershell
dotnet run --project tools/git_class_churn/GitClassChurn.csproj
```

Output:
- artifacts/git-class-churn.csv
- artifacts/git-class-churn.md

Notes: Requires `git` on PATH. Counts changes since the merge-base with a sensible remote/main/master candidate; if none found, analyzes full history.
