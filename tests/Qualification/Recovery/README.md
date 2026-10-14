# FIN-RECOVERY joined qualification

These tests require the backend's owned native PostgreSQL HTTPS fixture in
`tests/qualification/recovery/serve.mjs` at the FIN-RECOVERY backend source.
Follow that fixture's README to start its disposable exact-source copy. Serialize
the single fixture; do not run another client or reset while this runner is active.
The backend worker/runner owns its native cluster and shutdown. No hosted token,
deployment, Android process, Unity Editor or retained device database is used.

Overlay the task-owned SDK test and this support directory onto an exact-source
disposable SDK archive, then run from that copy:

```powershell
rtk proxy python tests/Qualification/Recovery/run.py --fixture-config C:/Work/fin-recovery-20261012/fixture.json --results C:/Work/fin-recovery-20261012/evidence/sdk-joined-final
```

The runner requires a real fixture configuration and a fresh results directory,
clears child-only environment variables, and rejects a git-index-bearing checkout.
It requires exactly eight executed/passed parent tests and zero skips/failures in
the TRX receipt. The ordinary SDK suite explicitly skips eight opt-in tests and
the child method; that suite alone provides no joined-server evidence.

The actual production bounded HTTPS executor, typed MessagePack codec and
provision/inspect/recover/receipt/bootstrap providers communicate with production
backend handlers and native PostgreSQL transactions. The test-only auth session
uses the fixture's test verifier. TLS trust pins the exact ephemeral certificate
SHA256 and loopback host/port inside this test's HttpClient handler. No production
transport, TLS validation or auth code changes.

SQLite account directory, journal, atomic command allocator/outbox, canonical
fingerprint and private sync stores are production implementations. Tests create
guard baselines; the healthy-case initial Ready row is deliberately seeded and
proves preservation only. Recovered successor readiness comes from actual complete
bootstrap, with independent reopened allocator, confirmed wallet snapshot, cursor,
checkpoint, entity and journal assertions. The wallet decodes to the genuine
one-unit test.coin grant, then a fresh successor command using the same business
session must preserve one grant and the same durable balance.

Coverage includes committed HTTP response loss, immutable retry after inspection
expiry, watermark advance, owner/app/installation rejection, absent actual receipt
for a valid atomically admitted profile command, unchanged retained intent,
transaction interruptions before adoption/bootstrap install/journal completion,
and account retirement after remote commit. These transaction injections are
distinct from the process-death test: its parent closes every SQLite handle,
launches an owned child test process, waits for native remote commit or durable
Adopted phase, terminates that exact process tree, then resumes the same on-disk
directory/journal and completes real bootstrap. It asserts one successor and
immutable request hashes; a child-only test intentionally never returns normally.

No consumer projector or gameplay admission is manufactured by this fixture.
The portable confirmed projection store is qualified; Unity's actual missing
admitted database detector, Unity lifecycle/readiness, backup/conflict detection,
configured hosted authentication and physical Android remain separate evidence.
No package payload changes, phone upgrade or broader P4 activation are implied.
