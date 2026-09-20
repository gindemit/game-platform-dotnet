# CL-011 scoped snapshots, durable state and disposable query caches

Source commit: `978f87435b8d2c22f594af05fd9d857467fb9825`.
Milestone M3/C; acceptance areas A07/A08; status `in_progress`.

The dependency-ready foundation now provides captured-owner immutable feature
snapshots with explicit missing, available, stale, pending, unavailable and error
states. Canonical private query keys include backend, app, account, authorized
view and local generation through `ScopedOwnerContext`, plus a bounded
feature-owned query identity. No catalog DTO, HTTP route, remote provider,
reactive framework, Unity type, reward behavior or gameplay composition was
added.

The disposable query cache has explicit entry and lifetime bounds, coalesces one
refresh task per exact key, isolates a cancelled waiter from shared work, rejects
foreign owner/generation publication, fences late results after invalidation and
cancels cache-owned work on disposal. It has no storage dependency and cannot
erase durable projections, receipts, checkpoints or outbox commands.

SQLite registry version 3 adds only `gp_feature_state`. The adapter uses an
internal typed SQL row distinct from storage values, feature read models and wire
DTOs. Writes borrow the caller-owned real SQLite transaction, reject owner/view
and revision conflicts, defensively copy payloads, cap payload/extension bytes,
and preserve existing opaque extension bytes when a known-field update does not
replace them. Feature-specific codecs remain responsible for validating an
extension's approved shape/depth before it reaches this storage boundary.

## Commands and results

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl011 --no-restore
exit 0; 9 passed, 0 failed/skipped/warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter "FullyQualifiedName~Cl007SqliteExecutorTests|FullyQualifiedName~Cl008AtomicOutboxTests|FullyQualifiedName~Cl010MigrationRegistryTests|FullyQualifiedName~Cl011" --no-restore
exit 0; 30 passed, 0 failed/skipped/warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 368 passed, 0 failed/skipped/warnings

rtk python scripts/validate.py
exit 0

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0; 51 passed

rtk python docs/implementation/validate_manifest.py
exit 0; 52 tasks, 16 feature ledgers, 21 waves

rtk python -m unittest discover -s docs/implementation -p "test_*.py"
exit 0; 14 passed
```

Native cases used the pinned Windows x64 SQLite library and prove commit,
rollback, reopen, view isolation, extension preservation, revision regression
rejection and foreign-owner rejection. The cache cases prove app/account/view/
generation/query separation, coalescing, independent waiter cancellation, late
result fencing and disposal.

## Limits

This is not a feature implementation and supplies no server authority or
freshness policy. Public/shared catalog cache keys, feature-specific extension
schemas/codecs, multi-database shared-row composition, physical process-kill,
disk-full/corruption, macOS, Unity/IL2CPP/AOT/device and independent review remain
unrun. Durable receipts/outbox/private sync retain their existing owners and are
not TTL data. A07/A08 therefore remain unpassed.
