# Browser Client

The first interactive M2 client is limited to the economy sandbox: [doc19](19-m2-client.md). It has no map, combat or original game assets; the other game screens below are later stages.

## Platform and State

A fully browser-based application without Unity, primarily targeting Windows. Current direction: TypeScript, DOM for the interface and GPU rendering for the map and combat. Based on the [technical investigation](ASSET-PROBE-REPORT.md), M3 uses Three.js 0.186.1 and WebGL2; WebGPU is not required. Google Chrome is the M3 target browser; a Chrome-specific check and minimum GPU requirement remain. Babylon.js is only a comparison tool for the workbench, not a game-client dependency.

Keep a local copy of the state authorized by the server. Locally handle the interface, camera, object selection, sorting, filtering and interpolation. Do not calculate authoritative economy, combat results or command eligibility.

## Screens for the First Playable Release

- Connection and recovery.
- Lobby: participants, observers, empire templates, readiness, map settings and start.
- Galaxy map and system view; selecting planets, fleets and stations.
- Resource, date, speed and pause panel.
- Planet: population, Workforce, jobs, districts, buildings and queues.
- Research and limited traditions.
- Fleets, design, shipyards, orders and combat display.
- Diplomacy, wars, peace, proposals and events.
- Host controls, authority transfer, kick and ban.
- Surrender flow with confirmation that it is irreversible.
- Game results and observer mode.

Record window order, keyboard shortcuts and layout details before implementing each screen. Reference fidelity does not replace interaction specifications.

## Stellaris Assets

M3 boundaries and movement/calendar clarifications are agreed: see [local import](M3-LOCAL-ASSET-IMPORT.md), [graphical slice](M3-GRAPHICAL-SLICE.md) and [answers](M3-QUESTIONS.md). There is no persistent cache; in-memory resources are reused. The manifest, time/movement, spatial protocol and fixture are specified in subcontracts; N01 is agreed. The limited BGRA8 reader has been checked, but the full playable M3 is not ready. See [remaining questions](M3-DISPUTES.md).

User clarification, 12 October: the galaxy map and system view are 3D. The host/server selects its installation, while each browser client selects its own local Stellaris folder. Serving the host's game assets to clients is not the chosen approach. Access and conversion methods must be specified before implementation; 3D alone does not provide format compatibility.

The client uses ready-made game resources. A browser cannot directly read an arbitrary installed folder and is not required to support Stellaris texture, model, animation or material formats.

M3 uses local import from each participant's folder in the browser, without a persistent cache; see M3-LOCAL-ASSET-IMPORT. Remaining work includes a visual asset-set gate, folder access, worker/cancellation and memory usage. Entering a path does not give a page access to files. A native helper and full animation import are outside the agreed M3 scope and require a separate decision.

Keep project code and converters separate from original and converted game resources. Do not include derived resources in the public Git repository. Prepared packages need a manifest, versions and hashes; missing assets must produce visible diagnostics.

## Testability

The application must run with a test server and fixed data set. Browser automation must access the UI through stable identifiers. Visual effects should support a reproducible test mode. GPU rendering checks supplement, but do not replace, interface and state checks.

## Acceptance

Two browsers independently control their own empires; pause and reconnection are shown; commands receive confirmation; combat is animated without the client deciding its outcome; and hidden data is unavailable to the UI and browser storage. Define target FPS, memory budget and initial-load time after selecting hardware and assets.
