# GamePlatform.Serialization.MessagePack

The explicit `QualificationMessagePackCodec` implements desktop qualification of
the G1-reviewed `0.2.0-core-schema.1` protocol with MessagePack-CSharp 3.1.8
low-level reader/writer APIs. It uses named maps, RFC-order UUID bin16, exact
signed integers, strict UTF-8, schema-directed absent/null handling, full schema
constraints and canonical semantic SHA-256 fingerprints. No reflection,
typeless metadata or serializer attributes enter the wire/domain types.

`Encode(schemaRef, diagnosticValue)` and `Decode(schemaRef, bytes)` accept an
explicit reviewed local schema reference, such as
`push.schema.json#/$defs/request`. `QualificationValue` is a qualification-only
immutable semantic tree. `CanonicalBytes` and `Fingerprint` accept the approved
fingerprint input envelope, including authenticated scope. They do not infer
authentication or permit a request to choose its actor.

Bounds include 262144-byte bodies and fixed-capacity output buffering, strict
local collection/string/binary limits, 32 container levels, 16384 decoded nodes,
and 2097152 logical allocation units. Decoding reserves the raw tree plus output
and intermediate normalization copies before normalization, including conservative
integer-to-decimal and UUID-to-text expansion. Encoding reserves three diagnostic
tree equivalents; fingerprinting reserves six. This conservative retained-copy
accounting can reject large trees before a single-tree budget would. Diagnostic
validation itself does not copy decoded containers. Parser-created containers
take internal ownership without another full copy.

Extension size is measured as compact diagnostic UTF-8 JSON with minimal escapes,
limited to 16384 bytes; its maximum 16-container depth includes its wrapper.
Atomic feed groups are capped at 65536 bytes in diagnostic and MessagePack
representations, including noncompact incoming MessagePack tokens. No production
HTTP compression policy is implemented by this standalone codec.

`generate-schema.py --check` verifies pinned schema hashes and deterministic
generated constants. Tests cover all 73 reviewed schema cases, 32 fingerprint
vectors, compact/full-width integer tokens, RFC UUID bytes, hostile UTF-8,
duplicate keys, reference siblings, actual depth/node/allocation limits,
extension boundaries and aggregate feed groups.

`UnavailableMessagePackCodec` remains the production-facing unavailable seam.
Desktop qualification is not A01/A02/A09 approval, Unity integration, IL2CPP/AOT
or physical-device evidence. The cross-repository executable harness and actual
peer evidence are tracked separately; this adapter never authenticates, grants,
acknowledges synchronization or advances checkpoints.
