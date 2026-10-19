# M3 — Residual Questions After Review

Updated 12 October 2026. Accepted decisions are in DECISIONS/M3-QUESTIONS. Project-wide reference is 4.5.2. M01–M03 and N01 are closed; 7.2 units/day and 15-day preparation were derived from the game, while radii 150/140 and a 5-day transition were separately authorized for M3 only. This table does not reopen these decisions or authorize changing mechanics.

## Questions Requiring Decisions

| ID | Remaining question | When it blocks / recommended path |
|---|---|---|
| R01 | How do we confirm that the 4.5.2 reference sources are clean: Steam verification, a verified manifest or a separate copy? | Before claiming vanilla runtime parity. The active playset includes Workforce Base Output per 100 and Exo 2 Font; checked files from these mods change localization/fonts. A Git diff in the installation may occur after an update and does not prove gameplay modifications. For manual checks, explicitly record the playset and checksum shown by the running game. |
| R02 | Full profile of DLC/content used beyond the selected M3 scope | Before dependent content. M3 requires only its profile's required files, not every installed DLC. |
| R03 | Hyperlane travel-duration formula, entry/exit geometry and the original's full speed-effect stack | Before replacing agreed N01 outside test M3. HYPERDRIVE_INTERSTELLAR_TRAVEL_SPEED=1.0 does not define a sufficient formula. Research/measure, then agree on a change; do not invent new numbers. |

## Engineering Checks, Not New Mechanical Decisions

| ID | Remaining work | Source / closure criterion |
|---|---|---|
| E01 | M1 runtime version check accepts 4.5.x although only 4.5.2 was checked | D44 pins the reference; this document does not change code. Separate task: agree loader behavior for unverified patches and add rejection/diagnostic checks. |
| E02 | Follow-up manual Chrome check: station, star, planet and icons | Profile v2: modern outpost from locator-only frame and core section with three render submeshes; legacy military_station_small excluded. Rigid bind pose and numeric checks implemented; preliminary in-app-browser inspection is in M3-IMPORT-VALIDATION. Chrome/FPS/lifecycle are not PASS yet. Under the current user decision, pending manual checks do not block autonomous implementation. Alternative geometry requires a user decision. |
| E03 | Browser folder-access lifecycle | Server manifest/digest, real 44 File and Worker checked; negative/atomic/cancellation cases covered by automated tests. Real Chrome grant/cancel/revoke remain pending; [results](M3-INTEGRATION-VALIDATION.md). |
| E04 | Manual check of client gestures and focus | CLIENT-INTERACTIONS/RENDERER-RUNTIME/CLIENT-RUNTIME contracts implemented; gating/interpolation/DOM handlers checked automatically. Full Chrome scenario remains pending; server movement was not changed for display purposes. |
| E05 | Actual RAM/VRAM, network and server-tick thresholds | FPS/import time and defensive decoded limits are defined. Measure and agree on remaining budgets before load acceptance. Probe does not replace the 1080p scenario with three clients. |
| E06 | Remaining M2 manual checks | Short scenario passed; doc22 T01–T14, physical LAN, long-duration load and visual upkeep breakdown do not automatically become PASS. |

Project-wide questions about victory, inactivity, retreat, full import and minimum hardware remain in [OPEN-QUESTIONS](OPEN-QUESTIONS.md). They block only dependent tasks, not all M3 work.
