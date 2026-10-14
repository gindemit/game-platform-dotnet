# Core codec peer harness (historical CL-016/P2 utility)

This .NET 9 executable exercises the qualification codec against the G1-approved
`0.2.0-core-schema.1` mirror. It is not a live service or Unity runtime and does
not by itself prove production codec registration. The explicit unavailable
adapters remain for callers that select an unsupported capability; the qualified
production MessagePack adapter is covered separately. No network calls,
credentials or provider fakes are used.

From the SDK root:

```powershell
rtk proxy dotnet run --project tests/acceptance/GamePlatform.CodecHarness -c Release -- self-test --root .
rtk proxy dotnet run --project tests/acceptance/GamePlatform.CodecHarness -c Release -- produce --root . --output artifacts/peer/csharp.json --sdk-commit <40-char-sha> --backend-commit <40-char-sha> --unity-commit <40-char-sha>
rtk proxy dotnet run --project tests/acceptance/GamePlatform.CodecHarness -c Release -- consume --root . --input <typescript.json> --sdk-commit <same-sdk-sha> --backend-commit <same-backend-sha> --unity-commit <same-unity-sha>
```

Use immutable actual implementation commits for all three arguments. `produce`
refuses an existing output file. `self-test` runs the canonical diagnostic
positive/negative corpus, fingerprint known answers and hostile JSON lexical
cases; its roundtrips are not independent peer evidence. The corpus mapping is
copied from the exact approved backend manifest identified in `core-corpus.json`;
every fixture is checked against that manifest's SHA-256 before use.

Exchange format: JSON object with `formatVersion: 1`,
`contractVersion: "0.2.0-core-schema.1"`, `producer: "csharp" | "typescript"`,
`commits: {sdk, backend, unity}`, `cases: [{id, schema, hex}]`, and
`fingerprints: [{id, sha256}]`. Schema is the filename plus fragment, for example
`push.schema.json#/$defs/request`. Case IDs are the valid entries in
`core-corpus.json`. Fingerprint IDs are all valid vector names from
`contracts/v1/fixtures/semantic/fingerprint-vectors.json`. Bytes are hex
MessagePack produced by the named runtime; hashes are lowercase SHA-256.

The consumer requires the complete exact corpus, rejects duplicate/missing IDs,
schema/version/revision mismatches, and compares decoded values to local approved
fixtures rather than peer-supplied expected values. Its input must identify the
TypeScript producer; C# self-consumption fails. The synchronization reviewer must
verify the actual producer command and immutable code, not trust a label alone.
Backend tooling may translate its output envelope to this shape without changing
the independently produced MessagePack bytes. Record that command and revision.

At the time this utility was introduced, G2 still needed both actual
producer/consumer directions, independently generated hostile binary cases and
compact integer variants, plus live-slice schema review. Those later results are
retained in dated evidence and summarized in `docs/IMPLEMENTATION_STATUS.md`.
For individual reproduction probes, `encode` accepts diagnostic JSON and writes raw
MessagePack; `decode` accepts raw bytes and writes diagnostic JSON:

```powershell
rtk proxy dotnet run --project tests/acceptance/GamePlatform.CodecHarness -c Release -- decode --root . --schema 'common.schema.json#/$defs/signedInt64' --input artifacts/peer/ts-int.bin --output artifacts/peer/decoded-int.json
```

Use the same options with `encode` for C# primitive production. Exit 1 means
conversion failed, creates no output, and is not by itself a passing negative
test. The independent gate runner must assert the intended rejection and record
the exact producer, consumer, fixture revision and command. These modes report
only a conversion result and never an interoperability verdict.

This command alone never approves G2. The separate
[`GamePlatform.LivePeerHarness`](GamePlatform.LivePeerHarness/README.md) exercises
the bounded production HTTP/MessagePack/SQLite journey against a disposable local
service. Unknown codec-harness modes fail with exit 1; no health response
substitutes for a real service journey. Logs print counts and stable failure types only.
