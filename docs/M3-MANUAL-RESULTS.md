# M3 manual acceptance log

## Intermediate 9fc45e5 — manual review ended, 13 October 2026

Reviewed candidate: `9fc45e5a8981876e9537cc481d004f0032c96e1c`. The user reported completing the requested version/clean-status/build step; no terminal screenshot independently records the hash for this run. Health protocol 2 / mode m3 was confirmed. The user started with two players, reached the galaxy, then connected and approved an observer after start. Explicit preset selection was not recorded; do not infer a preset from resulting numbers. Exact Chrome version/viewport/hardware were not measured. **This was partial economy/fleet/navigation review, not full construction-stage acceptance.**

### Bounded factual results

| Check | Result and limits |
|---|---|
| Observer admission after start | User confirmed host approval, map, general/player views and unavailable gameplay orders. |
| Observer camera retention | User confirmed position/orientation/zoom preserved through Player 1 → Player 2 → General Overview after changing the camera. This is functional confirmation, not observer UI visual acceptance. |
| Own-colony presentation | Screenshot shows an imported hydroponics image and explicit unresolved/unavailable construction. Jobs tab exposes technician/miner/farmer vacancy fields initially at 2. No enabled placement, completion or economic-effect acceptance is inferred. |
| Technician 2 → 1 | Screenshot at 2200.02.20 shows energy stock 1006 and +6/month. Expanded received economic snapshot says technicians assigned 1, miners/farmers assigned 2. This confirms application across a month boundary; Workforce 100 was not exposed by this UI and was not directly verified. The generic accepted-command text was already visible earlier, so screenshots alone cannot tie it to the new command. |
| Unsent focused numeric draft across another month | **Not performed.** User declined this minor manual scenario as suitable for automated coverage. Earlier automated evidence is separate, not a manual PASS. |
| Own-fleet outliner double-click | User confirmed opening its system and focusing the fleet. |
| Native-style local map order | User confirmed right-clicking a free system point produced route/destination and moved the selected own fleet after time resumed. |
| System-exit map order | User confirmed visible approach/preparation/arrival from a right-click exit command. No exact timing, phase samples or cross-client numerical positions were collected. |
| Automatic near/far system detail | User confirmed automatic detail changes and readable labels/icons/routes during zoom. This does not establish automatic system-to-galaxy navigation or complete visual fidelity. |

### Direct next-revision feedback

- Remove the legacy exact-coordinate fleet command UI; retain native-style map click control. This request does not remove authoritative coordinate mechanics.
- The user again sees no expected observer UI changes; original observer country-overview/outliner presentation is not visually accepted despite functional perspective/camera checks.
- Planet appearance still does not match the reference. No new specific planet cause or numeric scale correction is established by this statement.
- Sufficient zoom-out from system must automatically return to galaxy; the user explicitly rejected reciprocal automatic system entry on galaxy zoom-in. Entering a system remains explicit. Transition threshold is unspecified; preserve confirmed detail/readability behavior.

The human explicitly ended the current review and authorized forwarding these remarks to integrator `01a12d71-f1d2-7e31-b692-caaeebd67ace`; the message was sent. No more manual checks requested. Visual acceptance remains open. Shared BACKLOG/DECISIONS/contracts are integrator-owned and unchanged by this record; no production code or tests changed.

### Local screenshot evidence

Original PNGs were copied byte-for-byte from `D:/Temp` to the canonical Git-ignored `content-local/references/` archive and source/archive SHA256 matched. Existing differing files would not be overwritten; no image bytes are committed or uploaded. These are prototype screenshots, not original-game references; unrelated browser chrome is not relevant evidence.

| Local archive filename | Evidence | Bytes | SHA256 |
|---|---|---:|---|
| `codex-clipboard-94369de4-948f-4fbb-a578-7cd6c36cca69.png` | Hydroponics image and explicit construction refusal | 933835 | `66615FA0EE3288C044B69F08B057AF19D5F587581C759812F159E169EF484DEE` |
| `codex-clipboard-0f362420-85c6-41f6-877c-31cae06521e1.png` | Jobs fields initially 2/2/2 | 933250 | `3B9DF8FB847416C825EDB7CAE13923E4CE309B7EC9036CC5163E8B98E219FCAB` |
| `codex-clipboard-83adb8ce-8cdb-492d-937f-236d56516a04.png` | Technician input 1; generic command confirmation | 932598 | `C1B015EDF3DDDAC081AC0EEA340BE50E4621D114210D7FDF582D3354033B6041` |
| `codex-clipboard-e30e0453-c4d7-44eb-9f0f-005f9585c736.png` | 2200.02.20, energy 1006 and +6/month | 2073916 | `C76F7339AD43D5DCACB8BD4C9A78B3CA5916DDF0CC967000DA7B49910D8684C6` |
| `codex-clipboard-b69398fa-c44a-4268-b911-e14b6535e1c9.png` | Received vacancy snapshot: 1/2/2 | 2015375 | `A0C5D1C012F8DB91E8760754E4523361DC39CEECA52D30A51F13031E90D69D43` |

## Delivery caadc6d — ongoing user acceptance, 12 October 2026

### End-of-session reference evidence received

The user supplied seven original-game reference images and explanatory text. Store only these evidence identifiers in Git; do not copy game images/assets into the repository. Their game version was not independently verified from these crops. The observer-outliner reference follow-up is fulfilled; earlier pending notes describe the state before this submission.

| Image filename in the acceptance chat | Visible reference / user explanation |
|---|---|
| `codex-clipboard-7fa6ffcb-2e07-4fee-9671-8d4d3d3f3231.png` | Fleet movement: segmented green route/arrow ending at a circular destination marker, with a fleet badge near the ships. A still image does not establish animation timing or order semantics. |
| `codex-clipboard-31cf058e-519a-4713-910b-5076fdf29cc7.png` | Fleet panel: action icons, identity/status, strength/count, leader area, current movement and estimated arrival, individual ship rows. This is a presentation reference, not authorization to implement every depicted mechanic/content type. |
| `codex-clipboard-c819f99a-9b6b-45fd-8536-bab51d77db29.png` | Observer general country overview: country identities, summary indicators and a way to select a country. The prior absence of a personal owned-object outliner in general overview does not prohibit this country overview. Indicator meanings must not be invented from icons. |
| `codex-clipboard-1bbcea15-2d02-4794-a58c-9ae87aaaa026.png` | Selected-country outliner grouped into categories. User states the country/observer selection remains accessible as a button after selecting a country. |
| `codex-clipboard-c1bd2410-dabd-4c24-957c-1e9cd236651e.png` | System exit marker: green triangular marker and adjacent destination-system name. Exact click/hover behavior is not established by the image. |
| `codex-clipboard-85c501d5-4cda-446f-9ef1-effba5ffcc1c.png` | User identifies the inner system boundary: fleets may cross it, while exits toward other systems lie along it. System overview also shows orbital rings, object badges and labels. |
| `codex-clipboard-cd39b0e2-0004-4ba7-b997-2141f2467ad9.png` | User identifies a separate outer system boundary beyond which manual movement is unavailable. This is a reported original-game observation, not a measured radius or proven native collision rule. |

User proposal, pending design/reference review: allow automatically fighting fleets to move beyond the outer boundary for kiting. Do not treat this as accepted native behavior, a finalized combat exception, or permission to expand the current implementation into combat. Limits, return behavior, interaction with manual orders and navigation remain unspecified.

### Remaining session results and window requirement

- Collapsible panels work individually, but cannot both stay open. User clarified that connection is excluded; other future windows should be usable concurrently, with only one window per category. Categories will be discussed separately; do not invent them.
- Stress → galaxy → system return passed by user confirmation.
- Player 2 F5/reimport/reconnect passed by user confirmation: same participant, no duplicate, current date and personal outliner restored.
- These functional confirmations do not close visual fidelity acceptance. New reference criteria belong in the integrator-owned contracts/backlog before dependent implementation.

The user reported completing the version/build and health steps, then importing and connecting two players and an observer in three Chrome tabs without problems. The user confirmed the guided approval/start check: personal owned fleet/colony outliners for players, no personal outliner in the observer's general overview, and a paused start at 2200.01.01. PASS is limited to this reported scenario; observer player-POV switching and visual fidelity are not yet accepted.

New direct user requirement: near and far views must each have an appropriate amount of displayed information and switch automatically as the camera zooms in or out, rather than requiring a manual switch. Exact information sets, transition thresholds and behavior await the design contract; this is a requirement, not an implemented or tested PASS. Shared-contract/backlog integration belongs to the integrator.

Reference follow-up requested by the user: ask the user to provide the original observer outliner view before assessing that presentation against the reference. This follow-up is pending; no reference image has been received for it in this step.

### Continued caadc6d session: confirmed results and requested changes

- Observer player selector: user confirmed target player's resources/outliner change while orders stay unavailable; return to general overview removes the personal outliner while retaining the map and read-only restrictions. These checks do not establish full fog-of-war/native visibility fidelity.
- Camera: user confirmed galaxy rotation around its physical center without selection, rotation around a selected system after pan without a jump, rotation around a selected planet after pan, and Escape clearing selection while retaining system view followed by rotation around the system center. These are bounded manual confirmations; moving-fleet rotation and other unperformed cases remain unverified.
- Coordinate input: user confirmed entering X=20 / Z=-80 preserves camera position and fleet selection. No command execution result is inferred from this input-only check.
- Usability issue: selecting a system on the galaxy map is very difficult; the user requests easier clicking. Root cause and exact picking behavior are not established. Improve selection accessibility while preserving discrimination between nearby objects; design details remain pending.
- New requirement: double-clicking an object in the outliner should open that object in its system view. Cross-system/in-transit cases and focus behavior require an explicit contract before implementation.
- New requirement: implement fleet control through native-style map clicks, with exact gestures and command semantics verified against the reference rather than inferred here.
- New requirement: show visible system boundaries and transitions/exits to other systems as in the reference. Exact appearance and interaction await reference comparison; no change to authoritative navigation mechanics is implied.
- User will provide reference screenshots at the end of this testing session, including the previously requested observer outliner view. Do not request them again mid-session.

These requirements are recorded for integrator-owned backlog/contract synchronization, not as implemented features or acceptance PASS. No production code was changed by manual acceptance.

### Continued functional checks in the same session

- Economy privacy: user confirmed player 2 sees only their own economy; the observer sees the selected player's economy without vacancy-editing access.
- Confirmed observer camera defect: changing the observed player resets the camera; the user separately confirmed that returning to general overview also resets it. User requirement: switching observer perspective must preserve the current view and camera position, orientation and zoom. Visibility-dependent unavailable-view cases require explicit contract treatment rather than an invented fallback. Cause is unknown; implementation is not started. Keep this defect open for integrator-owned backlog synchronization.

- Local fleet command X=20 / Z=-80: user confirmed acceptance and movement after daily steps.
- Stop during movement: user confirmed the fleet remains in place after two more daily steps.
- Neighboring-system command: user confirmed arrival and stop after running at the selected 4 days/s setting. Actual simulation throughput was not measured; PERF-001 is not accepted by this check.
- Cross-client state: user confirmed the fleet is in the same system and position in both player clients and the observer client. This does not close MOVE-001's earlier intermittent first-order observation.
- Technician vacancies 2 → 1: user confirmed a new accepted command and base Workforce 100 in the next monthly report.
- Technician vacancies 1 → 2: user confirmed base Workforce returned to 200 in the following monthly report after the guided apply/confirmation step. This sequence passed once; ECON-001's earlier intermittent behavior is not closed without investigation/reproduction coverage.
- Additional user requests: improve fleet-control UI and correctly represent fleet movement against the reference. Exact presentation criteria remain to be established from the end-of-session references; neither feature is accepted by the functional movement checks above.

## Visual delivery e7fb05c — user acceptance, 12 October 2026

The user verified checkout `e7fb05cb1c856c86cf716b9a25ea876ef8b4b0d4`, a clean working tree, Release build with 0 warnings and 0 errors, and health protocol 2 / mode m3. Testing took place in the user's Chrome; exact browser version and calibrated viewport were not recorded.

**Visual acceptance: FAIL.** The user rejected the system view and interface appearance. The star looks volumetric, but relative object sizes remain wrong and a large unusual aura surrounds it. Screenshot evidence: `codex-clipboard-dd72af7f-f2aa-4098-98af-a91c65cf884c.png` in this acceptance chat; no game screenshot is copied into Git. MAP-001 remains open. Additional references/display rules offered by the user are pending; no new fidelity criteria or root cause are inferred. Automated PASS evidence remains in the QA reports and does not supersede this rejection.

| Manual scenario | User result / limits |
|---|---|
| Two players, observer approval before start, paused start | User confirmed completion of the guided setup and expected state. |
| Selected planet then star: Alt rotation after pan, without focus button | PASS by user confirmation; pivot changes with selection and gesture starts without a jump. |
| No selection in system: Alt rotation after pan | PASS by user confirmation. |
| No selection in galaxy: Alt rotation after pan | FAIL: user reports rotation around a far edge point rather than the physical galaxy center. Screenshot `codex-clipboard-cac1e4de-d474-4055-8c66-1d2a49dca821.png` confirms the selection card says nothing is selected. CAM-001 remains open. |
| Escape with selection | Repeat removed selection correctly. Earlier immediate return to galaxy is an unconfirmed observation, not a proven defect. |
| Own fleet retained when selecting destination or foreign fleet | PASS by user confirmation; viewing the foreign fleet does not make it the command fleet. |
| Observer fleet controls | PASS by user confirmation for both fleets; controls unavailable. |
| Window narrowed approximately by half and restored | PASS for the reported panel/button accessibility and absence of overlap; this is not acceptance of interface appearance. |
| Setup/economy collapsed and reopened | PASS: connection, command confirmations and camera controls remain accessible. |
| Hint and FPS/p95 lines at narrow/restored width | PASS by user confirmation: readable without overlap. No performance acceptance inferred. |
| Stress view → galaxy → system | PASS by user confirmation: stress instances removed and normal objects/selection/camera controls restored. |
| F5, reimport, reconnect without clearing login | PASS by user confirmation: same participant, no duplicate, same game, no remaining stale-state warning. |
| Rotation around a moving fleet | Not tested: the user stopped the manual run before performing the proposed scenario. |

Additional observation: player 2 briefly appeared offline while its tab was in the background; switching to that tab restored online status. Cause and reproducibility are unknown; tab optimization is only a hypothesis. Preserve for backlog triage without treating it as a confirmed transport defect.

**Current run stopped at the user's request.** Do not infer PASS for unperformed checks or begin fixes. Common backlog/checklist integration remains owned by the integrator; this section records factual user results only. Historical results below apply to their original run and limits.

**Run status:** by the user's decision of October 12, 2026, the remaining manual tests are deferred until the next development stage. Automated checks are planned in TEST-001 ([backlog](BACKLOG.md)); the results below remain valid within their limits, and full acceptance is not complete.

Manual acceptance chat: `01a12d73-b37b-7080-943c-684cc080b1f6`. The user reported these results from checks in Chrome; the agent did not perform the actions. Date: October 12, 2026. The exact Chrome version and current runtime commit were not recorded again.

| Scenario | Result | Basis and limits |
|---|---|---|
| Player tab active while paused for 45 seconds | PASS, user-reported | For the scenario with an unchanged date, preserved connection, and no warning, the user replied “Всё в порядке” (“Everything is fine”). |
| Player tab in background while paused for 60 seconds, then return | PASS, user-reported | For the scenario of preserved or automatically restored connection with no lingering warning or duplicate player, the user replied «Да. всё также в порядке» (“Yes, everything is still fine”). Connection preservation and reconnection were not distinguished separately. |
| 10 cycles galaxy → system → test 200 corvettes, then return to system | Preliminary-check PASS, user-reported | The user confirmed no issues with freezes, missing objects, or leftover test corvettes. The known system distortion was excluded from this check and remains open. This does not replace 100 switches with resource, FPS/p95, and RAM/VRAM monitoring; measurements remain queued. |
| G18: stop and restart server, old login, clear and log in again with folder reselected | PASS, user-reported | For the scenario of losing the previous game, rejecting the old login, and successfully creating a new session, the user replied “Всё сработало” (“Everything worked”). |

These results do not check actual tab freezing, TCP blackhole, lost command acknowledgments, or a physically slow network; those remain in a separate checklist queue. Open defects are in the [backlog](BACKLOG.md). Other scenarios are not declared passed here.
