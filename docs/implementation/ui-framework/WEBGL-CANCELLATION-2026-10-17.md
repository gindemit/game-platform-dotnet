# WebGL cancellation observation continuation

The actual UIH-07 Player fixture reproduced a cancelled feature-entry join that
remained pending after its shared owner completed. The trace recorded 320 frames
on Unity's UI context during the unchanged ten-second deadline. Tutorial load/save
and the initial admission/join completed. This is target-runtime red evidence;
desktop .NET scheduling does not reproduce the same failure.

The internal `CancellationObservation` helper observes source completion through
a direct caller-context await and forwards readiness into a private inline signal.
Token registration cancels only that private signal. The original operation stays
observed until it settles, including a late fault after cancellation. Callers read
the original source outcome after readiness and retain their cancellation/outcome
precedence. Already completed faults are observed even if a caller selects
cancellation. Registration is disposed after the observed race settles.

The five private cancellation races in the kernel, feature-entry deduplicator and
tutorial flow use the helper. Published pending/result promises retain
`RunContinuationsAsynchronously`; the legacy navigation branch is unchanged.
Cancellation and forwarding happen outside owning state locks, allowing same-context
reentry. No worker, default-scheduler continuation, timer, Unity dependency or new
public contract is introduced. Other same-primitive paths are reviewed preventive
scope, not additional claimed observed incidents.

Four added public-behavior tests cover cancellable-source success, cancellation
reentry with shared work preserved, tutorial store serialization during reentry
and kernel cancellation reentry before a late provider fault. All 57 focused
flow/kernel tests passed before and after the repair in an external test harness
linking the exact owning test files and canonical kernel project. This is desktop
semantic/context evidence, not the WebGL red regression or Player acceptance.
The expanded affected navigation/facade/presenter regression then passed all
80 linked source tests, with zero failures/skips, against the repaired canonical
projects. Its external harness does not reference MessagePack or change audit
policy; the ordinary full SDK restore failure remains separately recorded.

The normal full SDK test command currently stops at restore: the existing
MessagePack 3.1.8 dependency triggers NU1902 for
[GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h).
No audit setting, warning rule, dependency or full mobile SDK pin was changed by
this repair. Focused dependency-free navigation tests do not imply full SDK success.

Canonical source precedes deterministic optional-package generation and independent
source/payload review. Unity must import that exact delivery and execute the
extended delayed fixture, including source-wins cancellable joins, scope retirement,
noncooperative admission and kernel cancellation/join paths. Until strict Player
collection succeeds, UIH-07 remains open. Performance, graphics and physical-device
acceptance remain separate requirements.
