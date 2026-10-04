# SDK entry for coordinated work

Shared work starts in [game-platform-workspace](https://github.com/gindemit/game-platform-workspace),
normally the sibling checkout. Read its AGENTS.md and the owner's selected work
order, then request `python3 scripts/agent-guide.py feature ID` or `context TASK`.
One coordinator owns the cross-repository queue; do not launch a second here.

This repository owns portable SDK source, contracts, tests and package production.
[AGENTS.md](AGENTS.md), [current status](docs/IMPLEMENTATION_STATUS.md) and
[the provider](agent/provider.json) describe its local interface. Builds and local
validation do not require the coordination checkout. Historical P3/G3 and broad-P4
gates remain unchanged; Unity consumer acceptance remains a separate result.
