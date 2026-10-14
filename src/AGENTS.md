# Portable C# source

Follow root AGENTS.md and docs/ARCHITECTURE.md. Runtime libraries target netstandard2.1/C# 9; do not use UnityEngine, platform APIs or newer runtime APIs. Core/contracts have no NuGet references. Constructor dependencies are readonly and null-validated; constructors do not start IO. Use Task/CancellationToken and never block async work. Keep domain, wire and row types distinct. Review ProjectReferences and architecture.json together; native/Unity compatibility needs separate evidence.
