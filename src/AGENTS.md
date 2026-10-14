# Portable C# source

Follow root AGENTS.md and docs/ARCHITECTURE.md. Runtime libraries target netstandard2.1/C# 9; do not use UnityEngine, platform APIs or newer runtime APIs. Core/contracts have no NuGet references. Constructor dependencies are readonly and null-validated; constructors do not start IO. Use Task/CancellationToken and never block async work. Keep domain, wire and row types distinct. Review ProjectReferences and architecture.json together; native/Unity compatibility needs separate evidence.

## File organization for new features and refactors

- Put each independently useful handwritten public top-level class, interface,
  struct or enum in `TypeName.cs` by default. Keep it in its owning project,
  namespace and feature directory; organize related files through folders.
- Keep service implementations separate from public ports, request/response
  models and independently reusable contracts. Avoid growing catch-all
  `Contracts.cs`, `Models.cs` or `Types.cs` bundles as features expand.
- Keep private/nested helpers with their owner. Small internal SQL rows or
  diagnostic checkpoints used by one implementation may stay beside it.
  Shared internal validation/utilities should have a discoverable named file.
- A tiny enum, exception or trivial companion may stay with its primary type
  when that makes the concept easier to read. Make exceptions deliberate and
  explain a new grouping in the change description; do not split every tiny
  declaration merely to satisfy a count or line limit.
- Leave generated files under their generator's organization and vendor code
  under upstream organization. Change generator output only in an explicitly
  scoped generator task. Tests may keep local fixture/fake types together.
- Do not introduce partial classes merely to divide a large implementation.
  Use separate responsibilities when behavior warrants them, under a separate
  refactor scope.
- For layout-only moves, preserve type names, namespaces, visibility, generic
  constraints, members, documentation, attributes, serialization metadata,
  imports and applicable preprocessor/nullable context. Check references to old
  file paths, compare moved declarations and run affected validation.
- Preserve unrelated edits. Exclude a dirty source file from a layout-only
  refactor unless its owner has explicitly coordinated the change.

The 2026-10-14 cleanup and deliberate existing exceptions are recorded in
`docs/implementation/sdk-file-layout/RESULTS.md` and its type move map. These
rules apply to future SDK feature agents; legacy exceptions do not authorize
new broad model/contract bundles or unrelated cleanup.
