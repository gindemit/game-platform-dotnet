# FIN-AUTH local SDK validation — 2026-10-12

Base source: `976b4519330c1cc1bebb1bc0ad08696139e30bee`. Selected work order:
workspace `plans/FIN-AUTH-DEV-2026-10-12.md` at `5106cd1`.

The desktop executor's combined response/content header bound increases from 16
to 64. It retains duplicate detection, 128-character name and 16,384-character
value bounds, request admission, redirects, bounded body reading, cancellation
and delivery certainty. Lifecycle/CAS/recovery/retirement source is unchanged.

Seven additional cases exercise the actual bounded executor: 20-header signup,
in-session rotation and same-subject provider reconstruction; accepted counts 20
and 64; rejected count 65; duplicate names across response/content collections;
oversized header names and values. Fixtures contain synthetic header names and
synthetic credentials. Existing malformed, body overflow, uncertainty, CAS,
cancellation and retirement tests run in the focused and full suites.

## Snapshot and source identity

All execution used `C:/Work/fin-auth-20261012/sdk-worker`, outside repositories.
The initial Windows file copy caused the whitespace gate to fail with ENDOFLINE
errors in unrelated baseline files and the task files. Only the two task-owned
authoritative C# files were normalized to LF. The final disposable snapshot was
reconstructed from `git archive --format=zip` of the base (831 unchanged files)
plus the two task-owned LF changes. No unrelated authoritative files were edited.

Runtime executor SHA256:
`9b9a231d23cfb59f97b8c0b1ab2604373ead23b5ba833b71a8eacb6aac95380c`.
Test file SHA256:
`83aa436e31edb428237a4d17e2002032f6e7dcc3d06fa4d3c4793eb76541081c`.
SHA256 of concatenated sorted `git diff -- <path>` output for these two paths:
`c015fb8fb709014ddc65fc1c703f65f59a0ec46ebe47c71d6847fcd43863f318`.

## Commands and results

Each shell command was prefixed with `rtk proxy`.

| Command | Result |
| --- | --- |
| `dotnet test GamePlatform.sln -c Release --filter FullyQualifiedName~SupabaseAnonymousAuthLifecycleTests` | 55 passed, zero failures/skips |
| `python3 scripts/validate.py` | Passed; 15 projects, 16 evidence-aware feature states |
| `python3 -m unittest discover -s scripts -p test_*.py` | 69 passed |
| `dotnet restore GamePlatform.sln --locked-mode` | Passed |
| `dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs` | Initial Windows byte-copy failed ENDOFLINE; final archive snapshot passed, with workspace loading warning |
| `dotnet build GamePlatform.sln -c Release --no-restore` | Passed, zero warnings/errors; also passed after archive reconstruction |
| `dotnet test GamePlatform.sln -c Release --no-build --no-restore` | 883 passed, zero failures/skips |

The full suite passed both before and after archive reconstruction (883/883,
zero skips each). The reconstruction restored unchanged committed files/configuration
and its whitespace/build/structure gates passed. No source logic changed during
reconstruction.

## Evidence limits and remaining checks

The local lifecycle test uses an in-memory `ISupabaseAnonymousSessionStore` fake.
It proves rotated-secret CAS commitment and same-subject provider reconstruction,
not durable persistence or operating-system process restart. The portable SDK
defines this host-owned credential-store interface; it supplies no production
secure-store implementation to qualify locally. Hosted/host storage verification
belongs to the coordinator's separately owned external harness.

This worker did not generate packages, contact hosted Auth, inspect credentials,
change the Unity pin, run IL2CPP/device/browser checks or mutate Git. Deterministic
package review and hosted DEV evidence remain separate integration steps. Preserve
original P3/G3 acceptance and broad P4/ordinary-shell/public gates.

## Coordinator-hosted DEV verification

After fresh nonauthor source review, the coordinator compiled an external .NET 9
harness against the exact archive snapshot DLLs and used the actual executor and
auth lifecycle against the existing Supabase DEV project. One fresh anonymous
test identity was created, with no player provision, game, reward or social writes.
The older Teams fixture supplied endpoint/configuration only; its retained
credentials were not read or rotated.

Fresh signup and explicit recovery both returned HTTP 200 with 20 combined
headers. A separate OS process reopened the host-owned Windows DPAPI-encrypted
credential store and authenticated the same subject: HTTP 200 with 21 headers.
Each returned refresh secret matched the durably reopened store, the secret
changed, and the credential version advanced. Unauthenticated gateway health
returned 401; authenticated health and readiness returned 200 in both processes.
Default HTTPS validation, redirects disabled, encrypted atomic flush/write and
exclusive process ownership were retained.

External evidence: `C:/Work/fin-auth-20261012/hosted/owned-state/` contains encrypted
state and sanitized `fresh-summary.json` / `restart-summary.json`. Harness source
SHA256 is `6350a15db65497e968cdfef24110a4c6d1f40c84e6413358fafabc8c1eb2d70d`;
project SHA256 is `bfd6edbb77666eeb27f4d0704b2658e6bad5ad50debc8991b2b33a4839eaa296`.
No credentials, private subject IDs or response bodies are recorded here.

This proves chosen desktop host-store/process restart and actual hosted Auth,
not Android Keystore, power-loss durability, complete platform bootstrap or
recovery back into game. Backend current readiness still has the separate
FIN-BACKEND social-schema coverage gap; HTTP 200 does not close it. Generated
package and consumer evidence remain distinct.

## Reviewed deterministic Git UPM delivery

Reviewed source is pushed as `13c8ed92ecbf935795088555a2ea03ab2852d20b`.
Manual [source CI 38262786200](https://github.com/gindemit/game-platform-dotnet/actions/runs/38262786200)
passed validation/build/test/package; the tagged release job was skipped as intended.
Source commit used `[skip ci]` followed by manual workflow dispatch to preserve
the existing tagged-publication hold.

A clean exact-source disposable clone at
`C:/Work/fin-auth-20261012/sdk-package` generated the full deterministic bundle
with the existing packaging scripts, without dirty/no-build overrides. Bundle
verification passed 23 managed assemblies and three qualified native inputs.
Git UPM `0.1.0-dev.145` export and independent complete inventory/hash/native
verification passed. Source commit is exactly `13c8ed92`; the package changes
33 generated files (15 DLLs, 15 PDBs and three JSON manifests/version files).
Native assets, import metadata and AOT/link policy bytes are unchanged.

Bundle dependency-manifest SHA256:
`d60c46cfa6b9b96f3c7fcc12cfbbb652eedb3150cbceb969bd634bbd1d555b98`.
UPM content-manifest SHA256:
`3e37b1798bdbe8e77dee56304c479c653f35cb6fee65bb3d4821e744806eb62e`.
Packaged transport DLL SHA256:
`3ba09580127790b79c27ecf3895b6489a62bedb1ddd41677d222a87adc254765`.

Root rebuilt the external harness against the generated package and verified the
loaded transport DLL matches those exact bytes. An additional separate-process
DEV restart refresh passed with 21 headers, same subject, response-matching
rotated secret, encrypted durable state and health/readiness 200. The original
source-run restart summary remains `source-restart-summary.json`; the package-run
summary is `restart-summary.json`, alongside the original fresh summary.

Fresh nonauthor source/hosted-receipt/package reviews passed. A second complete
independent rebuild for byte reproducibility was not run. Git delivery is not a
registry/tag publication or Unity qualification: the game remains pinned to its
previous dev.144 delivery, with consumer repin and physical acceptance deferred.
