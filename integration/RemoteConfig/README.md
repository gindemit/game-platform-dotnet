# Isolated configuration harness and staging

`ConfigurationProbe.cs` is integration source, excluded from the shipping DLL.
Copy it into the isolated Unity harness with the runtime assembly. Baseline defines
`RC_TARGET_BASELINE`; typed variant defines `RC_TARGET_TYPED`. Typed smoke calls
the compiler/evaluator without invoking JSON reader; full path exposes `RunSmoke()`
and `RunCorpus(byte[])`. Shared corpus returns `passed:73` on success; any mismatch
throws a bounded `InvalidConfigurationException` code. This includes canonical,
strict-version, allocation, invalid JSON and 22 original guarded research cases.

Stage an exact source-built assembly after inspection with the existing standalone
`scripts/package-inspector` (build it in a disposable copy). `stage.py --assembly
PATH/GamePlatform.Configuration.dll --metadata PATH/metadata.json --output EMPTY`
requires metadata shaped `{assemblySha256,assemblies}`, binding SHA256 of the DLL
to the one-element PackageInspector result array. It accepts exactly this
netstandard2.1 assembly and its sole netstandard reference.
Output is a separate local package with explicit Editor/Android/WebGL import
policy and SHA256/byte inventory. No game paths, installs, regular UPM exports,
source duplication, runtime third-party packages or publication are involved.

Typed compilation also validates aggregate canonical size and computes a semantic
digest. Its canonical writer/cryptography cost therefore belongs to the typed
variant. The reader is linked only through the full variant. Unity/native/browser
measurements are owned by the workspace RC-TARGETS receipt, never inferred here.
