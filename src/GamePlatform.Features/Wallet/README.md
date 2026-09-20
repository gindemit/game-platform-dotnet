# Wallet

The legacy `WalletFeature` remains unavailable. `WalletService` is the bounded
CL-105 scoped coordinator for one captured backend/app/account/view/generation.
It stores only server-confirmed signed-64 balances and distinctly labeled local
spend intent state. Every public read or installed balance must match the exact
owner's catalog binding by definition ID, semantic key and Currency kind, and
that binding must be Visible. A receipt can become `AcceptedAwaitingPull`, but
it cannot change a balance; only a monotonic confirmed pull can do that. A
retained matching receipt/rejection remains reconcilable after that presentation
binding is revoked; it is not a new spend or a public catalog read.

Spend admission requires a fresh injected backend authorization for the exact
operation, source, currency, revision and amount, after the same binding permits
Spend. A stale/offline durable balance cannot authorize a purchase, and the
service contains no grant, credit or local funds validation. The pending intent
and immutable `wallet.spend` command share the existing atomic outbox
transaction. Production HTTP/wire adapters and Unity composition remain outside
this portable feature scope.
