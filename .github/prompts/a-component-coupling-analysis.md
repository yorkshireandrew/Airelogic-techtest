---
agent: a-architect
description: Analysis of coupling of components
---

# Intent
You are acting as a codebase analyst. I want you to ADD automation to this repository that computes Robert C. Martin component dependency metrics.

# Goal
- Compute these metrics per “component”:
  - Ca (Afferent Coupling): number of *external* components that depend on this component
  - Ce (Efferent Coupling): number of *external* components this component depends on
  - I = Ce / (Ca + Ce)
  - A = (# abstract types + interfaces) / (total types)   [for languages where this makes sense]
- Produce:
  - Plot Abstractness vs Instability as a 50x50 character ASCII plot followed by a key table identifying each component (Use a unique character to identify each component in the plot, derived from thecomponent name)
  - In your response list components that have both high A and I (both above 0.75) suggest those components might be useless.
  - In your response list components that have low A and I (both below 0.25) suggest those components as "high coupling with few abstractions"  


# How to define “component”
- Prefer: each top-level project/module/package:
  - For .NET: each .csproj is a component (assembly)
  - For Python: each top-level package directory under src/ (or the repo root if no src/)
- If neither structure exists, fall back to: each top-level directory under src/ as a component.

# How to detect dependencies
- For .NET:
  - Parse .sln and/or find *.csproj.
  - Dependencies:
    - ProjectReference edges: component -> referenced component
    - Additionally infer code-level edges by parsing `using`/fully-qualified type references only if needed, but ProjectReference is the primary source.
  - Abstractness:
    - Use Roslyn to count declared types in each project:
      - abstract classes + interfaces as “abstract types”
      - total classes + structs + records + interfaces as “total types”
- For Python:
  - Find python packages (pyproject.toml, setup.cfg, requirements are NOT reliable for internal deps).
  - Internal dependency edges come from import statements:
    - `import x.y`, `from x.y import z` determine component of the imported module.
  - Abstractness:
    - Set A to null (or 0) unless typing.Protocol / abc.ABC patterns are detected; prefer null for correctness.


# Process
- for each component in the solution calculate the following:
  - Afferent Coupling (Ca) – number of classes outside a component that depend on classes inside it.
  - Efferent Coupling (Ce) – number of classes inside a component that depend on classes outside it.
  - Instability (I) = (Ce) / ((Ca)+(Ce))
  - Abstractness (A) = (Number of abstract classes and interfaces) / (Total number of classes)
- Plot Abstractness vs Instability as a 50x50 character ASCII plot with a key identifying each component.
- 


