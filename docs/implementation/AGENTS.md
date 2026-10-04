# Planning/evidence scope

Root AGENTS.md applies. Read only the selected original task row/card section and its relevant requirements/evidence. Do not load RUNBOOK, all ledgers, frozen interfaces or handoff chronology as a mandatory bundle.

Keep original CL and A01–A13/LC01–LC13/DI01–DI12 identities and acceptance meaning. The local execution-manifest.json contains SDK evidence validation only; the full CL/INT dependency matrix is in game-platform-workspace/plans/sdk-integration. SDK implementation and Unity integration remain distinct. Update full acceptance only from real evidence; do not invent peers, reviewers, provider routes or runtime results.

Backend owns canonical wire contracts, SDK owns reviewed mirror/artifacts, Unity owns game-specific adapters/scenes. Run scripts/validate.py and scripts/test_validation.py when the local evidence ledger changes; full shared manifest validation lives with that manifest in the workspace. Preserve immutable pins, migrations and historical evidence. No publication or weakened tests. Work on `main`; do not switch to Future or former implementation branches.
