# Store

`StoreService` durably queues server-priced `store.offer.purchase` intents for a
captured owner and client stream. A UUIDv7 purchase key retains one immutable
command admission; repeating the key reuses the original operation, while a
different offer for that key conflicts. The service never prices, debits, grants
or confirms value locally. Hosts inject the command codec and atomic outbox store.

The legacy `StoreFeature` facade remains unavailable. The broader eligibility,
pricing, atomic debit, fulfillment, receipt and feed-group acceptance (A06/A07)
remains separate. See `StoreQuestServiceTests` for local source coverage; it does
not establish generated-package, Unity, native or physical-device acceptance.
