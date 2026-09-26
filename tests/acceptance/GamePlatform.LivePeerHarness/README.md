# CL-016 live peer probe

`GamePlatform.LivePeerHarness` exercises the production C# HTTP, MessagePack, private-sync and SQLite providers against a disposable local TypeScript/Supabase service. It fails unless anonymous sign-up, authenticated `/readyz`, provision, complete bootstrap, owner mismatch denial, a committed test-only Garden command, byte-identical operation replay, a distinct operation with the same business source, an exact G3 reward receipt, a complete confirmed pull, `Ready`, and a pull cursor all succeed. A fresh account can correctly bootstrap with no private projection rows, so the fixture seed is checked through the server's actual reward receipt and database inspection, rather than a pre-command row count. It does not use a synthetic access token or report scaffold `501` as a pass.

The input is the `live.json` emitted by backend `tests/hosts/supabase/serve-live.mjs`. That file contains a publishable key, pinned local CA, and host data; keep it in temporary local storage. The CLI never prints tokens or the config. Use a fresh SQLite path. A successful result prints the three exact source revisions and nonsecret scope/count evidence. The local HTTPS authority must be loopback, and its certificate must chain to the supplied CA.

```text
dotnet run --project tests/acceptance/GamePlatform.LivePeerHarness -c Release -- --help
dotnet run --project tests/acceptance/GamePlatform.LivePeerHarness -c Release -- \
  probe --live-file /tmp/disposable-stack/live.json --sqlite /tmp/disposable-stack/peer.sqlite \
  --sdk-revision <40-hex> --backend-revision <40-hex> --unity-revision <40-hex>

# With backend serve-live.mjs --lose-push-response-for-sequence 1:
# add --expect-lost-response true; the CLI requires an uncertain first response,
# a committed receipt, then successful replay of the exact command.

node /path/to/backend/tests/hosts/supabase/serve-live.mjs --inspect /tmp/disposable-stack/live.json > /tmp/peer-inspect.json
python3 tests/acceptance/GamePlatform.LivePeerHarness/verify_inspect.py /tmp/probe-result.json /tmp/peer-inspect.json
# In lost-response mode, add the backend live.json proxyFaultsPath as the third path.
```

The `verify_inspect.py` hook consumes only the backend's read-only local SQL inspection output and proves two distinct operations produced exactly one completion, grant, ledger entry, feed group and coin. With proxy fault data it also proves the dropped and replayed push bodies have the same SHA-256. Keep the underlying probe, inspection and fault files with the three revision pins. The broader CL-016 corpus also uses backend `test:interop -- --case BE-015` for C#↔TypeScript raw vectors and the R09 SDK bootstrap client for page-two interruption and SQLite reopen. This executable covers the real live-service provider path and named command/owner faults; the page interruption and raw vectors are separate peer commands.
