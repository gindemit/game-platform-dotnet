# Core Friends SDK source validation — 2026-10-11

The reviewed source implements the selected account-bound Friends service,
request/privacy/blocking commands, exact public-code discovery and bounded team
suggestions, plus friends rankings for the three existing player periods.
The legacy unavailable feature facades and historical full-feature gates remain
unchanged. Gifts and private chat are outside the selected scope.

Validation used the disposable exact-source copy
`C:/Work/temp/friends-sdk-20261011`, never the live repository build output.
`dotnet restore GamePlatform.sln --locked-mode` passed. Final
`dotnet test GamePlatform.sln -c Release --no-restore` passed **843/843**, with zero
failed or skipped tests. Focused Friends/social-marker tests passed **37/37**.
`scripts/validate.py`, the **21/21** validator tests and
`src/GamePlatform.Serialization.MessagePack/generate-schema.py --check` passed.
Whitespace formatting and subsequent `--verify-no-changes` passed for every
changed first-party C# file; generated schema constants retain their generator's
exact output. The formatter reported workspace-loading warnings, with exit zero.

Real SQLite tests prove fresh restart reads make zero network calls, fixed cache
age is preserved on reopen, expiry/clock rollback yields stale data, owner/backend/
app/player/query contexts remain separate, and late responses cannot escape
retirement or shared invalidation. Tests cover first binding from either Friends
or rankings, unbound authorization loss fencing a concurrent ranking response,
lost/cancelled command receipts, identical-body restart retry, transaction rollback,
historical outcomes above query revision floors, and approved retained negative
outcomes releasing pending commands. Unknown/auth/admission errors preserve them.

Production-provider tests cover the three bounded routes, authenticated retry with
identical bytes, zero-score viewer rankings, malformed/foreign/JSON rejection and
real SQLite journal recovery from decoded retained business errors. Combined pull
tests preserve core, Teams and Friends changes. SQLite cursor commits and typed
Friends invalidations roll back together; social upserts, malformed keys and
nonempty marker payloads fail closed.

The six canonical Friends vectors were exchanged with the backend. C# decoded
the exact emitted TypeScript bytes and validated identical closed-schema values,
then round-tripped them through its typed codecs. The backend independently
decoded all six emitted C# byte vectors. Legal MessagePack integer-width choices
differ between encoders; semantic interchange succeeds without requiring one
canonical byte representation. Separate Friends pins are:

- Schema SHA256 `09299f9f0beaf9cb13d3edb92b9b8a48440a99cf8d4d2051232e0b2e5622a606`.
- Fixture SHA256 `3f0c56767395ed4845f513bc62ecd3916922b1da301d2f630b624319680d5ccd`.

Disposable artifacts are `artifacts/friends/sdk-fixtures.json` and
`artifacts/friends/source-validation.json`; the latter records exact source-path
and portable assembly digests for the coordinator. Original core, Teams and global
leaderboard pins are unchanged. The coordinator's independent source review
approved this SDK source before integration.

These are local source checks. Generated-package reproducibility/CI, consumer
import, Unity/AOT/native SQLite, local HTTPS/PostgreSQL journeys, hosted Supabase
Dev and physical Android checks are separate coordinator-owned stages. No package,
device, deployment or Git mutation was performed by this SDK worker.
