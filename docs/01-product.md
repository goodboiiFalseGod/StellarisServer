# Product Scope and Boundaries

## Purpose

A private strategy game for a group of players. A custom server executes a compatible subset of Stellaris text rules, and a custom browser client displays the state each player is allowed to see. Performance and support for vanilla definitions take priority over reproducing the original engine's internal algorithms exactly.

A game is designed for 10–20 players and 200–600 systems. The balance target is to determine a winner within 4–5 hours, over a game horizon of roughly 50–60 years. The end date is configurable; there is no fixed ending at year 60. Victory is based on the submitted system of periodic points and achievements described in the [separate specification](11-victory-and-scoring.md). The end-date setting, final tie-breaker and development speed have not yet been agreed.

## Target Product

- Lobby, selection from a limited set of empire templates, readiness, game start and reconnection.
- Pause and game-speed controls for the authorized host, including transfer of host authority.
- Full visibility for observers and intel-limited visibility for players.
- Pop Groups/Workforce economy, construction, research, development and expansion.
- Fleets, ship design, combat, wars, conquest and peace.
- A limited set of traditions, events and player interactions.
- Saves, manual recovery, command log and a foundation for replay/rollback.
- Agreed AFK, kick, ban and surrender behavior.

## Outside the First Release

General-purpose AI, NPC empires, crises, every unique origin and DLC mechanic, full event compatibility, original save files, Workshop, accounts, multiple concurrent games and a general-purpose ECS implementation.

These are first-release limits, not a prohibition on extending the architecture.

## Versions and Deviations

The project targets Stellaris 4.5.2 (D44). Each release profile records the DLC set and required files; unused DLC is not required for M3. Updating the installation does not change the target version automatically. Any change to a formula, behavior or effect timing must be recorded in the deviation register and confirmed by the user.

Product readiness is determined by completed acceptance criteria and measurements, not by the number of parsed files or announced features.
