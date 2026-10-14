# Compact simple SDK auto-properties — 2026-10-14

The owner requested one-line auto-properties after seeing an expanded getter in
Visual Studio. SDK base: `f913f38179ee76e5da7989f73f1dcbb2456ef4f9` on `main`.

Owned paths in `gindemit/game-platform-dotnet`: `.editorconfig`, clean tracked
first-party C# files containing simple auto-properties, `docs/FORMATTING.md` and
this work-order/evidence directory. Use a disposable archive and a syntax-aware
one-time transformation. The root is the sole git integrator. Preserve concurrent
work; do not modify vendor/generated contracts, packages, APIs or executable
statement/accessor bodies. Commented or attributed accessor lists remain intact.

Enable single-line block preservation so Visual Studio and the existing formatter
retain `public string Reason { get; }`, including ordinary `get; set;` and access
modifiers. Retain Allman and expanded-statement settings. This formatter switch
also preserves other existing compact blocks; document that limitation. It does
not collapse already expanded constructors or control flow. Do not introduce a
custom runtime dependency, analyzer or permanent formatting tool.

Verify before/after C# tokens, directives and comments, and that only simple
auto-property accessor spans change. Verify excluded file bytes, normal CI
formatting and idempotence, the Release solution build, affected SDK tests and
the local documentation validator. Exercise compact get/set/private-set fixtures
and a multiline identity-method statement canary in the disposable copy.
Use a fresh nonauthor child with a read-only lease for review before integration
and push. No competing git indexes, package writers, ports, databases, devices,
Unity processes, deployment or consumer adoption. Source maintenance retains all
existing runtime/publication gates; an ordinary authorized main push has its
existing CI delivery behavior.
