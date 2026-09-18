# Executable feature cards — all sixteen capabilities

Baseline SDK: `f446101574e11c9885583ea184af583303b74ba1`. Every existing module is an unavailable scaffold; none becomes implemented by writing these cards. Exact ownership/dependencies/environments and separate SDK/consumer statuses are in [execution-manifest.json](execution-manifest.json).

## Common feature-card contract

Each card is owned by **gindemit/game-platform-dotnet**, an SDKWorker, and the named module subdirectories in the manifest. Existing `src/GamePlatform.Features/<Feature>/` contains its stub and README. Feature-specific contract, remote-port, SQLite implementation and test subdirectories in other assemblies are **proposed**, not existing APIs. Read root/scoped AGENTS.md, the module README, FEATURES.md's matching row, the specified accepted source sections, frozen interfaces and peer schema. Do not ingest unrelated game art/research.

Inherit the branch/evidence/reviewer/rollback contract from [FOUNDATION_TASKS.md](FOUNDATION_TASKS.md#common-card-delivery-contract). Shared central props/locks, interface baselines, registry migrations and ledgers are coordinator-owned; submit a patch request rather than racing another worker. All services use constructor injection, immutable typed snapshots and captured ownership. Domain, wire and SQL types remain distinct. UI/presenters must show pending/stale/unavailable/error states rather than fake empty success. Unsupported operations continue to fail explicitly.

**Commands for every card:** DN plus SQ for persisted behavior, with exact definitions in RUNBOOK.md. Add the named scenarios to executable tests; test names are proposed. PEER requires a real compatible backend, not the scaffold. Replace a module's old unavailable assertion only when its real behavior tests exist; retain negative capability tests for all still-unimplemented modules. Do not skip the suite based on status flags.

**Delivery for every card:** `impl/<ID>-<feature>`, module code/tests/README, evidence at its manifest path, a coordinator-applied status patch, API/schema/package compatibility, failure/rollback notes and a narrow Unity wiring recipe naming the corresponding INT task. Workers do not edit Unity scenes/manifests/composition. SDK completion requires real local behavior; integration completion additionally requires the actual consumer task and applicable INT-018 evidence. A fake provider or polished placeholder panel never satisfies that gate.

<a id="cl-101"></a>
## CL-101 — Accounts: provision once, bootstrap fully, reopen offline

**Scope:** M2/C; P0. Account/authentication lifecycle, not account merging or a new offline guest implementation.

**Read/evidence:** Accounts stub; PlatformContracts §4; client storage §8; LC07/08; CL-010 staging interface. No provisioning service or secure credential adapter currently exists.

**Dependencies/ownership:** CL-005/010/011; PEER-CORE. Own Accounts-scoped service/contract/store/test files. Auth provider integration is an adapter boundary; token storage is INT-004, not ordinary account rows.

**Steps:** expose explicit never-provisioned/authenticating/provisioning/bootstrapping/Ready/recovery states. Map provider principal to canonical account and app membership without assuming equal IDs. Resume idempotent provisioning for the known principal. Mark Ready only in the transaction installing every required snapshot collection and checkpoint. Later offline boot reopens durable ownership; expired token affects protected network work, not all local reads. Missing credentials for an existing account require recovery, not silent new-account creation. Link identity without rekeying; account switch retires the old scope and preserves its pending work.

**Tests:** interrupted auth versus lost provisioning response; principal retry returns same account; incomplete paged bootstrap never Ready; offline first launch blocks owned mutation; initialized offline restart retains IDs/stream; missing secret, cancelled renewal, A→B late result. A05/A07/A12.

**Acceptance/integration:** INT-006 and INT-010 show real account readiness in both intended entry compositions. Preserve recovery data on rollback. Prompt: Implement CL-101 with explicit readiness/recovery and no account-owned play before complete bootstrap.

<a id="cl-102"></a>
## CL-102 — Profiles: local-first projection with honest pending edits

**Scope:** M3/C–D; P0. Profile read/edit policy; no direct provider/SQL UI access or cross-account merging.

**Read/evidence:** Profiles stub, existing game IProfileProvider/StateRecord guards, DataAndRuntimeFlows §1 and adopted payload/audit policy.

**Dependencies/ownership:** CL-005/008/011; PEER-CORE; Profiles-only files.

**Steps:** define immutable confirmed/pending snapshots and semantic profile read/update commands. Persist an edit and outbox atomically, retaining expected revision and supported unknown extension fields. Refresh by complete account/view scope, coalesce reads, reject stale revisions and handle conflicts through the frozen merge policy. Keep backend-authored audit fields separate from provisional client-observed metadata; never allow createdBy/updatedBy forgery.

**Tests:** offline cached/missing profile; duplicate edit callback; conflicting devices; refresh racing local pending edit; invalid/deep payload; forbidden audit actor; A's late response after B activation; new schema unknown-field preservation. DN/SQ and compatible PEER, A03/A07.

**Acceptance/integration:** INT-008 renders a real profile and pending edit in the actual game, then confirmed data after pull. Cache reset may discard only disposable fetches, not pending edits. Prompt: Implement CL-102 with revision-aware profile reconciliation and a narrow presenter wiring recipe for INT-008.

<a id="cl-103"></a>
## CL-103 — Catalog: versioned definitions and explicit app bindings

**Scope:** M3/C–D; P1. Generic definitions/configuration, not plant/puzzle catalogs or grant authorization.

**Read/evidence:** Catalog stub; accepted backend app-binding principles and PlatformContracts extension/cache sections. No actual catalog provider exists.

**Dependencies/ownership:** CL-005/011; PEER-VALUE. Catalog-scoped ports, local cache/projection and tests.

**Steps:** model stable definition IDs separately from semantic keys and versioned app bindings; preserve per-app presentation payload without overwriting canonical definitions. Validate downloaded definitions before installing a versioned snapshot. Cache by logical backend/app/version/locale/audience where relevant; honor visibility changes and bounded payload schemas. Missing/unsupported definitions remain unavailable and cannot be treated as permission to spend/grant.

**Tests:** same item explicitly bound to A/B versus A-only item; revoked binding; incompatible version; corrupt/incomplete page; payload limits; offline prior validated version; app-specific metadata isolation. DN/SQ/PEER; A07/A08.

**Acceptance/integration:** INT-011 renders actual allowed definitions, not fabricated local catalog success. Keep prior validated version for compatible recovery, never silently reinterpret IDs. Prompt: Implement CL-103 with validated versioned catalog/app-binding snapshots and explicit unsupported-version behavior.

<a id="cl-104"></a>
## CL-104 — Inventory: confirmed holdings, instances and pending intentions

**Scope:** M4/C–D; P1. Read/project inventory and submit permitted intents; no local authoritative grant/consume engine.

**Read/evidence:** Inventory stub, legacy IInventoryProvider, private-feed atomic-group and server-value rules.

**Dependencies/ownership:** CL-010/011/103; PEER-VALUE. Inventory-scoped files; migration registration remains StorageOwner-controlled.

**Steps:** separate stack balances from owned instances and app visibility membership; use checked long quantities and revision-aware projections. Display last confirmed state offline; pending intents never become trusted holdings. Apply server group effects with cursor atomically via CL-010. Submit supported consume/use intent only through frozen authority policy, preserving operation identity. Instance payload/audit fields use bounded typed contracts.

**Tests:** duplicate/upsert/tombstone, older revision, shared item two app views, reset visibility without deleting another view, quantity overflow, unavailable catalog, offline denied spend, pending intent rejection and replay. DN/SQ/PEER; A06/A07.

**Acceptance/integration:** INT-011 inventory changes after a real server result and ordered pull; no double application. Rollback cannot restore revoked rights as spendable. Prompt: Implement CL-104 as server-confirmed inventory projections and explicit intent handling, never a local grant authority.

<a id="cl-105"></a>
## CL-105 — Wallet: exact confirmed balances and safe spend intentions

**Scope:** M4/C–D; P0 for the first one-currency reward slice. No client-ledger authority or floating-point currency.

**Read/evidence:** Wallet stub, signed-int64 policy, server-owned value and private change groups.

**Dependencies/ownership:** CL-010/011/103; PEER-VALUE; Wallet-scoped files.

**Steps:** represent currency definition, exact long confirmed balance/revision and separately labeled pending information. Apply accepted server balance changes monotonically through the feed; do not credit merely because gameplay reports success. An offline cached balance cannot authorize a purchase. Commands carry immutable intent/source identity and backend validates funds. Avoid subtracting a prediction twice when accepted overlay becomes confirmed state.

**Tests:** long extrema/overflow, insufficient funds from stale cache, duplicate grant receipt, accepted response followed by earlier pull then confirming group, concurrent spends on two devices, rejected intent, offline read without spend authority and namespace isolation. DN/SQ/PEER; A01/A06/A07.

**Acceptance/integration:** INT-010 shows one real authoritative reward once; INT-011 shows the value widget and failure states. Rollback preserves receipts and pending operations. Prompt: Implement CL-105 exact wallet projections and pending/confirmed transitions suitable for the early real-game reward slice.

<a id="cl-106"></a>
## CL-106 — Entitlements: durable rights and revocation

**Scope:** M4/C–D; P1. Rights projection/eligibility, not local IAP verification or permanent ownership inferred from UI.

**Read/evidence:** Entitlements stub, legacy EntitlementGrant semantics, Catalog bindings and server purchase authority.

**Dependencies/ownership:** CL-010/011/103; PEER-VALUE; Entitlements-scoped files.

**Steps:** distinguish active/revoked/expired/unknown rights with trusted revision and origin receipt; use frozen offline-read eligibility, never device clock as grant authority. Apply changes through the same private transaction group as purchase/wallet/inventory effects. Preserve app visibility and supported payloads. Expose meaningful unavailable status when proof/schema is missing.

**Tests:** duplicate activation, revoke after activation, stale activation after revoke, offline expiry uncertainty, two-app visibility removal, invalid receipt reference and account switch. DN/SQ/PEER; A06/A07.

**Acceptance/integration:** INT-011 consumes a real entitlement projection and revocation in the game. Cached UI unlock alone is insufficient. Prompt: Implement CL-106 rights projection and revision-safe revocation, with no local grant or receipt-verification shortcut.

<a id="cl-107"></a>
## CL-107 — RewardFulfillment: trusted receipt presentation, not client grants

**Scope:** M4/D; P0 for first slice. Client coordination of confirmed reward outcomes. Backend RewardFulfillment remains the transaction-borrowing authority.

**Read/evidence:** module stub/README, PlatformContracts §7, Wallet/Inventory/Entitlements interfaces and operation/business-source uniqueness.

**Dependencies/ownership:** CL-104/105/106; PEER-VALUE; RewardFulfillment-scoped files. No reusable client API accepting arbitrary grant plans.

**Steps:** reconcile a trusted reward receipt/source/operation with component projections from the same committed change group. Expose pending/accepted-awaiting-pull/confirmed/rejected presentation state without minting value. Dedupe receipt presentation separately from permanent server business uniqueness. Claims or purchases invoke their semantic use cases; do not duplicate Wallet/Inventory/Entitlement mutation logic. A local animation is not a fulfillment acknowledgment.

**Tests:** repeated receipt, different operation IDs for one business source, response loss, accepted result before pull, partial/malformed group, rejected reward and stale account callback. Assert no local increment from UI and no double receipt presentation after restart. DN/SQ/PEER; A06/A08.

**Acceptance/integration:** INT-010/011 display the real backend reward exactly once in effects, with durable receipt evidence. The backend transaction proof must be linked, not recreated with client mocks. Prompt: Implement CL-107 as receipt/projection coordination and explicit reward status; do not port server grant authority into the SDK.

<a id="cl-108"></a>
## CL-108 — Progression: generic durable outcomes and reconciliation

**Scope:** M3/C–D; P0. Portable progress semantics without puzzle/difficulty/plant rules.

**Read/evidence:** Progression stub; existing GameplayOutcome/ProgressEvent; DataAndRuntimeFlows completed-run transaction; PocketBloomOutcomeAdapter and game checkpoint boundary.

**Dependencies/ownership:** CL-008/009/010/011/101; PEER-CORE; Progression-scoped files. The game supplies its own completion writer/outcome policy.

**Steps:** accept validated generic outcome input with stable run/business identity, semantic content/version and captured ownership; commit local pending projection and immutable command through the shared transaction seam. Keep checkpoint serialization game-owned. Reconcile accepted/rejected outcomes with server revisions while preserving other pending work. Expose durable save failure, pending confirmation and confirmed progress; no local trusted reward allocation.

**Tests:** duplicate callback; kill after local commit; lost ACK; same run/different payload conflict; terminal rejection followed by valid next sequence; offline soft restart; account switch with pending result; reset snapshot preserving pending outcome. DN/SQ/PEER; A03/A05/A07.

**Acceptance/integration:** INT-007 actually dispatches real level completion and shows durable pending state; INT-010 proves live acceptance. A sink exercised only by an EditMode fake is insufficient. Prompt: Implement CL-108 generic outcome persistence/reconciliation and the transaction contract needed by MrSquare's real completed-run writer.

<a id="cl-109"></a>
## CL-109 — Quests and the shared multi-objective engine

**Scope:** M4/E; P1. Versioned objective/occurrence projection and claim intent; no one-counter-only replacement or client-time reward authorization.

**Read/evidence:** Quests stub, legacy QuestCounter/QuestCheckpoint, PlatformContracts §5 objective model and §7 reward composition.

**Dependencies/ownership:** CL-008/011/107/108; PEER-OBJECTIVES. Own Quests plus proposed shared Objectives directories under the objective-engine lock; Achievements consumes this engine rather than forking it.

**Steps:** typed counter/max/min/flag/distinct/duration/streak evaluators with units, checked arithmetic and bounded state; register custom game evaluators through neutral ports. Pin definition/evaluator/occurrence versions; preserve/migrate/reset only under explicit policy. Dedupe input events with durable consumer keys, not a cache TTL. Keep pending progress distinct from server confirmation. Claims carry server occurrence/reward-slot identity and compose RewardFulfillment; offline clock cannot create a daily occurrence or claim permission.

**Tests:** three-objective quest (wins, distinct regions, best time), duplicates/out-of-order events, version changes, bounded distinct sets, overflow, unknown evaluator, device clock jump, concurrent/repeated claim and terminal rejection. DN/SQ/PEER; A06/A08.

**Acceptance/integration:** INT-012 displays each objective and a real claim result. Legacy single-counter fixtures remain compatible where deliberately bridged, not evidence of general objectives. Prompt: Implement CL-109 shared objective mechanics and quest lifecycle/claim policy without copying value services.

<a id="cl-110"></a>
## CL-110 — Achievements on shared objectives and occurrence claims

**Scope:** M4/E; P1. Achievement-specific lifecycle/eligibility over the shared engine; no duplicate evaluator implementation.

**Read/evidence:** Achievements stub, CL-109 engine contract, adopted objective/version/reward-slot rules.

**Dependencies/ownership:** CL-109; PEER-OBJECTIVES; Achievements-scoped files, acquire objective-engine lock for coordinated review but request shared-engine edits from its owner.

**Steps:** compose shared objective state with achievement visibility, tiers, occurrence lifecycle and claim eligibility. Pin evaluator/definition versions and support explicit upgrade policy. Persist claim intent/outcome and receipt source; preserve unknown supported extension fields. Server validates authoritative completion and grants through the existing value modules.

**Tests:** multiple objective thresholds, tier slot uniqueness, repeated claims with new operation IDs, shared definition across apps, version transition preserving or resetting only by policy, forged audit fields, pending progress not claim authority. DN/SQ/PEER; A06/A08.

**Acceptance/integration:** INT-012 renders a real achievement with multiple objectives and a backend-confirmed claim, without awarding twice after restart. Prompt: Implement CL-110 by composing CL-109 and RewardFulfillment, preserving occurrence/tier uniqueness and server authority.

<a id="cl-111"></a>
## CL-111 — Store: offers and server-revalidated purchases

**Scope:** M4/E; P1. Offer browsing and virtual-currency purchase intent; no cached-price authority or local debit/grant transaction.

**Read/evidence:** Store stub, Catalog/value interfaces, PlatformContracts purchase transaction and cache policy.

**Dependencies/ownership:** CL-103/107; PEER-COMMERCE; Store-scoped files.

**Steps:** cache offers by app/account/eligibility/catalog/query with explicit freshness. Represent exact prices/currencies, purchase limits and requirement versions. Submit an immutable purchase intent bound to offer/version/operation; backend revalidates price/eligibility/funds and atomically debits/grants/receipts/feed. Present pending, rejected, price-changed and confirmed outcomes. Never treat a successful local button animation as purchase success.

**Tests:** stale price/offer expiry, wrong currency, insufficient funds, two concurrent devices, response loss then retry, same business purchase with different operation IDs, partial-value response and account switch. DN/SQ/PEER; A06/A08/A12.

**Acceptance/integration:** INT-013 actual game offer purchase shows one authoritative debit and grant after pull. Rollback retains uncertain purchase identity. Prompt: Implement CL-111 safe offer/read/purchase orchestration using existing value services and frozen backend purchase semantics.

<a id="cl-112"></a>
## CL-112 — Purchases: receipt workflow and verification boundary

**Scope:** M4–M5/E; P1. Portable purchase state machine and semantic verification requests. No real-money purchase in tests or assumption that a provider callback grants value.

**Read/evidence:** Purchases stub, legacy PurchaseIntent/IPurchaseValidationProvider, trusted verification rules and CL-111 purchase state.

**Dependencies/ownership:** CL-111; PEER-COMMERCE. SDK files only; native billing integration belongs to INT-013 and explicit sandbox credentials.

**Steps:** separate initiated/store-pending/receipt-received/submitted/verification-pending/confirmed/rejected/cancelled states according to provider capabilities. Preserve provider transaction/product IDs; store only approved minimal sensitive metadata behind secure boundaries. Submit stable verification operation identity and await backend receipt/business uniqueness. Resume uncertain purchases and restored receipts without a second grant. A cancellation does not erase a receipt already accepted by the server.

**Tests:** duplicate callbacks, delayed approval, restored purchase, tampered/missing receipt, provider IDs with non-UUID strings, lost response, invalid signature verdict, server-pending then confirmed/revoked and owner mismatch. DN/SQ/PEER; A06/A12.

**Acceptance/integration:** INT-013 verifies a genuine store sandbox receipt end-to-end; fake receipts remain unit-only. No provider has been chosen/installed merely by this plan. Prompt: Implement CL-112 portable purchase verification/recovery boundaries; never authorize entitlements from client receipt data alone.

<a id="cl-113"></a>
## CL-113 — Leaderboards: scoped queries and validated score intents

**Scope:** M5/E; P2. Standings display and score submission, not client season settlement or a replicated global leaderboard feed.

**Read/evidence:** Leaderboards stub, legacy query/entry enums/ordering, adopted query cache and score authority.

**Dependencies/ownership:** CL-005/011/108; PEER-SOCIAL; Leaderboards-scoped files.

**Steps:** key cache by backend/app/board/season/cohort/snapshot/filter/sort/page and personalized account. Display stale pages explicitly; preserve server snapshot pagination. Submit a validated gameplay-related score intent with stable operation/source identity; no direct rank editing. Profile navigation carries caller context without changing tab history. Server owns settlement/reward bands.

**Tests:** changed season/snapshot, page duplication, tied ordering, app-only boards, forbidden scope, stale account callback, offline cached page, forged score and replay. DN/SQ/PEER; A07/A12.

**Acceptance/integration:** INT-014 real query, score result and participant profile return path. Cache deletion cannot erase submission intent. Prompt: Implement CL-113 snapshot-scoped leaderboard queries and validated score intents without making rank/reward authority local.

<a id="cl-114"></a>
## CL-114 — Teams: queries, membership and contribution intents

**Scope:** M5/E; P2. Generic team behavior, not Pocket Bloom gardening mechanics or offline-authoritative membership.

**Read/evidence:** Teams stub, actual TeamSummary/Membership/Intent/Contribution contracts and fake provider tests, backend app isolation.

**Dependencies/ownership:** CL-005/008/011; PEER-SOCIAL; Teams-scoped files.

**Steps:** preserve legacy role/intent/result semantics while exposing scoped cached queries and explicit freshness. Join/leave/contribute are durable server-authorized commands where defined; membership changes invalidate dependent queries. Keep game contribution translation outside the platform. Account changes retire subscriptions and private team cache access. No local permission escalation from stale role data.

**Tests:** join/leave repeated/lost response, duplicate contribution/business source, concurrent membership changes, stale role, team not found versus offline, mixed app access and old-account completion. DN/SQ/PEER; A07/A12.

**Acceptance/integration:** INT-014 actual team screen/member profile navigation and one confirmed contribution. FakeTeamsProvider remains test-only. Prompt: Implement CL-114 team query/intent service preserving ownership, freshness and server membership authority.

<a id="cl-115"></a>
## CL-115 — RemoteConfig: assignment-scoped validated configuration

**Scope:** M3–M5/E; P1 for safe defaults, P2 for breadth. Configuration projection, not executable arbitrary payload or device-selected reward authority.

**Read/evidence:** RemoteConfig stub, legacy RemoteConfiguration<T>, adopted assignment/cache/payload policy.

**Dependencies/ownership:** CL-005/011; PEER-CONFIG-INBOX; RemoteConfig-scoped files.

**Steps:** define immutable versioned configuration with assignment/cohort/visibility metadata and explicit fallback policy. Validate schema/bounds before replacing last-good values; preserve local read availability where allowed. Coalesce refresh and reject stale or wrong-owner response. Account/assignment changes invalidate personalized caches. Keep game-specific settings typed in game adapters and separate from installation feedback preferences.

**Tests:** invalid schema/deep payload, partial config, stale version, two account assignments, expired config offline, missing required setting, safe last-good fallback and account switch mid-refresh. DN/SQ/PEER; A07/A08.

**Acceptance/integration:** INT-015 visibly applies a benign versioned game setting and reports unavailable required configuration correctly. No config payload may confer grant authority. Prompt: Implement CL-115 validated scoped configuration with explicit last-good/failure rules and no hidden global current-player state.

<a id="cl-116"></a>
## CL-116 — Inbox: messages, read state and attachment claims

**Scope:** M5/E; P2. Player inbox feature; distinct from the internal durable event-consumer inbox used for deduplication.

**Read/evidence:** Inbox stub, private feed/cache boundaries, occurrence/business-source uniqueness and RewardFulfillment.

**Dependencies/ownership:** CL-005/008/011/107; PEER-CONFIG-INBOX; Inbox-scoped files.

**Steps:** model server message identity/version, scoped content, read state and claimable attachment state. Preserve durable read/claim intent when policy permits offline admission; server eligibility controls attachments. Apply confirmed receipt/value effects through shared synchronization, not an inbox-specific grant engine. Bound content/payloads and retain explicit expiration/unknown state. Cache eviction cannot delete an unsubmitted claim.

**Tests:** repeated message/update, read retry, claim with new operation ID for same attachment, expiry while offline, missing attachment definition, lost response, duplicate reward receipt, account switch and old message replay. DN/SQ/PEER; A06/A07.

**Acceptance/integration:** INT-015 real message/read/claim path and one authoritative attachment result; empty placeholder list does not qualify. Prompt: Implement CL-116 durable player inbox intent/projection behavior composed with existing reward services, keeping it separate from event deduplication internals.
