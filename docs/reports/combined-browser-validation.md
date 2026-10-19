# Combined browser QA — 12 October 2026

## ECON-001 paused reproduction plan — 13 October 2026

Authorized read-only investigation on reviewed coordination `3c0c681` under the ordered fallback policy; dedicated QA worktree only. No production edits, new monitor, native formula research or MOVE-001 work. Restore actual accepted v7 two-player fixture on an owned isolated loopback server, record exact QA/build revision, templates and environment. Use the existing economy laboratory's historical “Вакансии” (“Jobs”) UI to explicitly apply technician counts 2 → 1 → 2 while paused. For each apply record the typed count, full command ID/ACK and source-backed payload binding; separately reopen an authoritative Jobs projection and inspect the next monthly report. Do not treat the edited input as projected authority, or an old report before settlement as a bug. Browser APIs do not expose WebSocket frame capture: distinguish code-bound payload reconstruction from actual wire evidence and leave that limitation explicit.

Advance to each next boundary with actual paused day-step controls, separately observed ACK/date, rather than wall-clock running/overshoot. Check unsent focused input across an independently acknowledged second-player state refresh where feasible; report exactly which state changed and do not claim all rebuild/lifecycle paths. Preserve farmer/miner counts and no laboratory trait/building changes. Capture bounded screenshots/hash manifest, warnings/errors, owned cleanup and independent report review. Historical month 7/8 evidence and earlier asymmetric monthly QA remain separate; non-reproduction cannot unconditionally close the intermittent observation. Results pending at plan commit.

Before the bounded focus-refresh extension: the first paused 2 → 1 → 2 sequence settled correctly in months 1/2/3. Inspection of `renderEconomy` shows it skips the entire economy panel whenever focus is anywhere inside it, including an Apply button. Check this distinct presentation risk without inferring a historical cause: transfer host within the disposable fixture to B, let A retain an accepted technician Apply button focus, and advance the next exact paused month through B's UI. Compare A's clock/resource HUD with its still-open report, then remove focus and observe fresh presentation. No production mutation or broader authority test; this extends only the requested input-preservation/state-refresh investigation.

## ECON-001 investigation results — 13 October 2026

QA `0a132a7`, merged reviewed production `3c0c681`; in-app browser 1280×720, no override, actual local 4.5.2 v7 149-file imports (323.0 MiB, 7.43 s / 6.91 s), two Economic players, no laboratory traits/buildings enabled, start paused 2200.01.01. Isolated server session 25787 on port 5093. No production edits, native mechanics research, broad suite or MOVE-001 work. [Local evidence manifest](../REFERENCE-ARCHIVE.md#econ-001-paused-investigation-evidence).

### Accepted vacancies and next reports

Each technician value was applied through the historical economy laboratory's “Вакансии” (“Jobs”) button. DOM command history supplied full IDs and accepted `OK` results. A separate newly opened colony Jobs tab confirmed received technician counts 2, 1, 2; its inputs were not edited. Miner/farmer counts remained 2. Source-backed payload reconstruction is `{type:'setJobs', commandId:<below>, planetId:<A's received economy planet.id>, job:'technician', count:<below>}` from client `m3-client.mjs:600–604`, connection `m3-connection.mjs:111–116,125–140`. Actual wire frames and the numeric planet ID are not exposed by the supported browser APIs/DOM and were not captured; do not present this reconstruction as frame evidence.

| Applied count / date | Accepted command ID | Next report / exact boundary | Technician WF / energy output / unemployed WF |
|---|---|---|---|
| 2 / 2200.01.01 | `81a6a2ec-94b0-4f62-932c-74d166c864dd` | Month 1 / 2200.02.01 | 200 / 12 / 0 |
| 1 / 2200.02.01 | `b6efff8d-ca92-4666-922a-7ba007f18b44` | Month 2 / 2200.03.01 | 100 / 6 / 100 |
| 2 / 2200.03.01 | `9e198b8e-4386-43fc-ac66-079c6c98d38b` | Month 3 / 2200.04.01 | 200 / 12 / 0 |

Each boundary used exactly 30 physical paused UI day-step clicks, each followed by fresh AX and DOM date/accepted-status observation. Final step IDs respectively `a87c12fa-aba8-49bb-bc13-0fdc56c91d48`, `f081e913-81cc-46ec-b658-d70662afd653`, `6a19a6a4-81ff-44f0-9595-03b99010f8b3`. Food/mineral monthly output stayed 12/8; bonus/job/building upkeep stayed zero in this laboratory fixture. A's month-3 balances were energy/food/minerals 1030/1036/1024, consistent with the three observed settlements. No wall-clock running/overshoot.

Immediately after count 1 was accepted, month-1 WF/output remained 200/12; immediately after count 2 was accepted, month-2 WF/output remained 100/6. These old reports before the next settlement are expected and are not server failures. Thus the exact paused 2 → 1 → 2 command/projection/next-report mismatch did **not** reproduce in this fresh fixture. Historical month 7/8 evidence, bonus/upkeep configuration and intermittent/network cases remain unresolved, not unconditionally closed.

Unsent-input check: A typed/focused 1 while its server count was still 2; B reapplied its own unchanged count 2 and received accepted ACK `d1cb9584`. A's focused input stayed 1 with no new A command. This is bounded preservation around an independently accepted B action, not proof that A's private economy key changed, nor full input-preservation coverage.

### Reproduced P2: focus guard freezes the entire economy report

Historical finding on `3c0c681`. The bounded assembled `2208e98` replay below resolves this reproduced presentation path, not every historical ECON-001 observation.

Reproduction on the same fixture, after month 3: A transferred host to B through the actual setup control (accepted `6d9b241b`), reopened economy, changed technician 2 → 1 and clicked “Вакансии” (“Jobs”). Accepted command `3199e694-22e2-4d9a-9af9-871eaa677e3c`; A retained DOM focus on `<button data-owner="1">Вакансии</button>`. Do not move A's focus. B closed its economy panel and performed 30 individually observed accepted paused day steps (2200.04.02 → 2200.05.01, final ACK `6db99377-2a9b-44b8-be01-91c6537cd580`).

Actual A presentation at 2200.05.01: HUD energy balance 1036 / +6 per month, food 1048, minerals 1032; technician input 1 and accepted ACK retained. Yet the open economy panel still claimed three closed months, report month 3, technician WF 200, energy output/net 12 and unemployed WF 0. Expected: report month 4, WF 100, output/net 6 and unemployed WF 100, without erasing an edited input. Screenshot `econ13-focused-stale-month3.jpg` captures this concrete mismatch.

Recovery: A closed the economy panel, B performed one more accepted paused step, A reopened economy at 2200.05.02. The panel now showed four closed months, report 4, WF 100, energy output/net 6, unemployed WF 100 and technician count 1, with no new vacancy command. Screenshot `econ13-blurred-fresh-month4.jpg` corroborates the received report after focus release/next snapshot.

Code boundary/cause: `src/Stellaris.Server/wwwroot/m3-client.mjs:589–594`, especially line 592, returns from all of `renderEconomy()` whenever any descendant of `economy-content` is focused. The Apply button remains focused after acceptance, so the guard intended to protect editing also prevents monthly report/date/allocation refresh. `renderState:587` still refreshes the independent resource HUD. This explains the newly reproduced stale panel, **not** an unproven cause of the original month-7/8 observation or ignored server commands. Risk: users may interpret a successfully applied vacancy change as an ineffective simulation change. Recommended owned UI fix: preserve dirty/focused input and its value narrowly while allowing authoritative report/month/allocation updates, including after Apply-button focus; cover both unsent editing and post-ACK settlement. Do not “fix” this by deleting input preservation or changing economy arithmetic/settlement timing. No implementation by QA.

### Limits, cleanup and review

No raw wire payload capture, numeric planet-ID observation, original month-7/8 configuration replay, broad privacy/reconnect/race/throughput/native acceptance or claim that all ECON-001 causes are closed. One selector lookup initially timed out because reopening the colony resets to Infrastructure; selecting the visible Jobs tab resolved it. One tool-script syntax error was rejected before execution and corrected; neither was a product failure. Both browser warning/error log queries returned empty.

Nine screenshots were copied byte-for-byte to both ignored reference archives; sizes/SHA256 verified against originals. Closed only owned tabs 23/24, preserving original tabs 1/2/3. Stopped retained server handle 25787; exit 1 with connection-ending warnings, not a graceful-shutdown claim. Final elevated query found no port-5093 listener; owner verified both archives Git-ignored. `git diff --check` passed. Independent `/root/review_documentation_fix`, unchanged settings, reviewed actual report/manifest/source and screenshots: no actionable findings, all 27 original/archive files independently matched recorded sizes/hashes. Reviewer's canonical Git-ignore query was permission-blocked (owner's elevated check passed); file-hash verification was available. No stable-main readiness claim.

## ECON-001 assembled fix replay plan

Reviewed production `2208e98`, UI result `163cc112`, scoped BACKLOG contract. Replay only the reproduced Apply-focus defect, draft/report reconciliation and accessible invalidation contexts on the exact assembled runtime in dedicated QA; no production edits or broad suites. Actual v7 two-player plus approved observer fixture, owned server/port, explicit templates, paused day steps. Transfer host to B so A can keep its laboratory panel open across exact monthly boundaries. Apply A technician 2 → 1 with button focus; verify next report/month/allocation/HUD agree without manual blur. Type an unsent dirty numeric draft and keep focus across B's next month; verify raw value/focus survive while report refreshes and no vacancy command is automatically created. Check unchanged focused-input received-count adoption only if a real supported UI can independently change that same owner value; do not inject state. Check disconnect/reconnect and readiness invalidation through actual UI, plus observer A/B/general read-only presentation and no draft resurrection. Owner/planet/job removal and native number-input caret may be unavailable; identify exact limits rather than synthetic browser PASS. Preserve historical month 7/8 uncertainty and earlier passing evidence. Archive screenshots/hashes, inspect errors/warnings, clean owned resources and independently review actual report before results commit; do not start MOVE-001 yet. Results pending at plan commit.

## ECON-001 assembled fix results — 13 October 2026

Pre-execution plan `888439e`; runtime source is reviewed production `2208e98`, UI result `163cc112`. No production edits or broad suites. After an unrelated completed turn, the browser inventory showed only pre-existing tabs 1–3: temporary fixture tabs 25–27 had automatically closed, losing tab-scoped login state. Retained QA server session 17395 was stopped (exit 1, no graceful-shutdown claim); only the disposable port-5093 server was restarted as session 84750. This necessary recovery is not continuity of the lost month-3 fixture.

Fresh owned tabs 28/29/30: QA Fix A, QA Fix B and approved QA Fix Observer; real 149-file v7 imports, 323.0 MiB (A 7.31 s, B 6.93 s; observer completion confirmed, duration not recorded). Explicit Economic presets, no laboratory traits/buildings; normal 1280×720 in-app viewport. Start paused 2200.01.01. Exactly 90 accepted UI day steps reached 2200.04.01, final ACK `bb165125-4cca-47d7-b0c7-efffd2f71f5c`; A transferred host to B (accepted `3020c3e4`). Baseline counts 2/2/2, report 3, WF 200 per job, balances energy/food/minerals 1036/1036/1024. Unlike the original investigation this fixture did not repeat the earlier 2 → 1 → 2 history; the energy baseline therefore differs intentionally.

### Report refresh and unsent draft

A applied technician 1 at 2200.04.01: accepted command `ce0724b3-c4cc-4381-9f11-c0ff19042d93`; DOM binding owner 1, planet 1, job technician, received value 1. The accepted snapshot rebuilt the row and focus became BODY automatically. No manual blur, close/reopen or A action occurred before the next report. This is not a claim that Apply-button focus itself survives reconciliation.

| Boundary advanced by B | Final accepted day-step ID | A received report / WF / energy output-net / unemployed WF | A HUD energy / food / minerals |
|---|---|---|---|
| 30 steps → 2200.05.01 | `5b7f309d-facf-45b8-bcde-836a38240754` | Month 4 / 100 / 6 / 100 | 1042 (+6) / 1048 (+12) / 1032 (+8) |
| 30 steps → 2200.06.01 | `a338589e-e806-4d72-a9b8-99a832ace757` | Month 5 / 100 / 6 / 100 | 1048 (+6) / 1060 (+12) / 1040 (+8) |

Every step was followed by fresh AX/DOM accepted-status/date observation. Before the second boundary A filled technician with unsent raw string `3`, while received value remained `1`. Before and after all 30 steps the same owner/planet/job numeric input was focused, enabled and value `3` / received `1`; month/allocation/report refreshed correctly. A's latest command remained the original accepted vacancy ID throughout: no automatic vacancy command appeared. Native number-input selectionStart was null, so native caret/selection preservation is not claimed. Farmer/miner received counts stayed 2. B control showed month 5, counts 2/2/2, WF 200 each, energy net 12, no unemployment and balances 1060/1060/1040.

### Context and connection invalidation

Observer selected A (accepted `ffcbc521-4104-409f-bdde-be0aac6ca61a`): month 5, received technician 1, owner 1 / planet 1, all numeric controls disabled; A's local draft 3 did not leak. B POV (accepted `35ffc9e3-8cff-4578-986a-3bad03551ab1`) replaced these with owner 2 / planet 4, counts 2/2/2 and B's report/HUD, all inputs disabled. General selection was initially pending; only after accepted `8bc987d4-f9be-4c82-ab95-31e91ba216e3` did identity show general overview and outliner hidden=true. No observer mutation is claimed or attempted.

A closed economy, opened setup and disconnected through the actual control. Existing economy DOM reconciled to received 1/2/2, all inputs disabled, draft 3 absent. Reconnect of the same visible login/profile received accepted readiness `7661e12a-0a91-4d92-a08d-3d20557b92b2`; reopened economy showed owner 1 / planet 1, enabled values 1/2/2 and unchanged month-5 report, with no draft resurrection. Closing the modal necessarily moved numeric focus before disconnect: this proves invalidation of retained draft DOM, not abrupt network loss while the numeric input remains focused. No separate asset-readiness-loss window was observed; transport/readiness are coupled in this reconnect case.

### Evidence, limits and retained fixture

All three warning/error console queries were empty. Seven screenshots were archived byte-for-byte in both ignored reference directories, hashes/sizes matching originals; [manifest](../REFERENCE-ARCHIVE.md#econ-001-assembled-fix-evidence). Actual WebSocket frames were not captured. No supported second controller can independently change A's same-owner received count while its numeric input remains focused; unchanged-input adoption of a *different* received count was therefore not browser-tested. Independent owner changes, player-to-observer role transition, planet/job removal, focused abrupt disconnect, standalone readiness loss/restoration and native caret remain unverified; no injected state or synthetic browser PASS. Existing focused automated-owner results are not counted as a new QA run.

The reproduced stale-report path and dirty-draft/month refresh pass this bounded assembled replay. Historical month 7/8 causality, bonuses/upkeep configuration and intermittent network cases remain unresolved. No native arithmetic, full ECON closure, construction acceptance or stable-main readiness follows. Per subsequent ordered dispatch, owned server 84750 and marked tabs 28–30 are deliberately retained for MOVE-001 after independent review/commit of this evidence; final owned cleanup belongs to that follow-up, not falsely reported here. Pre-existing tabs/processes were preserved.

Independent evidence reviewer `/root/review_documentation_fix`, unchanged settings, found no actionable issues: all 21 original/archive files match recorded sizes/hashes; representative captures support refresh, draft preservation, read-only observer values and reconnect reset. Source attribution and explicit limitations were checked. `git diff --check` passed; no broad tests rerun.

Later follow-up: retained ECON resources were cleaned up after the bounded MOVE replay; [exact owned cleanup](navigation-browser-validation.md#evidence-and-cleanup).

Scope: disposable QA server on loopback port 5093 and three QA-created in-app browser tabs. Stable main and the user's session were not changed. No production edits or live-construction acceptance. Earlier independent harness results are in [planet acceptance validation](planet-acceptance-validation.md).

## Tested checkpoints

- `9cde21f`: independently reviewed real public-session snapshots through browser navigation adapters, four checks passed; build had zero warnings/errors. This test commit precedes the later browser integration merges and is not represented as a new run against them.
- `7b1aec9`: actual served integration with renderer, authoritative exits, module packaging and source-bound hydroponics image.
- `29923ea`: earlier actual served integration including Jobs controls from `3aa02c7`.
- `7a32e16`: fresh two-player monthly follow-up, merging reviewed integration `0100e13` into QA. No observer retest or broad suite run.

## Confirmed browser observations

Each of two players and one approved observer imported the actual 133-file profile locally. Import observations were approximately 7–9 seconds and 322.8 MiB decoded; no source assets were uploaded or added to Git. Explicit Economic/Scientific templates were selected and the session started paused. These are in-app browser observations, not physical Chrome or target-hardware performance acceptance.

On `7b1aec9`, each player's outliner contained its own colony/fleet/station. General observer hid the outliner; selected player perspectives showed the corresponding private presentation while remaining read-only. Actual outliner double-click opened/focused a colony or fleet. Both players' planet dialogs displayed a decoded 78×78 hydroponics image with the exact supported building image key and a local blob URL. Catalog eligibility/quotes remained unresolved: this was not an enabled construction order.

Actual right-click on received exit 2 produced an accepted approaching-exit order and visible green route. Inspection of a colony retained the owned command fleet. Stop was accepted and removed the route; a subsequent local-point order was accepted and displayed local geometry. Wheel input changed automatic detail between far and near. Observer A → B → general retained the same system/camera composition while replacing private content; the foreign route disappeared and general overview hid the outliner. No warning/error browser console entries were observed during this phase.

On `29923ea`, A changed farmer vacancies from 2 to 3 through the Jobs tab and received an accepted response. The subsequent input and expanded job detail showed 3. B's corresponding value remained 2. Observer A showed 3 with all three vacancy inputs and buttons disabled. This establishes confirmed projection and read-only controls, not monthly settlement or native employment arithmetic.

## Served module checks on `7b1aec9`

All returned HTTP 200 with JavaScript MIME type and UTF-8 bytes matching the tracked module:

| Module under `/m3-modules/` | Bytes | SHA256 |
|---|---:|---|
| `m3-navigation.mjs` | 7184 | `29E7178BA228908F6F313F9D957C88E8518BF85C180E38EC0A31F7CFF7CB16F1` |
| `m3-planet-assets.mjs` | 8575 | `7D436ACF8081E8221C0BB888C4C8A839F1C5A863F64F1B538A02588A1C9904BF` |
| `m3-planet-images.mjs` | 6515 | `A23D31180A5AAB6DDA48881FF5CE0F2984B8B8FA73DB2154F2101CE55D599351` |

## Earlier interrupted monthly check

The attempted Close colony → speed 4 → Continue batch did not establish running state. The next Pause lookup failed; subsequent DOM inspection showed the colony dialog open, Continue still displayed and date 2200.01.01. Therefore no elapsed-month or economic-effect result is claimed. This sequence alone does not establish a reproducible production defect: each intermediate modal/control transition was not independently confirmed.

After the turn ended, the three unmarked QA-created tabs were automatically closed. Recovery inventory contained only the three pre-existing unreachable tabs; those were preserved. The original imported browser state could not be recovered for a single-action reproduction or monthly comparison. A fresh isolated session is required for that outstanding check. No production workaround was attempted.

## Completed fresh monthly follow-up on `7a32e16`

Two new disposable players imported the real 133-file profile in 7.40 and 8.31 seconds. Both explicitly selected the Economic template, with no laboratory traits/buildings enabled, and started paused on 2200.01.01 with Population/Workforce 600 and initial balances 1000 each. Only A's Jobs tab was changed: technician vacancies 2 → 1, then farmer vacancies 2 → 3, each accepted and subsequently confirmed; miners remained 2. This frees actual Workforce rather than assuming extra vacancies create it. B received no job command.

Individually confirmed UI steps: Close colony made `dialog.open` false; speed 4 was accepted and displayed while the dialog stayed closed; Continue was accepted and changed to Pause; a separate observation showed date 2200.01.25; Pause was then accepted, returning to Continue at 2200.03.17. Browser/tool latency crossed two monthly boundaries rather than exactly one. Both authoritative economy dialogs reported two closed months and month-2 reports. The earlier apparent clock/modal failure did not reproduce with these separately confirmed transitions; no production defect or fix is claimed.

| Received month-2 value | A after Jobs changes | B unchanged control |
|---|---|---|
| Vacancies: technician / miner / farmer | 1 / 2 / 3 | 2 / 2 / 2 |
| Workforce: technician / miner / farmer | 100 / 200 / 300 | 200 / 200 / 200 |
| Monthly output and net: energy / food / minerals | 6 / 18 / 8 | 12 / 12 / 8 |
| Balances: energy / food / minerals | 1012 / 1036 / 1016 | 1024 / 1024 / 1016 |
| Unemployed Workforce | 0 | 0 |

All received job/building/total upkeep values were zero in this laboratory scenario. The balances match two received monthly net settlements from the initial 1000; B retained its vacancy/allocation baseline and did not inherit A's reallocation. This is bounded live browser → existing command → authoritative monthly report evidence, not native employment validation, construction economic effects or proof of unpublished template startup mechanics. Both tabs had no warning/error console entries. Saved both report screenshots; closed only these new QA tabs and stopped only the retained QA process session. As before, the process returned exit 1 with connection-ending warnings rather than a confirmed graceful shutdown; no listener remained on port 5093.

## Remaining limits

Other unverified conditions: actual browser arrival/transit geometry (covered only through received-session adapters), picking ties/drag discrimination, quantitative zoom hysteresis, import cancellation/BFCache/URL revocation, browser reconnect, hidden-system fixtures, native appearance and target GPU/FPS. No playable construction, debit/reservation/completion or month-boundary construction atomicity is established. Old-token warnings following deliberate QA server restarts are not evidence of a new gameplay/privacy defect.

## Assembled v7 browser follow-up on `1945c76`

Merged reviewed integration `ef78ee4` into the clean QA branch as `1945c76`; no production edits or duplicate asset handoff. This includes reviewed v7 images, backend v7 hydroponics binding guard, zone-contribution staging and independent version gate. Started only the disposable server with `dotnet run --project src/Stellaris.Server -c Release -- --mode m3 --game-root '<licensed local 4.5.2 installation>' --urls http://127.0.0.1:5093 --join-code qa-v7-image`. Two disposable player tabs were necessary to start the existing fixture; no observer/navigation/monthly test repeated. No game time advanced.

Actual 149-file local imports completed in 7.31 and 7.28 seconds, displayed 323.0 MiB and received accepted readiness. Both selected the Economic template and started paused at 2200.01.01. A's own colony displayed exact `building.building_hydroponics_farm` with a complete local blob image, natural dimensions 78×78, through the current backend guard. Catalog remained unresolved/disabled, not playable construction. The new thirteen images were CPU/source-checked in the independent gate, not exercised as received live catalog entries here.

One replacement import of all 149 files was cancelled while active. The UI explicitly reported the previous set retained. After dismissing the setup dialog with Escape, reopening the same colony showed a complete 78×78 hydroponics image with exactly the original accepted blob URL. Clear login then closed and emptied the planet dialog (`open=false`, zero images, empty content) and private resource presentation, with source-reselection status. Both tabs' warning/error console logs were empty. Direct blob-URL navigation was refused by browser policy; no workaround was attempted. Independent revoked-URL dereference, BFCache and cancellation at PNG/GPU staging remain unverified. This cancellation was early in the replacement, not proof of all late-stage rollback paths.

### Actionable UI finding: setup Close covered by feedback overlay

Historical finding on `1945c76`; the narrow real-v7 follow-up below closes this obstruction on `2a88e6d` only, not broader visual acceptance.

Reproduction in this 1280×720 in-app browser: start the paused fixture, open own colony, close it, open Connection/import/lobby, start and cancel a replacement file import, then click Close connection. The modal remained open; an isolated second semantic Close click also left `setup-dialog.open=true`. Read-only DOM evidence: Close button rect x=1103..1133, y=63.078125..91.078125; its center (1118, 77.078125) hit-tests to `p#command-feedback`, showing the accepted start feedback. Screenshot visibly places the feedback overlay over the modal header's right edge. Escape closes the modal. This is a concrete overlapping pointer target, not a claim that cancellation or v7 introduced the layout defect. Source inspection found the generic close handler correctly calls `dialog.close()`; no production workaround/fix was authored by QA. UI owner/integrator must correct the layout and request a narrow pointer-close/retained-profile recheck before closing this finding.

Evidence `combined-v7-close-obstructed.jpg`, SHA256 `23D17FF6740D268C05B0806E1F957C5921BB6F0030CCB93A20CF96322A3539A1`, is archived with image/cancel/clear-login screenshots in both ignored project reference directories. [Relative links and hashes](../REFERENCE-ARCHIVE.md#assembled-v7-browser-evidence). The before/after-cancel screenshots are byte-identical; that supports unchanged visible composition, while same-URL comparison above supplies handle evidence.

Closed only the two QA tabs and stopped only retained QA process session. The process returned exit 1 with connection-ending warnings, not a verified graceful shutdown; the subsequent listener check found no listener on port 5093. Stable main/user session untouched. No broad test run, full-stage acceptance or native/FPS claim follows.

## Close obstruction resolved — targeted real-v7 recheck on `2a88e6d`

Merged reviewed coordination HEAD `f3ffe95` into clean QA as `2a88e6d`, including reviewed UI fix `f26dd95b9d9687ebebaa7b5def58e475b75ec068` and category binding `239f6c3`. QA changed no production files. Started the same isolated server command above with join code `qa-close-recheck`, port 5093; two disposable player tabs only, normal 1280×720 viewport. No navigation, observer, monthly, category-binding or full-stage suite repeated.

Real 149-file v7 imports reached accepted readiness in 7.33 and 7.46 seconds, displaying 323.0 MiB. Explicit Economic templates selected; session remained paused at 2200.01.01. Own hydroponics image was complete, local blob-backed and 78×78 before the reproduction. Closed colony, opened setup, started one replacement import and cancelled while active; UI confirmed the old set retained.

Actual post-cancel DOM hit-test: feedback computed position `static`, bottom y=96; heading starts y=104. Close rect x=1088..1118, y=114..142; its center (1103,128) hit the Close button itself, not command feedback. Normal pointer click then yielded `setup-dialog.open=false`, feedback parent BODY and exactly one `.network-status`. No Escape workaround used in this recheck. Reopened own colony: exact `building.building_hydroponics_farm` image still complete at 78×78, with the exact same accepted blob URL. Both browser warning/error logs empty. This closes the reported desktop setup-Close obstruction for the original real-v7 cancellation scenario; late-stage cancellation, independent URL revocation, mobile real import and general visual acceptance remain outside this check.

Two screenshots and hashes are in [the reference archive](../REFERENCE-ARCHIVE.md#real-v7-close-fix-recheck); originals and both ignored project copies match. Closed only the two newly created QA tabs. Stopped only retained QA process session: exit 1 with connection-ending warnings, no graceful shutdown claim; subsequent port-5093 listener check was empty. No production change, stable-main readiness or playable construction acceptance.

## Local visual evidence

Screenshots remain outside Git under the local visualization folder for this chat, date `2026/10/12`, ID `01a12d6b-8e8f-7723-a719-e75c3b77eb14`:

- `combined-planet-image.jpg`: actual imported hydroponics image in the planet dialog.
- `combined-observer-route.jpg`: observer A, focused fleet and received route.
- `combined-observer-b-camera.jpg`: retained camera/system after switching to B.
- `combined-observer-general-camera.jpg`: retained camera/system, general view without outliner.

The earlier Jobs change had only DOM/accepted-response evidence. The fresh follow-up adds `combined-monthly-owner.jpg` and `combined-monthly-control.jpg`. All six screenshots have now been copied byte-for-byte into both the ignored QA archive and canonical project's `content-local/references/` archive, with hashes verified against the visualization originals. Relative links, sizes and SHA256 are tracked in [reference archive](../REFERENCE-ARCHIVE.md#combined-browser-qa-evidence). No screenshot/game bytes are in Git or uploaded.

QA process cleanup was attempted only through its retained process session. That session returned exit code 1 with connection-ending warnings, so a graceful shutdown is not claimed. The subsequent `Get-NetTCPConnection` check found no listener on port 5093. No unrelated process was stopped. No broad suites were repeated for this report.
