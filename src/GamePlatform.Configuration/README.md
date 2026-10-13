# Optional remote configuration core

First-party netstandard2.1/C#9 immutable models, bounded restricted JSON reader,
canonical bytes/SHA256, strict version parsing, pure ordered selector and generation
fenced activation helpers. Zero project and runtime package references. Build and
evaluate without accounts, login, mobile SDK, networking, UnityEngine or persistence.

Use `ConfigurationReader.Read(bytes, app, environment)`, then repeatedly call
`Evaluator.Evaluate(compiled, key, context)`. Caller context explicitly supplies
source/freshness/verification. Unknown values never match inequality. Percentage
SHA1 allocation is deterministic selection only; backend confirmation owns grants.

See `docs/implementation/remote-config/` for the pinned contract, exact standalone
tests/dependency evidence and limits. `integration/RemoteConfig/` stages a separate
Android/WebGL package and supplies integration-only corpus probes. This project is
excluded from the normal mobile SDK package export. No production service is built.
