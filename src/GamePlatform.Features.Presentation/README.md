# GamePlatform.Features.Presentation

Provides portable CL-012 presenter ownership primitives over feature
contracts/navigation/Core. Presenters capture an owner generation, dispose their
subscriptions and view leases across repeated bind/unbind, reject stale callbacks,
and never dispose borrowed account services. Pending, confirmed and save-failed
results remain explicit. Excludes Unity views, SQL and HTTP. Gates: A07, A10.
