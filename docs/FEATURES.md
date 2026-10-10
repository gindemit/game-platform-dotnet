# Feature catalog

The machine-readable source is `features.json`. Accounts, Profiles, Catalog, Inventory, Wallet, Entitlements, RewardFulfillment and Progression have bounded implementations but remain `unverified` at their full feature acceptance scope. Quests, Achievements, Store, Purchases, Leaderboards, Teams, RemoteConfig and Inbox remain `stubbed`.

Teams now also has an explicitly composed `TeamsService` and authenticated
`ForAccount(...).Teams` provider, tested independently of its legacy unavailable
facade. See [Teams source and package evidence](../src/GamePlatform.Features/Teams/EVIDENCE-2026-10-11.md).
The catalog and its copy in the generated bundle retain the original full-feature
acceptance gate; they do not describe availability of this new scoped API.

Legacy parameterless feature facades stay fail-closed even where a scoped service exists; hosts must explicitly compose the scoped implementation and its real ports. No stub may authenticate, grant, acknowledge, mark Ready or simulate successful transactions. Module READMEs define each implemented boundary, exclusions and remaining evidence.
