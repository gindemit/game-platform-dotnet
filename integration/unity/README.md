# Unity integration

The repository contains the generated Git UPM at `upm/com.gindemit.game-platform`, including the verified managed dependency closure, selected native SQLite libraries, importer metadata and lifecycle policy. Source-controlled package-generation policy lives in `integration/unity/package/`; the game consumer and its manifest remain in the separate Unity repository.

Package presence is not consumer acceptance. The current package/source/Unity pins and exact qualified scope are recorded in [SDK status](../../docs/IMPLEMENTATION_STATUS.md) and the [campaign handoff](../../docs/implementation/CAMPAIGN_UPM_HANDOFF.md). A newer generated package does not become the consumer pin until import verification and the required Editor/IL2CPP/device checks pass.
