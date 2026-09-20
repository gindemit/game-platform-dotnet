# CL-103 — Catalog portable service evidence

Task state: dependency-ready SDK implementation. Milestone M3/C-D; acceptance
areas A07/A08 remain unpassed.

## Implemented boundary

`GamePlatform.Features.Contracts.Catalog` defines immutable versioned snapshots
of stable definition IDs, semantic keys, server-owned definition versions and
per-app bindings. Definition payload and app presentation payload are copied and
bounded independently. A visible binding is not use, grant, spend or submit
permission.

`CatalogService` has only injected feature ports for download and durable
snapshot storage. Its disposable refresh cache is keyed by captured
backend/app/account/view/generation plus requested version, locale and audience.
It validates that a provider result belongs to that exact identity before
installing it. Unsupported schemas produce an explicit unavailable snapshot;
missing, incomplete, duplicate, mis-scoped or corrupt snapshots are error state
and are never installed. A previously validated durable snapshot is returned as
stale for offline use; it is never reinterpreted as a newer catalog version.

## Tests run

Windows; .NET SDK from the repository `global.json`.

| Command | Exit | Result |
| --- | --- | --- |
| `rtk dotnet test GamePlatform.sln -c Release --filter 'FullyQualifiedName~Cl103CatalogServiceTests'` | 0 | 5/5 focused tests passed, zero warnings |
| `rtk dotnet test GamePlatform.sln -c Release` | 0 | 375/375 SDK tests passed, zero warnings |
| `rtk python scripts/validate.py` | 0 | architecture, package and contract-pin validation passed |
| `rtk python -m unittest discover -s scripts -p 'test_*.py'` | 0 | 51/51 script tests passed |
| `rtk dotnet build GamePlatform.sln -c Release --no-restore` | 0 | 18 projects, zero errors/warnings |

Focused cases cover a common definition bound to A/B with independent app
metadata, an A-only definition, versioned revocation with offline prior data,
owner/version/locale/audience cache isolation, unsupported schemas,
incomplete/duplicate/mis-scoped/corrupt snapshots, and payload bounds.

## Limits

The backend's BE-017 catalog source deliberately exposes no public browse or
import protocol at this checkpoint. Accordingly, this task adds no wire DTO,
HTTP route/provider, SQLite migration or adapter, host composition, catalog
authoring, value mutation, Unity integration or authority to grant/spend. The
test store/provider are test-only fakes. A real provider and a transaction-owned
durable storage adapter need their separately owned protocol/composition and
migration work before A07/A08 or G3 can be claimed.
