# Intermediate playable review candidate — 13 October 2026

This is an optional intermediate review checkpoint for the existing economy, fleet controls and navigation. **It is not the completed construction/fleet/navigation stage.** Live construction is not available: source-derived startup/zone occurrences, required effect consumers, applicable modifier totals and live queue/work integration remain incomplete. Trusted ledger cancellation/progress/queue checks do not establish player construction. Work toward the full stage continues after this checkpoint; no new gameplay experimental branch is included.

## Verified stable checkout

Runtime merge commit `65b289cd075b456ed1bb67b04854dee9cb31292d`, stable checkout `D:/Projects/StellarisServer`, branch `feature/m3-static-visuals`. The merge preserved the previous stable manual-evidence commits through `2c4cc7d`. Stable tracked status was clean before and after verification. Production, tools and tests exactly match browser-tested integration `2208e98` (`git diff --exit-code 2208e98 HEAD -- src tools tests`); later integration changes are documentation. The final documentation handoff commit is supplied in the coordinating/manual-review chats.

Actual stable-checkout `dotnet build src/Stellaris.Server -c Release --nologo` passed with **0 warnings, 0 errors**. Started its built server on isolated loopback port 5193 against the licensed local 4.5.2 installation. `/health` returned `status: ok`, `protocol: 2`, `mode: m3`; manifest returned `m3-static-452-v7`, **149 required files**, digest `a2d7d42fc1dd8d9565a12017eb222b6e09509fa2a6528a145fc7dfa6b2367fa3`. Entry page, client/connection modules, actual `/m3-modules/` renderer/importer/planet-image routes, CSS and Three.js core/module/OrbitControls returned HTTP 200. Two exploratory incorrect probe paths returned 404; they are not client dependencies. No content-unavailable startup warning appeared.

Stopped only retained smoke process session 55715 with Ctrl+C; it exited 1, so graceful shutdown is not claimed. Subsequent port-5193 listener query was empty. No existing user server was stopped or reused. This smoke check did not create a game session, reimport browser assets or repeat the browser suite.

## Applicable evidence

- Reviewed economy correction `163cc112`, integrated `2208e98`: actual client tests **96 PASS, 0 FAIL** and Release build passed before this stable merge. [Independent assembled replay](combined-browser-validation.md#econ-001-assembled-fix-results--13-october-2026), plan `888439e`, result `d53bf04`: accepted technician change updates the next monthly report/HUD without manual blur; an unsent numeric draft survives the following month without an automatic vacancy command. Control player unchanged, observer received values read-only, reconnect clears retained draft. Seven screenshots and both archive copies match their manifest. Historical ECON-001 month-7/8 causality remains unresolved.
- [First movement replay](navigation-browser-validation.md#move-001-first-order-results--13-october-2026), plan `20abe46`, result `0a76f67`: first accepted S01 → S02 order progressed through approach, charging and transit to idle at S02 after 44 paused day steps. Owner/matching observer agreed throughout; B stayed idle in S16. No stop/replacement was sent. Numeric position was not exposed. This is bounded non-reproduction, not an explanation or global closure of historical MOVE-001. Earlier physical picking, zoom and outliner checks remain separately scoped in that report.
- [Construction machinery checks](construction-paid-cancellation-checks.md): integrated **50 PASS, 0 FAIL, 1 SKIP** (unavailable symlink creation, no real-content argument); M3 session owner checks **15 suites, 0 errors**. These are earlier checks on unchanged code, not rerun counts or live construction acceptance.

Each implementation received independent actual-diff/contract review. QA evidence received independent review by `/root/review_documentation_fix`; root separately inspected integrated reports/manifests. The stable build/HTTP smoke above is new; earlier scoped counts are carried forward because production identity was verified, not silently relabeled as fresh runs. Manual Chrome visual fidelity, native parity, target hardware FPS/RAM/VRAM, load and unavailable network/race cases remain unverified.

## Launch and bounded manual checklist

Use [the launch guide](../M3-RUN-AND-TEST.md) for prerequisites and independent browser tabs. Stop a previous server through its own terminal before rebuilding; restarting ends that unsaved session. From PowerShell:

```powershell
Set-Location -LiteralPath '..'
dotnet build src/Stellaris.Server -c Release
dotnet run --no-build --project src/Stellaris.Server -c Release -- --mode m3 --game-root 'C:\Path\To\Stellaris' --urls http://127.0.0.1:5089 --join-code M3-local-test-code
```

Use your own licensed installation path if different. Local browser dependencies are already present in the verified checkout; on a fresh checkout first use the frozen-lockfile install command in the launch guide. Open `http://127.0.0.1:5089/m3.html` in three separately created tabs, import the complete local v7 profile into each, connect two players and an observer, approve the observer, explicitly choose presets and ready/start on pause.

1. Open the own colony: actual imported hydroponics image and received jobs are visible; unsupported construction must remain visibly unavailable. This tests presentation, not building placement.
2. On pause, change technician 2 → 1 through Apply. Advance 30 days from start with the host. Expect one monthly report with technician Workforce 100 and energy output 6 in the unmodified Economic fixture. Leave an unsent numeric draft focused during another monthly boundary: report updates, draft survives and no command is sent automatically. Other player/observer views must not inherit the draft.
3. Select the own fleet, inspect another object, and verify the retained command-fleet indication. Send a local move, then separately send an intersystem move to a visible neighbor. Accepted route/phase must be shown; pending intent must not masquerade as arrival. Stop/replacement behavior follows the existing movement contract. The specific untouched S01 → S02 source fixture replay reached S02 after 44 steps; do not apply that duration to an arbitrary starting position.
4. Check outliner double-click focus, near/far zoom detail, readable labels, and read-only observer A/B/general perspectives. Reconnect requires readiness before control returns. Record the exact checkout commit and any differing result.

Human review is optional now and does not block remaining implementation. No complete-stage readiness, construction economic effect, native formula proof or manual PASS is claimed.
