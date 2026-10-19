# Documentation index

[Project overview](../README.md). This index covers the documents and reference sources in this Git snapshot. A contract describes required behavior; a report records evidence and limitations. Neither a plan nor a passing automated check implies completed user acceptance.

## Decisions and work coordination

- [Compact context loading and renewal](CONTEXT-WORKFLOW.md), [current handoff](CONTEXT-HANDOFF.md).
- [Detailed decisions](DECISIONS-DETAILS.md) and [question register](OPEN-QUESTIONS-DETAILS.md): topic lookup only, not mandatory full reading.

- [Project rules and working language](../AGENTS.md).
- [Accepted decisions](DECISIONS.md).
- [Open questions and decision boundaries](OPEN-QUESTIONS.md).
- [Defects and permitted work](BACKLOG.md).
- [Current task and file ownership](M3-WORK-OWNERSHIP.md).
- [Coordination history](PROTOTYPE-COORDINATION.md).
- [Delivery roadmap](IMPLEMENTATION-ROADMAP.md).
- [Graphics and UI delivery order](CLIENT-DELIVERY-PLAN.md).

## Target-game requirements

- [01. Product scope](01-product.md).
- [02. Server simulation](02-simulation.md).
- [03. Content and scripts](03-content-and-scripts.md).
- [04. Gameplay and economy](04-gameplay.md).
- [05. Control and session lifecycle](05-control-and-session.md).
- [06. Fleets and combat](06-fleets-and-combat.md).
- [07. Networking and visibility](07-network.md).
- [08. Browser client](08-client.md).
- [09. Persistence and recovery](09-persistence.md).
- [10. Validation and delivery stages](10-validation-and-delivery.md).
- [11. Victory points and achievements](11-victory-and-scoring.md).
- [Victory-points reference text](references/victory-points-source.txt).

## M1 — economy laboratory

- [Scope and acceptance criteria](12-milestone-economy.md).
- [Workforce, production and job upkeep](15-workforce-and-job-upkeep.md).
- [Trait workforce import](16-trait-workforce-import.md).
- [Limitations and disputed questions](M1-DISPUTES.md).
- [Run instructions and initial validation](13-m1-validation.md).
- [Manual comparison with Stellaris](14-manual-economy-comparison.md).

## M2 — network sandbox

- [Scope and lifecycle](17-m2-network-sandbox.md).
- [Server and protocol](18-m2-protocol.md).
- [Browser client](19-m2-client.md).
- [Connection-liveness contract](M2-CONNECTION-LIVENESS.md).
- [Questions and limitations](M2-DISPUTES.md).
- [Run instructions and short check](20-m2-run-and-test.md).
- [Full manual acceptance plan](22-m2-manual-acceptance.md).
- [Feedback questions and result form](23-m2-test-questions.md).
- [Validation report](21-m2-validation.md).
- [Connection-liveness validation](M2-LIVENESS-VALIDATION.md).

## M3 — scope and contracts

- [Graphical slice and acceptance criteria](M3-GRAPHICAL-SLICE.md).
- [Local asset access and resource lifecycle](M3-LOCAL-ASSET-IMPORT.md).
- [Asset profile and reader limits](M3-ASSET-MANIFEST.md).
- [Static bind pose, layers and materials](M3-STATIC-VISUAL-ADAPTER.md).
- [Daily phases and movement](M3-TIME-AND-MOVEMENT.md).
- [Fixed world and N01 numeric profile](M3-WORLD-FIXTURE.md).
- [Spatial DTOs and commands](M3-SPATIAL-PROTOCOL.md).
- [Accepted answers](M3-QUESTIONS.md).
- [Remaining questions and limitations](M3-DISPUTES.md).

## M3 — runtime contracts

- [Simulation core](M3-SIMULATION-CORE.md).
- [Session runtime](M3-SESSION-RUNTIME.md).
- [Manifest runtime](M3-ASSET-MANIFEST-RUNTIME.md).
- [Import worker](M3-IMPORT-WORKER.md).
- [Client interactions](M3-CLIENT-INTERACTIONS.md).
- [Client runtime](M3-CLIENT-RUNTIME.md).
- [Renderer runtime](M3-RENDERER-RUNTIME.md).

## M3 — current visual milestone

- [Permitted visual scope](M3-VISUAL-FIDELITY.md).
- [System visuals, materials and lighting](M3-SYSTEM-VISUALS.md).
- [Star import dependencies](M3-STAR-IMPORT.md).
- [DOM interface layout](M3-UI-VISUALS.md).
- [Current reference-driven revision and ownership](M3-REFERENCE-COMPARISON.md).
- [Personal outliner and observer player perspectives](M3-PLAYER-PERSPECTIVES.md).
- [Bounded presentation imports, LOD and memory limits](M3-PRESENTATION-IMPORT.md).
- [Minimal galaxy core/dust dependencies and inferred bindings](M3-GALAXY-PRESENTATION-IMPORT.md).
- [Current comparison table and delivery limitations](reports/m3-visual-milestone-validation.md).

## M3 — launch and manual acceptance

- [Launch instructions and three-client check](M3-RUN-AND-TEST.md).
- [Manual acceptance checklist](M3-MANUAL-ACCEPTANCE.md).
- [User-reported results; full acceptance remains incomplete](M3-MANUAL-RESULTS.md).

## M3 — validation and research reports

- [Simulation-core validation](M3-CORE-VALIDATION.md).
- [Worker validation](M3-WORKER-VALIDATION.md).
- [Initial integrated slice](M3-INTEGRATION-VALIDATION.md).
- [Initial decoder checks](M3-IMPORT-VALIDATION.md).
- [Camera validation](reports/m3-camera-validation.md).
- [Star-import validation](reports/m3-star-import-validation.md).
- [Star-material validation](reports/m3-star-material-validation.md).
- [Independent visual QA and its limits](reports/visual-qa-validation.md).
- [Observer perspective server and transport validation](reports/m3-player-perspectives-validation.md).
- [Material research handoff](reports/m3-visual-research-handoff.md).

## Preliminary renderer experiment

- [Experiment scope](ASSET-PROBE-SPEC.md).
- [Results and renderer selection](ASSET-PROBE-REPORT.md).
- [Standalone probe instructions](../tools/asset-probe/README.md).

## Future construction — paused

These are preparatory materials, not approval of M4 or implemented construction.

- [Proposed construction slice](PROTOTYPE-CONSTRUCTION-PROPOSAL.md).
- [Draft definition-import contract](CONSTRUCTION-DEFINITION-IMPORT.md).
- [Planned reference checks](CONSTRUCTION-452-MANUAL-ACCEPTANCE.md).
- [Independent next-slice research](reports/construction-next-slice-452.md).
- [Hydroponics dependency research](reports/hydroponics-import-dependencies-452.md).

## Keep documentation compact

- Keep the root README as an entry point and this index as the complete file list.
- Record decisions briefly in DECISIONS and detailed behavior in its owning contract. Link to that contract instead of repeating it.
- Keep validation evidence separate from requirements. Extend the relevant report instead of adding a file for each run or agent message.
- Create a new file only for a separate topic or contract; update this index and affected links when files change.
- Preserve historical evidence and distinguish implemented behavior, remaining limitations and proposed work.
