# Roadmap

- **M0 compatibility/tooling:** scaffold, inventory, locked restore, validators, build/tests/package; no Unity migration.
- **M1 protocol:** backend-owned canonical contract, bounded MessagePack, RFC UUID/int64/timestamp behavior, semantic fingerprints and real bidirectional bytes.
- **M2 durability/provisioning:** selected native SQLite adapter, migrations, serialized executor, complete online bootstrap, crash/reopen/clone tests and Unity/IL2CPP validation.
- **M3 progression/sync:** one generic outcome through durable ordered push/pull/reset handling.
- **M4 value/objectives:** transactional wallet/inventory/entitlements/rewards plus one quest and store slice.
- **M5 breadth/package:** remaining features, consumer/native package proof and second synthetic game.

## Executable decomposition and early consumer gates

Use [implementation/README.md](implementation/README.md) and the [task/ownership DAG](implementation/execution-manifest.json). M0–M5 above retain their meanings; task cards map back to these milestones and the existing MrSquare A–E phases. Current execution routing lives in [AGENT_ORCHESTRATOR.md](../AGENT_ORCHESTRATOR.md) and the selected backend campaign packet.

Do not postpone all packaging/adoption until M5. CL-013/INT-002 prove an early canonical managed SDK import; CL-015/INT-009 qualify the real native/AOT bundle. INT-006/007/008 wire actual account/bootstrap, durable level completion and truthful UI. **INT-010 is the early M3–minimal-M4 live-backend/device gate:** provision fully online, play one real level offline, commit progress/outbox atomically, restart, reconnect with the same command identity, receive one authoritative reward and display confirmed state after ordered pull. This does not wait for every social/store/objective feature.

M5 still includes broad feature acceptance, independent second-game reuse and stable packaging/bridge removal. A scaffold repository or fake-backed sample does not prove reuse. Task completion and SDK implementation never automatically set MrSquare integration to complete. All sixteen dual statuses remain explicit in the manifest.
