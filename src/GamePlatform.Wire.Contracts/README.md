# GamePlatform.Wire.Contracts

The P3 same-phase `0.4.0-g3-schema-checkpoint.1` mirror adds generated,
attribute-free carriers for the schema-version-2 reward receipt. Its completion
branch exposes the required `client_trusted_unvalidated` authority label and a
required nullable reward. The generated mapping catalog records exact schema
sources and preserves `long` for receipt signed64 fields. No serializer mapping,
HTTP provider, feature behavior or runtime interoperability is implemented by
this checkpoint. Regenerate/check with `rtk python
src/GamePlatform.Wire.Contracts/generate_g3_dtos.py [--check]`.

Owns attribute-free, immutable wire carriers, distinct from domain and SQL models.
The 64 concrete shapes and 12 union interfaces cover the G1-reviewed core schema
`0.2.0-core-schema.1`, canonical commit
`0109936f0ebf232924cec70adb79c4f790b604bc`. No runtime package or project
dependency is introduced. Milestone M1, CL-003; A01/A02 remain separate runtime
and peer acceptance gates.

`generate_dtos.py` reads the pinned schema mirror and produces `CoreDtos.g.cs`
and `dto-catalog.json`. Run `rtk proxy python
src/GamePlatform.Wire.Contracts/generate_dtos.py` from the repository root.
The catalog records every constructor/property/wire-field mapping, nullable and
optional types, constants, union branches, aliases, and original source pointers
for inline objects. Generated output is checked in; generation is not required
on a Unity/device runtime.

UUID fields are `Guid`; revisions, sequences, economic values and ticks use
`long`, and bounded Unix-millisecond timestamps also use `long`. Consumer strings
are preserved exactly. `WireOptional<T>` distinguishes omission from a present
value, including explicit null where the schema allows it. Lists/maps are copied
and exposed as read-only collections. Extensions have a recursive immutable
null/boolean/string/array/object union, while error details have a separate
null/boolean/string/int32 union. No arbitrary object payload or numeric extension
variant is available. Serializer adapters must reject unknown implementations of
the branch interfaces.

These are wire carriers, not validated domain objects. Constructors require
non-null required references and own container copies; the codec must enforce
all schema constraints before accepting inbound or emitting outbound values:
UUID version/variant, string/token bounds, enum membership, integer bounds,
collection/depth limits, cross-field constraints and operation/payload agreement.
For example, `PushOperation.Type` is explicitly carried and must match `Payload`;
an exhausted stream must have a null next sequence. A constructed DTO does not
authenticate, confirm value, finalize an operation or make bootstrap Ready.

`ProtocolVersion.Current` retains the legacy policy-version constant;
`CoreSchemaVersion` identifies these shapes and `WireVersion` the integer
envelope version. Tests: `rtk dotnet test tests/GamePlatform.Tests -c Release
--filter FullyQualifiedName~CoreDtoTests` (11 tests). DTO tests cover presence,
full-width numbers, original consumer IDs, immutable collections, branch shapes,
and dependency boundaries. They do not prove MessagePack or C#/TS compatibility,
hostile-input schema validation, AOT/device execution or real service behavior.
