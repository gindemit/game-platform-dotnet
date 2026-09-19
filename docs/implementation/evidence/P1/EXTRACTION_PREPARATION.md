# P1 safe extraction preparation

This is a reviewed preparation subpart, not CL-002 or INT-002 completion. Source authority remains MrSquare.Platform. Actual SDK import belongs to P2 and requires the coordinated core artifact and approved interfaces; pure legacy compatibility work does not intrinsically require G1 wire schemas.

Input consumer: `fa393e1caec88d9cd58f4780e682f50e3e715774`, inventory baseline `9c2aa45a5727b5f3a4dc7caba2bad344db530173`, preserving main `47dd7b20b31f56ff8c39f36d5798c3362c54c452`. The consumer owner reviewed CL-001 responsibilities against these callers. No assemblies, callers, scenes, packages or saves were migrated in P1.

## Frozen compatibility constraints for CL-002 review

| Existing responsibility | Required compatibility before extraction |
| --- | --- |
| PlatformId | Preserve ordinal semantic/provider string identity, 128-character bound, blank rejection and default-invalid behavior. `pocket-bloom`, `fixture/0`, `race-4`, `weekly-distance` and store/provider IDs are not UUIDs. Add distinct UUID roles only where ownership requires them. |
| ProviderFailure | Preserve None=0, Offline=1, Unauthorized=2, Conflict=3, NotFound=4, RateLimited=5, Invalid=6, Unavailable=7. Explicitly map/add SDK NotImplemented without renumbering. Reject undefined failures. |
| OperationResult<T> | Freeze guards for mutually exclusive success/error, default error value, null success and bounded diagnostics (legacy 512 characters). Current SDK PlatformResult permits undefined failure values and unbounded diagnostics; this is a known CL-002 gap, not an accepted compatibility match. |
| StateRecord/providers | Preserve expected-revision and ownership restrictions; use distinct feature/domain/wire/SQL representations. Game checkpoint/payload migration is not a database migration engine. |
| GameplayOutcome/completed run | Preserve stable run identity, semantic mode/content/difficulty and checked signed-64 score/duration/metrics with tick rate/validation reference. Game adapter owns mechanics. A signed MGD1 checkpoint is not server-trusted completion. |
| Inventory/Wallet | Preserve any required two-read legacy facade through delegation while separating canonical responsibilities. No facade owns mutable balances or exposes client grants. |
| Host/composition | Do not extract PlatformServices as a universal service bag or treat synchronous legacy cleanup as asynchronous quiescence proof. Keep manual constructors and explicit ownership. |

Use the existing 14 Platform and 27 GameplayDomain source-linked passing cases as baseline fixtures. CL-002 adds exact negative constructor/result/ID and enum compatibility vectors, then a reviewed public API diff. Do not label those future vectors executed merely because these existing cases passed.

## Import and rollback preparation

1. Use the consumer inventory's GUIDs/asmdefs and full source-linked project list; regenerate the scan at import time.
2. Produce one exact managed SDK artifact via CL-013, with hashes, target metadata, references and capability list. No duplicate UPM/NuGet/runtime copy.
3. Change every inventoried live caller and the source-linked Platform test project together, with explicit precompiled references and one canonical type per responsibility. Any temporary facade delegates only and has INT-017 removal evidence.
4. Preserve all original meta GUIDs, MrSquare.Logic dependency in game only, deterministic gameplay, MGD1 decoding/signatures, PlayerPrefs progress/difficulty and installation feedback formats.
5. Before deleting source or declaring import accepted, run the pinned Unity editor and both real scenes' regressions. Missing editor/device evidence stays blocked. Rollback restores the previous coordinated artifact/reference/caller set without deleting saved data or pending operations.

The original user's dirty Unity checkout was not touched. No deployment, Firebase distribution, package publication or main merge was performed.
