# Profiles

Owns app-scoped profile reads/revision edits. Depends on inward contracts only; excludes SQL/HTTP. `ProfilesFeature` is stubbed/fail-closed. Evidence: app/account isolation, stale revision, optimistic rejection and late-result isolation (A07).
