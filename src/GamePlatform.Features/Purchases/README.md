# Purchases

The portable coordinator supports server-verified Google Play consumable lifecycle
reconciliation; it never treats client callbacks as value authority. Apple is
unavailable until its real bridge and verifier exist. The legacy
`PurchasesFeature` facade is not the coordinator.

The native purchase coordinator is a portable, explicitly invoked lifecycle. A
host supplies an actual native bridge and authenticated protected-proof store;
neither fake implementation nor native device support is implied by these
contracts. It reserves proof capacity and persists intent and launch marker
before opening a sheet. The visible price must match fresh provider details.
Known callbacks keep an independent protected token identity and submit against
their exact reserved intent; out-of-band tokens use a separate recovery key.
Call recovery and intent refresh on startup and resume; no constructor starts
background work. Server acceptance never applies value locally: delivery requires
the same installed private-feed visibility and epoch at the authorized checkpoint.
Presentation claims are durable by grant ID. The claim and checkpoint belong to
different stores: a reset racing a claim can suppress a celebration, but cannot
produce local value or make the claim evidence of delivery.

Network and platform failures leave attempts or protected proofs unresolved.
Unsupported products, provider credentials, native bridge/Keystore runtime,
actual Play verification, Android device execution and production configuration
remain separate acceptance gates.
