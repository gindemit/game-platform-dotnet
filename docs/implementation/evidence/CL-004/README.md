# CL-004 desktop wire candidate checkpoint

Date: 2026-09-20

Source commit: `83caf64596869fc7a1dc35ac6440e21ac97a7d68`

Qualification correction: `2e46b6f90ed60492d77114c655fb9788d4112e25`

`DesktopQualificationWireCodec` is an internal desktop-only candidate over the
existing generated DTO/union mappings and bounded MessagePack reader/writer. It
is visible only to the test assembly, does not implement `IWireCodec`, and
production registration remains unavailable.
It accepts only the reviewed core catalog. There is no typeless resolver,
reflection/runtime-code-generation path, serializer annotation, compression or
JSON fallback.

Desktop tests prove the approved C# producer bytes for
provision request/response, generated interface-union dispatch, named maps, RFC-order
UUID bin16, exact signed-64 extrema, opaque-token bin, absent versus explicit null,
unknown-field and arbitrary-type rejection, duplicate-key rejection, and body/depth
bounds. Provider-fit tests wrap the candidate in an explicit test-local
`IWireCodec` adapter and use a scripted executor. No such adapter or registration
exists in production source.

## Verification

```text
locked restore: 18 projects, exit 0
Release build --no-restore: 18 projects, 0 warnings/errors, exit 0
focused Serialization tests: 171 passed, exit 0
focused CL-005/CL-010/desktop-candidate tests: 46 passed, exit 0
desktop-candidate tests: 7 passed, exit 0
full .NET suite: 335 passed, 0 failed/skipped, exit 0
codec harness self-test: 73 schema, 32 fingerprint, 5 hostile JSON, exit 0
schema and DTO generator --check: exit 0
architecture/dependency validation: exit 0
script tests: 33 passed, exit 0
manifest validation: 52 tasks, 16 feature ledgers, 21 waves, exit 0
manifest tests: 14 passed, exit 0
package pipeline: 14 managed assemblies, symbols/dependency closure/licenses, nothing published, exit 0
git diff --check: exit 0
```

The dependency inventory remains exactly MessagePack 3.1.8 plus the reviewed
lockfile transitive closure. The current early package intentionally still excludes
the codec and its dependencies pending CL-015.

## Remaining limits

CL-004 remains `in_progress`. No Unity import, IL2CPP/AOT/stripping, device run,
complete transitive/native bundle, concrete HTTP executor/host, live backend
transcript or independent review was performed. The earlier G2 bidirectional core
exchange remains the peer evidence; this checkpoint does not rerun or broaden it.
Production `IWireCodec` remains a blocker for CL-005/CL-010 until required AOT
acceptance. No G3 assessment or P4 work occurred.
