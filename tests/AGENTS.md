# Test instructions

Follow root guidance. Test implemented behavior; stub-failure tests prove only explicit unavailability. Do not substitute fakes for integration evidence. Include multi-account/app, retry/duplicate, overflow, cancellation and fault cases. Use deterministic clocks/IDs and controlled races, not sleeps. Never use production credentials or networks. Update the acceptance ledger with exact evidence and unrun gates.
