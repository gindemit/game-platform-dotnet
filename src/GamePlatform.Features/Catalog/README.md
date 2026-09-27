# Catalog

Owns versioned definitions, app bindings and permissions. Inward contracts only;
visibility never implies grant/spend.

CL-103 adds a dependency-ready, provider-agnostic `CatalogService`: versioned
snapshots have an exact backend/app/account/view/generation/version/locale/audience
identity; canonical definitions and app presentation bindings are distinct;
unsupported, incomplete, duplicate or corrupt data fails closed. The service can
show only a prior validated durable snapshot as stale while offline. It does not
add a wire route, HTTP provider, SQLite migration/adapter, catalog authoring,
grant/spend behavior or Unity composition. `CatalogFeature` remains the legacy
unavailable compatibility seam until production composition exists. Evidence:
binding visibility and authorized-view reset (A07).
