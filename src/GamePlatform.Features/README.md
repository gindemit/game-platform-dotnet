# GamePlatform.Features

CL-011 provides a bounded disposable query-cache primitive. It coalesces one
refresh task per exact scoped key, isolates waiter cancellation from shared work,
rejects foreign owner/generation results, and cancels cache-owned work on
disposal. It is an optimization only and never owns durable projections,
receipts, checkpoints or outbox commands. Feature authority and freshness policy
remain with each later feature implementation.

Owns portable feature policy over feature/backend/storage contracts, Core and logging. Excludes SQL, HTTP, Unity and serializers. All sixteen M0 classes are fail-closed stubs. Gates: A05–A08.
