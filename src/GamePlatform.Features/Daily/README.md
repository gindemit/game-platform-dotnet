# Daily

Owns the account-bound `DailyService`: authenticated `daily.status` reads with a
bounded stale durable cache, immutable `daily.claim` admission keyed by the server
policy version and UTC date, and explicit recording of confirmed `daily_claimed`
receipts into the cached history. Eligibility, cycle day and rewards are
server-owned; offline reads return the last confirmed status as stale and never
grant locally.

`daily.claim` travels through the A06 superset push route behind the same
store/quest capability flag. Source tests do not establish generated-package,
Unity, native or physical-device acceptance.
