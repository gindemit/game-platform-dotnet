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

`ApplyConfirmed` is the narrow borrowed-transaction mutation for a private-feed
adapter. Its caller supplies the durable prior revision observed in the same
projection/cursor transaction; the mutation requires a strictly higher server
revision and validates the exact visible catalog binding before writing. It does
not advance a cursor. `AcceptedAwaitingPull` is a local overlay: when the stored
confirmed revision reaches its receipt revision, every cached read suppresses it
after commit/reopen, including when the confirmation arrived through this
borrowed path. Awaiting and rejected intents remain visible.

`G3WalletProjectionService` is a separate read-only compatibility path for the
frozen G3 wallet projection, whose sole identity is semantic `currencyId`. It
does not synthesize a catalog definition or weaken the catalog-gated spend API.
Only a bootstrap/pull owner can install a strictly newer confirmed record (or an
explicit removal/reset marker) through its borrowed local transaction. Cached
reads are stale or Missing, never a manufactured zero balance; its bounded
versioned local codec is separate from `WalletService` state.
