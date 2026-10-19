# Prototype Defect Backlog

## Latest bounded investigation results — 13 October 2026

Latest direct human priority after manual review: bring the planet menu to the reference in UI and functionality. Complete the ongoing PERF cycle, then prioritize bounded reference-backed planet work; population detail remains lower priority. [Binding priority and limits](DECISIONS.md#planet-menu-priority-after-manual-review--13-october-2026).

The partial human session is recorded in [M3-MANUAL-RESULTS](M3-MANUAL-RESULTS.md#intermediate-9fc45e5--manual-review-ended-13-october-2026), integrated at `729a040`. Secondary required corrections: remove legacy coordinate-entry UI; add system zoom-out-only return to galaxy; investigate still-unaccepted observer overview/outliner and planet appearance against archived originals. These are authorized corrections, not completed fixes. Preserve bounded functional fleet/navigation/observer confirmations, distinguish them from visual acceptance, and do not mark the declined unsent-draft check manually passed. Construction was excluded from this review, not removed from scope. [Required behavior](FLEET-NAVIGATION-STAGE.md#next-revision-amendment--13-october-2026).

- ECON-001: reproduced focused-button stale-report path corrected in `2208e98`; reviewed real-v7 replay `d53bf04` passed next-month report/HUD refresh and unsent-draft preservation, with explicit reconnect/observer limits. Historical month-7/8 causality remains unresolved. [Evidence](reports/combined-browser-validation.md#econ-001-assembled-fix-results--13-october-2026).
- MOVE-001: reviewed first-order replay `0a76f67` reached S02 through approach/charging/transit after 44 accepted paused day steps, without stop/replacement or premature idle. Historical preparation/position differs and original first ACK is absent; this is bounded non-reproduction, not global closure. [Evidence](reports/navigation-browser-validation.md#move-001-first-order-results--13-october-2026).
- B01: current M3 deterministic test/report `f2f22db`, integrated `437b066`, confirms refusal stops retries as the accepted liveness contract requires. Manual return preserves the pending ID and waits for fresh asset acknowledgement. Integrated connection suite: **12 PASS, 0 FAIL, 0 SKIP**. Actual one-way network loss and server cleanup timing remain unverified. No production retry-policy change is authorized by this finding. [Evidence](reports/b01-current-m3-connection.md).

- B02: reviewed current timer assessment `a90c4ed`, integrated `002404e`, identifies a conditional long-calculation risk, without reproducing it or establishing a violation of the existing no-batching contract. No scheduling change or physical slowdown claim. [Evidence](reports/b02-current-timer.md).
- PERF-001: reviewed backend and UI are assembled at `2bf5196`. Root backend checks against the actual game root passed **316** checks; focused integrated client suites passed **116** tests. Reviewed actual local-import browser/raw-transport evidence `c3f6435`, integrated `aa4bf75`, records whole-interval means 0.95357109 / 1.89316011 / 3.49738980 days/s at configured 1/2/4; matching trailing windows agree with wire 0.9 / 1.9 / 3.5. Root recalculated intervals and verified both archive copies. Bounded measurement/display/reset checks pass; exact live stale threshold, same-sequence callbacks and broad load/native/Chrome conditions remain unverified. No causal slowdown claim. [Backend evidence](reports/perf-001-backend-checks.md), [UI evidence](reports/perf-001-ui-consumer.md), [actual QA evidence and limits](reports/simulation-throughput-validation.md#assembled-runtime-results--13-october-2026).

The clean stable intermediate review candidate is `9fc45e5`; [its report](reports/intermediate-playable-rc-2026-10-13.md) distinguishes working economy/fleet/navigation flows from incomplete live construction. Later numerical machinery and throughput integration are coordination-only. Historical deferred wording below is superseded only by these explicit bounded investigations and the current finite fallback authorization.

## ECON-001 — current bounded refresh correction

Integration verification, 13 October 2026: integration root inspected the actual production/test/contract diff of `163cc112` and integrated it as `2208e98`. Focused actual-client checks passed **96/96**, with no skipped tests; assembled Server Release passed with **0 warnings, 0 errors**. Initial independently reviewed QA plan/evidence `0a132a7` / `977c1e7` are integrated as `f63703c` / `6bd2f68`. Exact assembled browser correction replay is assigned to the existing QA owner at `2208e98`; this record does not claim that replay passed, close historical intermittent server observations or certify full construction readiness.

13 October 2026, QA reproduced on `3c0c681`, implementation base `0400c01`: with the laboratory vacancy Apply button focused, the clock reached 2200.05.01 and HUD showed +6 while the panel retained report month 3/output 12. Releasing focus and receiving another snapshot refreshed it to month 4/output 6. Cause: renderEconomy returns for any focused descendant, including a button. This proves a stale browser report; the historical month 7/8 server-command failure is not established by this reproduction.

The panel must reconcile authoritative month/report/state changes even while a button or numeric field is focused. Preserve the active numeric field's unsent raw draft and focus only for the same actual participant, confirmed perspective, typed planet identity and catalog job, while still own/editable/running/fresh/asset-ready. Keep selection/caret only where the browser exposes it for number inputs; do not claim unsupported native selection APIs. An unchanged focused field follows new received counts. A draft never changes the received model or sends a command automatically. Disconnect/readiness loss, observer or POV/owner change, removed planet/job and cleared login discard the draft before later re-enabling; no draft persistence/cache across invalid contexts. Non-editable rows render received values, and unavailable job identities stay disabled. Missing values do not become economic defaults. Preserve existing command envelopes, reports/privacy, import ownership and UI outside this correction.

Use the existing renderer and targeted focus capture/restore, not a new form framework or server schema. Refresh identity includes authoritative month, planet/report data, catalog and edit eligibility; transport/readiness changes reconcile even without a new planet payload. Acceptance: actual-client test reproduces focused-button stale month/report; dirty numeric draft survives received updates with focus/caret where supported; unchanged input adopts received count; invalid contexts clear drafts and cannot resurrect them on reconnect; two players/observer and removed identities remain isolated. Focused checks and independent actual-diff review are required. Browser PASS waits for QA's exact assembled replay. MOVE-001 and other fallback tasks are not part of this correction.

Reviewed implementation handoff, base `0400c01`, branch `fix/economy-report-refresh`: UI owner `/root` authored the bounded client correction; `/root/economy_refresh_tests` authored focused fixtures/tests with explicit `gpt-6-luna`, `low`, `fork_turns: none`, no nested delegation. Independent reviewer `/root/ui_stage_review` kept existing model/effort, approved the actual three-file diff after its P2 synthetic-DOM selector/reparenting finding was corrected and re-reviewed. The focused Apply case checks refreshed month and produced amount; owner-only invalidation also checks later ownership restoration cannot resurrect the draft. `node --test tests/client/m3-client.test.mjs`: 96/96 PASS; `git diff --check`: clean. No remaining P1/P2 in the bounded review. Synthetic DOM checks do not establish native number-input caret support or assembled-browser PASS; QA's exact replay and integrator review/integration remain pending. Actual usage savings are unavailable and not claimed. Result commit is supplied by the UI owner's completion handoff.

13 October 2026 finite fallback authorization: after independent current-stage work is exhausted, process **ECON-001 → MOVE-001 → B01 → B02 → PERF-001** in that order. Inspect current applicability and reproduce first; fix established defects under existing contracts. PERF measures authoritative simulation days per real second, not client FPS. Follow the [waiting policy](ARCHITECTURE-WAITING-POLICY.md): one implementation at a time, complete independent review/checks/integration before the next, no arbitrary backlog expansion. Historical statuses below remain evidence; this authorization does not declare observations reproduced or fixed.

## Active combined delivery authorization

### Separate gameplay feature candidates

13 October human extension permits bounded source-backed slices below, after independent current-stage work and the five ordered fallback tasks, unless reprioritized. These are candidate investigations, not implemented features or selected original definitions. Choose exact 4.5.2 IDs/dependencies before code; reject heavy missing foundations or unresolved essential semantics. Each implementation stays on its own experimental feature branch until human manual testing **and explicit approval**, including no indirect integration through the shared branch. See [branch gate and handoff](ARCHITECTURE-WAITING-POLICY.md#separate-gameplay-feature-branches-requiring-human-approval).

| Candidate | Bounded first scope and evidence gate |
|---|---|
| ANOMALY-SLICE-001 | One source-defined anomaly's discovery/progress/result lifecycle and minimal actual inspection/action UI; first verify survey/science-ship ownership, assignment, timing, reward and repeat/cancel dependencies. No whole anomaly catalog or invented reward. |
| ARCHAEO-SLICE-001 | One source-defined excavation site's stage/progress/result lifecycle and small site view; first verify scientist assignment, roll/timing, ownership, interruption and event/reward dependencies. No fabricated stage rolls or full archaeology engine. |
| SITUATION-SLICE-001 | One source-defined situation's trigger/progress/approach/result lifecycle and minimal status/action UI; first verify owner visibility, modifier/event application/removal, termination and repeatability. No generic event framework expansion solely to keep busy. |

Branch test handoff: exact clean candidate commit, source IDs/evidence, independently reviewed diff, focused checks, isolated branch launch commands/ports, reproducible player steps and expected results, explicit unsupported conditions, and pending human manual-test/integration approval. No promotion of unapproved candidate code to stable main.

12 October 2026, latest direct user instruction: PICK-001, VIEW-001, OUTLINER-001, FLEET-UI-001 and SYSTEM-UI-001 are required implementation in the current combined construction/manual-review delivery. [Fleet/navigation contract](FLEET-NAVIGATION-STAGE.md) defines behavior, ownership, evidence limits and checks. This supersedes older planning-only/deferred wording for these items, without closing their acceptance or authorizing unrelated historical fixes. Preserve OBS-CAM-001 camera continuity. Stable main readiness requires the combined reviewed delivery, not an integration-worktree build.

## Latest manual session and independent review — 12 October 2026

Manual-session documentation checkpoint: `4b5dfbc`, after delivery `caadc6d`; subsequent reviewed local correction is integrated at `d1d9ef1`. Source evidence: `4b5dfbc:docs/M3-MANUAL-RESULTS.md` and independent review `c638a06`. The user has finished this manual session, not granted blanket visual acceptance or next-milestone implementation. The older triage below predates these results. Only the separately authorized OBS-CAM-001 correction below changes production behavior.

| ID / classification | Evidence and required follow-up |
|---|---|
| OBS-CAM-001 — local correction reviewed; manual recheck pending | User confirmed player/general switches reset camera. Client handler forced `showGalaxy()` and cleared view state; bounded correction removes those actions while clearing old selection/orders/economy and resetting interpolation. Luna low pilot passed 13 focused checks, independent diff review, plus 6 existing renderer regressions. No browser/manual camera PASS claimed; unavailable-view policy remains unresolved and existing renderer fallback unchanged. Supersedes prior unconditional perspective-reset requirement. |
| PICK-001 — confirmed usability issue | Selecting systems on the galaxy map is very difficult. Define larger/easier target affordances with nearby-object discrimination before implementation; no proven root cause. |
| VIEW-001 — accepted new requirement | Near/far information detail changes automatically with camera zoom. Exact displayed sets, thresholds and transition behavior remain design questions; four manual presets alone do not satisfy this. |
| OUTLINER-001 — accepted new requirement | Double-click owned/visible object opens it in its system view. Cross-system/transit/focus behavior needs a contract. |
| FLEET-UI-001 — accepted presentation/input requirements | Native-style map-click orders, fleet panel and visible movement route/destination marker. Reference images are in manual evidence, not copied into Git. Gestures/command semantics and ETA rules require evidence; still screenshots do not establish timings. |
| OBS-UI-001 — accepted presentation requirement | General country overview and grouped selected-country outliner, with country selection accessible after switching. Reference follow-up is received; icon meanings/indicators require evidence. General country overview does not contradict hiding a personal owned-object outliner. |
| SYSTEM-UI-001 — reference-driven requirement | Show exits/neighbor labels plus separate inner and outer boundaries. User says inner is crossable with exits along it, outer limits manual movement. No radii/collision formula established; existing M3 exceptions remain until a contract changes them. Automatic combat kiting beyond outer boundary is only a future proposal; combat stays excluded. |
| WINDOWS-001 — accepted usability requirement | Connection is excluded; other future windows should coexist, one per category. Categories and interaction policy remain unresolved. Current panels work individually but cannot both remain open; no category taxonomy invented. |
| CAM-001 — bounded new manual PASS | User confirms no-selection galaxy center, selected system/planet after pan and no-selection system after Escape. Preserve these scenario results; moving-fleet/other unperformed conditions and broader native acceptance remain unverified. OBS-CAM-001 remains a separate defect. |
| ECON-001 / MOVE-001 — historical observations remain open | One new 2 → 1 → 2 technician sequence and local/stop/neighbor movement passed by user confirmation. They do not close earlier intermittent observations without their reproduction/investigation evidence. Actual throughput was not measured; PERF-001 remains open. |
| Session/privacy — bounded new manual PASS | Observer resource/outliner switching and read-only restrictions, private player economy, cross-client fleet state, stress return and player2 F5/reimport/reconnect confirmed. No full visibility/native fidelity, forced network-failure or load PASS inferred. |
| B01 — independent P2 review finding | `c638a06`: legacy client VM timeout → busy-token refusal ends auto-rejoin (two attempts, then offline). Real shared-network scenario not tested. Protocol currently stops after any join refusal; temporary-vs-final classification needs contract review and current M3 applicability check. No fix or reproduced current M3 claim. |
| B02 — independent preliminary P2 review risk | `c638a06`: M2 Tick schedules next month using pre-calculation time, risking catch-up after a calculation longer than interval. Static finding, long-calculation scenario not reproduced. Verify applicable current code/time contract and deterministic clock oracle before a fix; no measured slowdown claim. |

Reference image identifiers and exact bounded results remain in the manual journal at the main commit above; do not cherry-pick the independent branch's entire historical BACKLOG over this file. MAP-001/native visual fidelity, performance and unavailable conditions remain open. Future fixes follow [execution policy](EXECUTION-POLICY.md) after explicit authorization.

## Current triage after delivery caadc6d — 12 October 2026

This reconciliation is documentation only. Stable main remains at `caadc6d` during the user's review. The next stage is planning and questions only until the user agrees its scope; no production implementation or automatic backlog repair is authorized by that planning request. Monitoring remains paused.

Sources: [historical manual results](M3-MANUAL-RESULTS.md), [final delivery evidence](reports/m3-visual-milestone-validation.md), [final independent QA](reports/visual-qa-validation.md), and the manual acceptance chat's latest user turn `01a13090-49eb-79f1-8f3b-e792647583a8`. That turn asks whether the current work is complete and supplies no new defect observations or acceptance of `caadc6d`. Earlier user results must not be relabeled as results for the new revision.

| Item | Current classification | Remaining action / evidence boundary |
|---|---|---|
| ECON-001 | Historical unresolved observation; investigation deferred | Input/ACK/state/month boundary mismatch is not diagnosed. Later asymmetric-economy QA does not reproduce or close the original 2 → 1 → 2 scenario. |
| MOVE-001 | Historical unresolved observation; investigation deferred | Initial interrupted order lacks captured ACK/state sequence. A successful subsequent move does not close it. |
| PERF-001 | Requested missing measurement, not proven slowdown | Actual authoritative simulation days/second remains distinct from configured speed and client FPS; agree measurement contract before implementing. |
| CAM-001 | Correction implemented and available checks passed; user validation pending | Historical latest failure is the no-selection galaxy pivot after pan. Earlier selected-object/system PASS remains bounded to that run. Repeat physical gestures on `caadc6d`; moving-fleet and held-drag/modal conditions remain unverified. |
| MAP-001 | Reference-driven revision implemented; native visual acceptance pending | Original sprites, sky/core/dust, presets and calibrated composition are delivered. Native relative sizes, lighting/corona and transfer equivalence remain unproven. The earlier galaxy exclusion is superseded by the comparison contract. Do not declare the visual complaint closed. |
| Panel label overlap found by final QA | Fixed and rechecked in available browser QA | Measured panel exclusion passed expanded/collapsed, selection and resize checks. Preserve the finding and fix in the QA report; no currently reproduced open defect from that scenario. |
| Brief background-tab offline status | Unconfirmed historical observation | User saw recovery on refocus; cause/reproducibility unknown. Reproduce with timestamps, role/token and server connection evidence before diagnosing. No new current-run evidence. |
| Transient blank galaxy during rapid QA actions | Unresolved observation with bounded successful follow-up | Three cycles / nine acknowledged POVs with far/near views passed. This does not prove every rapid-switch race absent; preserve original uncertainty in the QA report. |
| TEST-001 | Available checks completed within delivered scope; unavailable conditions remain open | Exact results and overlapping suite counts are in the delivery report. Physical input, induced GPU loss/restore, target Chrome FPS/RAM/VRAM, physical TCP slow-reader, privileged symlink and original-game comparisons are not PASS. No broad rerun is requested. |

New manual findings should identify the actual revision, role, reproduction steps, expected/actual behavior and available evidence. Record them before diagnosis; do not invent findings from pending checks. Feature requests for the next stage belong in its proposed plan, not in the defect list. The historical entries below retain their original observations; this table supplies the current status where earlier wording differs.

## Historical defect records and original task boundaries

User instruction from 12 October 2026: add discovered issues to the backlog; do not diagnose or fix them independently. Revisit them only on a separate request.

Later direct user instruction authorizes the [M3 visual stage](M3-VISUAL-FIDELITY.md): MAP-001, CAM-001 and suitable TEST-001 coverage. The previous prohibition is lifted only for this scope; ECON-001/MOVE-001/PERF-001 remain deferred. A visual fix does not mean manual acceptance is complete.

## TEST-001 Run Automated Checks for the Remaining M3 Backlog

- **Status:** suitable checks are authorized in the M3 visual stage; other scenarios remain deferred. Remaining manual tests are not considered passed.
- **Scope:** browser FPS/p95 measurements for a normal scene and 200 corvettes; 100 galaxy/system/stress view switches with resource growth and available memory metrics monitored; forced WebGL context loss/recovery; controlled network latency, blackhole, disconnects, slow client and lost acknowledgements; available tab-freeze/resume scenarios. Repeat existing symlink checks in Windows with required privileges.
- **Approach:** first review existing automated coverage and run suitable checks; automate missing scenarios under a separate task. For PERF-001, add a check of actual simulation speed after the measurement instrument is implemented.
- **Evidence limits:** simulated failures do not replace all physical network scenarios; do not present browser-exposed memory metrics as total RAM/VRAM. Keep folder-permission revocation, visual comparison and target-hardware checks in the manual queue if reliable automation is unavailable.
- **Result:** separate report with version, environment, workload, method, PASS/FAIL/SKIP and reasons for skips. Repeated success does not close defects without checking their scenarios; unavailable conditions remain unverified.

## PERF-001 Show Actual Simulation Speed

- **Status:** user request from 12 October 2026; implementation and verification are deferred.
- **Problem:** selecting “4 days/s” shows the configured speed, but does not prove that the server advances the simulation at that rate. The user doubts the game reaches 4 days/s; slowdown has not yet been confirmed by measurement.
- **Requirement:** display configured speed separately from actual in-game days advanced per real second. Client FPS is not a substitute for simulation speed.
- **Future task boundaries:** measure authoritative game-time advancement against monotonic server-side real time; state the measurement window and data age. Distinguish pause, manual steps, speed changes and connection loss; do not present stale data as current. Client observation of snapshot arrival is not an exact measurement of server work. Agree on a specific contract before implementation.
- **Acceptance:** compare the display with an independent measurement of game days advanced over a real-time interval at speeds 1/2/4; check pause, speed switching and load. If behind, show the actual result while keeping the configured value visible. Numeric tolerances and measurement window are not yet defined.

## ECON-001 Unstable Application of Job Slot Count

- **Status:** open based on a manual observation on 12 October 2026; cause is unknown and no fix was made.
- **Observation:** changing technicians from 2 to 1 was not reflected in the calculation at first, then took effect: month 7 report shows WF 100 + 15 = 115, energy output 7.475, building upkeep 2, net flow 5.475, unemployed Workforce 100. When attempting to restore 2, the field shows 2, but the month 8 report still calculates one slot.
- **Unknown:** whether the new value was sent and accepted by the server before the calculation boundary. The input field alone does not confirm command application. Do not assume a delayed monthly report or server error without checking.
- **Evidence:** screenshots `codex-clipboard-37c3cf13-2d0b-4994-875f-3d9963ae6b39.png` and `codex-clipboard-4837970d-105c-4e57-be60-b723d6c6e829.png` in the manual-acceptance chat; game images are not copied into Git.
- **Recheck queue:** while paused, perform 2 → 1 → 2 using the “Вакансии” button; for each click, compare the sent value, command acknowledgement, server state and next monthly report. Check that input is preserved across UI refreshes. Do not report PASS while the scenario is unstable.

## MOVE-001 First Inter-System Order May Have Been Interrupted

- **Status:** open observation from a manual walkthrough on 12 October 2026; cause and reproducibility are unknown.
- **Observation:** on the first attempt to travel S01 → S02, the fleet moved several steps and stopped without a visible error; the screenshot shows it still in S01 at X=20/Z=−80, idle phase, no target. A repeated explicit “Переместить” command completed the trip successfully.
- **Unknown:** acknowledgement that the first inter-system order was sent and accepted was not recorded; a server failure is unproven. The later successful attempt does not close the original observation.
- **Queue:** on a separate request, review possible order submission, replacement and completion problems by comparing acknowledgements and state sequence. The user asked to retain this case for later review; investigation is currently deferred.

## Current reference-driven revision

Delivery e7fb05c failed user visual acceptance; factual results are recorded at f5600bb in M3-MANUAL-RESULTS. The new [comparison contract](M3-REFERENCE-COMPARISON.md) supersedes MAP-001's earlier galaxy no-redesign boundary: galaxy presentation and full-viewport UI now join system fidelity work. CAM-001 remains open specifically for the confirmed no-selection galaxy pivot failure after pan. Selected-object and no-selection system rotation were user PASS within that run; moving-fleet rotation was not tested. Brief background-tab offline status remains an unconfirmed observation, not a proven transport defect. No automated PASS closes user visual acceptance.

## CAM-001 Camera Rotation Center Remains the Camera Focus Point

- **Status:** confirmed manual-acceptance defect; fix implemented and passed automated checks, user recheck is pending. [Report](reports/m3-camera-validation.md).
- **User observation:** “Rotation is around the camera focus point”; after clarification, confirmed: “behavior does not change depending on the selected object.” Simple selection does not move the rotation center to the object; the requirement is not met without separately clicking “Центр на выборе”. Behavior without a selection has not been checked separately.
- **Requirement:** rotate around the selected object; with no selection, rotate around the center of the current galaxy/system; see M3-CLIENT-INTERACTIONS.md. An arbitrary focus point after panning is not a substitute for this center.
- **Recheck:** pan the camera, click a planet, and confirm it is selected on the right; press Alt+LMB without clicking “Центр на выборе”. Change the object and repeat; clear selection and check the system/galaxy center. If the focus matches the required center, rotation around the focus itself is not a defect.
- **Closure:** confirm both views follow the rule when selecting, changing and clearing selection, without pan/zoom regressions. Investigate the cause separately; the presence of focus/OrbitControls alone does not prove it.

## MAP-001 Star System View Is Significantly Distorted

- **Priority:** priority fix under the user's direct instruction of 12 October 2026.
- **Status:** open; fix is authorized in the M3 visual stage, repeat visual acceptance has not passed.
- **Version:** manual test of delivery `971c410` (M3 runtime `4f5bcc6`), Windows/Google Chrome, local server. Exact Chrome version and viewport parameters were not recorded.
- **Reproduction:** import local Stellaris 4.5.2, connect two players and an authorized observer, start the world paused; open “Система 1” by double-clicking it in the outliner.
- **Observed result:** the user confirmed that the system map is significantly distorted compared with the reference. In the submitted screenshot, the station visually dominates the small planet and the star looks like a flat disk; relative scale and composition need comparison. These are image observations, not an established technical cause.
- **Boundary:** the user is currently satisfied with the galaxy map; redesigning it is outside this defect. This is not full acceptance of the galaxy, performance or game orders.
- **Evidence:** screenshot `codex-clipboard-3f6eaeed-41bb-47c4-a9e6-9ace54a43ce8.png` in manual-acceptance chat `01a12d73-b37b-7080-943c-684cc080b1f6`; direct user clarification: “Карта галактики пока устраивает. Карта системы значительно искажена”. Game asset images are not copied into Git.

### Recommended Diagnosis and Fix Boundaries

Compare visual model/layer scales, transforms, system composition, camera/projection and materials against Stellaris 4.5.2 primary sources. If evidence is insufficient, send a specific question to the reference chat; leave unknowns pending. Until the cause is investigated, do not assume models are damaged or assign new numeric scales by guesswork.

Fix the system presentation while preserving agreed game coordinates, speed and navigation. If the fix requires changes to those contracts, agree on them separately first. Approximate M3 materials are allowed, but do not by themselves resolve the user's report of significant distortion.

### Closure Criteria

1. Cause and confirmed reference settings are recorded; changes are limited to the system view and necessary dependencies.
2. Relative size/placement of the star, planet, station and three corvettes, model readability, camera and object selection are checked again; no layer gaps/disappearances.
3. No regression in galaxy map, orders or view transitions; available automated checks pass.
4. The user rechecks the corrected system and confirms the visual result. Until then, the defect remains open and movement checks from the current walkthrough are not reported as passed.
