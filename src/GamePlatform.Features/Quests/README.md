# Quests

`QuestService` durably queues `quest.claim` intents for a captured owner and
client stream. Repeating an occurrence key returns the original immutable command
admission. Hosts inject the claim codec and atomic outbox store; the service never
evaluates objectives or grants rewards locally.

The legacy `QuestsFeature` facade remains unavailable. Broader multi-objective,
eligibility and claim acceptance (A06/A08) remains separate. See
`StoreQuestServiceTests` for local source coverage; it does not establish
generated-package, Unity, native or physical-device acceptance.
