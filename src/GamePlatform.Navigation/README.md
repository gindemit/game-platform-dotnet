# GamePlatform.Navigation

Implements the pure CL-012 minimal Home/play/result/Next/Profile flow with typed
routes and caller return context, bounded history, replace semantics, single-owner
modals, bounded deferred popups, unavailable-route results and stale-load
cancellation. `DestinationRoute`/`DetailRoute` let a game configure its own
destination roots and caller-returning detail screens; `ReturnHome` returns to the
coordinator's configured root and `Back` cancels a pending delayed load before it
pops history. Depends only on Core; excludes Unity views, tabs and a global
WindowManager. Gate: A10.
