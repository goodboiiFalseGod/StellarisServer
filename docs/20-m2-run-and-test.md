# M2 — Launch and manual check

The player count is not limited to two: one or more independent tabs can be opened. The example below uses two players; launch is available once all players are ready, and a disconnected player also blocks start. [Contract](M2-PLAYER-COUNT.md).

Full manual acceptance: [plan with expected results](22-m2-manual-acceptance.md), [questions and feedback form](23-m2-test-questions.md). The instructions below cover launch and a short main scenario.

.NET SDK 8 and an installed copy of Stellaris 4.5.2 are required. The client only needs a browser; only the server needs the game installation. Paradox source files and assets are not served to clients.

Commands below use the canonical checkout `..` and can be run from any directory. For another copy or worktree, replace this prefix in every command with the absolute root of the intended repository; otherwise a different project version will launch. Replace `--game-root` with your 4.5.2 installation path as well. In PowerShell, copy only the lines inside the blocks, without the triple-backtick fence or the word `powershell`. The parameter `-p:BuildInParallel=false` avoids a silent parallel-build failure in the project chain on the tested .NET SDK 8.0.424.

## Simple manual launch on port 5080

Run this in a separate PowerShell window:

```powershell
dotnet run --project "..\src\Stellaris.Server\Stellaris.Server.csproj" -c Release -p:BuildInParallel=false -- --game-root "C:\Path\To\Stellaris" --urls http://127.0.0.1:5080
```

Keep this PowerShell window open throughout the manual check: the server runs there while the command is active. The server prints the address `http://127.0.0.1:5080` and a random access code. Open the address in two new tabs, enter different names and the same code. The first player is host. Opening two new tabs normally creates independent logins; do not duplicate an already connected tab, because the browser may copy its sessionStorage. To return, use the “Вернуться” button in the original tab.

For trusted LAN, replace `--urls http://127.0.0.1:5080` with `--urls http://0.0.0.0:5080`, then open `http://<LAN-IP-of-server>:5080` on another computer. The user configures network access and Windows Firewall permissions; rules are not changed automatically. HTTP is unencrypted: do not forward the port to the Internet. For LAN, use ordinary Chromium/Edge, not an isolated preview window.

## Manual scenario

1. In the first tab, select “Изобретательные” and “Энергосеть” and save. Leave settings empty in the second tab.
2. Optionally open a third tab as observer; the host must approve viewing before start. No planets are visible before approval.
3. Both players click “Готов”; the host starts. Month 0, paused, report not yet calculated.
4. The host clicks “Следующий месяц”: their technicians have W=200, B=30, energy output=14.95, building upkeep=2, and net flow=12.95. The second player's energy output is 12, with no building upkeep.
5. The host changes technician jobs to 1 and clicks “Применить”. The report is still from the previous month. On the next step: W=100, B=15, output=7.475; the second player is unaffected.
6. The observer switches between both planets; controls are unavailable. Regular players see only their own economy.
7. Disconnect the second player, step through months, then return using the same button/token. The empire and current month are preserved. After 60 real seconds, offline changes to AFK, including while paused.
8. Resume time and disconnect the host: months continue at the same speed. No new host is appointed automatically. Return the host and pause; transfer permissions to the second player and confirm the former host's permissions disappear.
9. Reloading the tab preserves the login; click “Вернуться”. Stopping the process with Ctrl+C ends the session. On a new launch, the old token is rejected: use “Очистить вход” and heed the warning, then enter the code and name again.

## Optional automated tests

Automated tests are not required for a simple manual launch. Run them only after the manual session has ended and server `5080` has been stopped with Ctrl+C: rebuilding while the server is running may fail because a DLL is in use.

```powershell
dotnet run --project "..\tests\Stellaris.Checks\Stellaris.Checks.csproj" -c Release -p:BuildInParallel=false -- --game-root "C:\Path\To\Stellaris"
dotnet run --project "..\tests\Stellaris.SessionChecks\Stellaris.SessionChecks.csproj" -c Release -p:BuildInParallel=false
```

The transport test occupies seats and changes the session, so do not point it at the manual session on `5080`. It needs a separate fresh server on `5088`. In a first additional PowerShell window, run the following and leave it running:

By default, the test connects 20 players and an observer. A different player count ≥2 may be specified as the last argument; a new server session is required for each repeated run.

```powershell
dotnet run --project "..\src\Stellaris.Server\Stellaris.Server.csproj" -c Release -p:BuildInParallel=false -- --game-root "C:\Path\To\Stellaris" --urls http://127.0.0.1:5088 --join-code M2-local-test-code
```

In a second additional PowerShell window, run:

```powershell
dotnet run --project "..\tests\Stellaris.ServerChecks\Stellaris.ServerChecks.csproj" -c Release -p:BuildInParallel=false -- http://127.0.0.1:5088 M2-local-test-code
```

After checking, stop only the test server on `5088` by pressing Ctrl+C in its window. The manual server on `5080` must already have been stopped as instructed above before automated tests begin. A normal launch without `--join-code` generates a new random code; do not use the fixed test code for a private LAN session.
