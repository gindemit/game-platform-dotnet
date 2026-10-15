# Portable navigation kernel

Dependency-free `netstandard2.1` / C# 9 logical navigation. No Unity, networking,
storage, account or economy services are referenced. The Unity host owns visual
leases and lifecycle work; `NavigationCoordinator` is the legacy facade over this
same engine.

`SelectRootAsync` begins a new flow. `PushAsync` preserves callers; `ReplaceAsync`
retires the current entry and its children. `OpenModalAsync` records its caller and
stacks modal owners. `Back` consumes exactly one modal, pending admission or history
entry. Capacity counts committed screens and modals, including the root, and rejects
without evicting. The default is 16. Explicit root selection clears nested history.

An admission receives a provisional entry and a request token. It prepares the
shell, including its entrance, without waiting for remote feature data. The entry
lifetime token starts before admission and is cancelled on failure or retirement.
The host must release provisional resources when that lifetime ends, including late
provider results. The kernel fences logical publication even when a provider ignores
cancellation; it cannot dispose resources that the provider keeps privately.

Latest admissions supersede the same screen target or modal owner slot. Independent
slots coexist. ReuseMatching uses an explicit stable match key; SingletonPerScope
uses the definition key. Both join a matching pending shell and focus a matching
committed entry with its original result owner. A cancelled joining caller does not
cancel the admitted owner. Request cancellation applies until commit; later caller
cancellation does not close the committed entry. Close by its entry handle instead.

Snapshots copy lists and state envelopes. Arguments and restoration payloads are
opaque **immutable caller-supplied values**: no serializer, reflection cloning or
runtime mutation detection is provided. Replace restoration state with
`SaveRestorationState`, never mutate an already saved object. Identity/token handles
inside historical snapshots remain handles: their lifetime token can subsequently
become cancelled. Snapshot notifications execute outside the lock and may be
reentrant/concurrent; hosts marshal them to their UI thread and discard versions
older than their last applied version. Observer exceptions cannot stop cleanup.

Child result tasks settle on every close/failure/retirement. Only `Complete<T>` with
the registered result type and a live original owner can supply a value; retirement
suppresses later completion. A caller should still check its own lifetime before
applying an awaited result, as it can retire after completion was accepted. Root
entries have no result owner and cannot complete a child result.

The internal legacy policy preserves old bounded-history eviction, one replacement
modal, modal survival across ordinary route navigation, and loader exception/cancel
behavior. It is available only to the existing `GamePlatform.Navigation` assembly;
new consumers use the public rejecting/stacking policy.
