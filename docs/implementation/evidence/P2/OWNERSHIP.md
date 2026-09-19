# P2 epoch 2 ownership and release observation

Backend execution commit `19aef14eaa6ca428775ec7ff7a4f69cfeab0e20a` releases
P2/epoch 2/parallel with G1 passed and G2 pending. Canonical schema commit
`0109936f0ebf232924cec70adb79c4f790b604bc` and SDK mirror/metadata
`f61931b95fc176f8f4aa9ed81aea376b76bcd038` /
`a232e1c85598c12701981e22666dbf35ae9f7cb2` are immutable inputs.

The coordinator owns shared projects, package pins, evidence, manifests and
integration. Backend files and central release state are read-only during P2.
All changes remain on execution/task branches under CODEX_CLIENT.md.

- `/root/dto`: CL-003, Wire.Contracts and dedicated DTO tests, isolated CL-003 tree.
- `/root/consumer_audit`: independent G1/readiness audit, then CL-002 Core and
  compatibility paths in an isolated CL-002 tree. INT-001 inventory is sufficient
  for this extraction subpart; its whole Unity acceptance remains partial.
- `/root/codec_design`: codec design, then CL-004 adapter/serialization tests in
  an isolated CL-004 tree. Integration waits for reviewed CL-002/003 delivery.
- `/root`: CL-016 CLI, package review and sequential integration/review.

The core and wire contract locks are split along their existing disjoint owned
paths for P2. All public API integration is serialized by the coordinator. No
worker edits shared project files or another worker's mutable worktree.

Milestone M1/A-D: A01 primitive correctness and A02 interoperability are relevant;
A09/A10/A13 native, assembly and artifact acceptance remain separately measured.
No A01-A13 whole-production gate passes from these desktop deliveries alone.
