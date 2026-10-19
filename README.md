# StellarisServer

An authoritative server and fully browser-based strategy client using Stellaris 4.5.2 data. Unity is not used. Original and derived game assets stay outside Git; each client reads its own local installation.

The project includes the M1 economy laboratory, M2 network sandbox and M3 graphical network slice. The current combined stage adds planet construction, fleet controls and navigation. An [intermediate playable review candidate](docs/reports/intermediate-playable-rc-2026-10-13.md) supports bounded economy/fleet/navigation checks; live construction is still incomplete. This candidate does not finish the combined stage or replace its remaining goals. M4–M10 remain proposals.

- [Complete documentation index](docs/README.md).
- [Launch M3 and check three clients](docs/M3-RUN-AND-TEST.md).
- [Manual checklist](docs/M3-MANUAL-ACCEPTANCE.md) and [user-reported results](docs/M3-MANUAL-RESULTS.md).
- [Independent visual QA](docs/reports/visual-qa-validation.md) and [initial integration report](docs/M3-INTEGRATION-VALIDATION.md).
- [Run M2](docs/20-m2-run-and-test.md); [run the M1 laboratory](docs/13-m1-validation.md).
- [Project rules](AGENTS.md), [decisions](docs/DECISIONS.md), [open questions](docs/OPEN-QUESTIONS.md).
- [Backlog](docs/BACKLOG.md) and [task/file ownership](docs/M3-WORK-OWNERSHIP.md).

M2 supports [any positive number of players](docs/M2-PLAYER-COUNT.md), including a solo session. All players must connect and be ready before starting; the current M3 implementation still has a separate two-player limit.

M1/M2 support a limited 4.5.x economy profile tested on 4.5.2; M3 requires the exact 4.5.2 profile. Refer to the linked documents for launch instructions, asset counts and validation status.
