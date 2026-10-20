# UIH-01–02 flow execution and admission

Current local contract under the workspace UI framework hardening order. This
extends the [kernel host-context repair](WEBGL-ASYNC-2026-10-16.md); it does not
qualify browser tutorial/feature-entry adapters or update the complete mobile SDK.

All flow awaits preserve the calling synchronization context: tutorial semaphore
waits, checkpoint load/save, cancellation observation, feature admission, pending
joins, caller cancellation and late-fault observers. Cancellation registrations
only signal completion; external callbacks and result completion run outside locks.
Hosts must continue servicing their context while detached operations unwind.
No Unity reference, Task.Run, timer or other SDK-wide await change is introduced.

Tutorial cancellation/retirement settles its observer before a store that ignores
cancellation finishes. The underlying operation keeps its serialization gate
until storage settles; a hung store blocks later store operations. Late failures
are caught by a supervised observer. Canceled saves never publish completion,
even after acknowledgement. Storage may already have persisted a canceled write;
the provider still owns revision/identity authority and durable reconciliation.
Disposal/reentrant retirement returns Retired and cannot publish a late load.

Feature admission reserves before outside-lock current lookup, then rechecks
scope cancellation. Same-key callers join that reservation; caller cancellation
does not cancel shared admission. A canceled old reservation cannot remove a new
one. Keys must include scope/generation and providers must fence late publication,
publishing the current handle before admission completes. Scope cancellation is
checked before returning any handle. These flows own no second history engine.

Prompt revalidation remains outside the lock. Commit atomically checks global
claim ownership. Expiry removes only unclaimed prompts: a presented expired claim
retains exclusivity until explicit Release/Resolve. Stale claim tokens stay invalid.

Regression-first failures and final exact package/test identities are recorded in
the workspace `docs/work/ui-framework/HARDENING-2026-10-16-RESULTS.md` receipt.
`FlowHardeningTests` exercises incomplete tasks on a pumped single-thread context,
reentrant/concurrent admission, cancellation, late faults and write serialization.
The tests exercise provider faults; they do not claim their FIFO sentinel proves
all private asynchronously posted observers drained. Source review checks the
catch/await paths. Actual delayed WebGL execution remains pending UIH-07.
