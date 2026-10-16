# Navigation asynchronous host context repair — 2026-10-16

Real Pocket Bloom WebGL qualification exposed an animated modal remaining pending
after its admission presentation. Navigation admission and pending joins used
`ConfigureAwait(false)`, allowing continuations to require a managed thread pool.
Unity WebGL has no managed thread pool. The kernel now preserves the caller's
synchronization context at all six admission/join awaits. State locks, cancellation,
typed results, stale-result fencing and portable dependencies remain unchanged.
No Unity API was added to the SDK. The consumer separately replaces unsupported
.NET timers with Unity player-loop timing.

Reviewed default-branch source: `a2d4c956abce186470143a9a18d3c67c20d28069`.
Optional source package delivery: `2f885bd37d98de603cd1370aa796786b89d4ba05`.
Generate/check through `scripts/package-ui-kernel.py`; all 27 canonical runtime
files match the generated package. The existing full mobile SDK is not upgraded.

Controlled synchronization-context tests cover asynchronous admission commit,
matching pending reuse and joining cancellation without cancelling its owner.
Focused navigation tests: 43 passed. Exact reviewed delivery Release regression:
982 passed, 0 failed; 9 separately gated recovery cases were not executed.
`scripts/validate.py` passed. Raw TRX files stay outside the repository under
`C:/Work/scratch/ui-framework-2026-10-16/sdk-tests/`.

The first Debug regression attempt had one crash-harness failure because that test
launches a Release child with `--no-build`. Both the canonical-source and reviewed
delivery Release runs passed. This receipt does not substitute SDK tests for the
rebuilt Unity WebGL/Android runtime checks recorded in the workspace continuation.
Unrelated pre-existing local AccountsLifecycle changes are excluded from delivery.
