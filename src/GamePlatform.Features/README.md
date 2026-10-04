# GamePlatform.Features

CL-011 provides a bounded disposable query-cache primitive. It coalesces one
refresh task per exact scoped key, isolates waiter cancellation from shared work,
rejects foreign owner/generation results, and cancels cache-owned work on
disposal. It is an optimization only and never owns durable projections,
receipts, checkpoints or outbox commands. Feature authority and freshness policy
remain with each later feature implementation.

Owns portable feature policy over feature/backend/storage contracts, Core and logging. Excludes SQL, HTTP, Unity and serializers. The sixteen legacy parameterless facades remain fail-closed compatibility stubs; bounded scoped services now exist for Accounts, Profiles, Catalog, Inventory, Wallet, Entitlements, RewardFulfillment and Progression. Their module READMEs define the implemented boundary and remaining composition/acceptance work. Gates: A05–A08.
