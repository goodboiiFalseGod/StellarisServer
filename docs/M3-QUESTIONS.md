# M3 — Answers and Remaining Questions

Main user answers have been moved to DECISIONS and the [import](M3-LOCAL-ASSET-IMPORT.md)/[slice](M3-GRAPHICAL-SLICE.md) contracts. Game M3 is not yet implemented; the status of individual subcontracts is listed below.

## Answers to the Initial List

| Item / ID | User decision |
|---|---|
| 1 / I01 | One PC, localhost, two Google Chrome clients and an observer |
| 2 / I02 | Stellaris 4.5.2; required files must match, reject on mismatch; unused DLC is not required |
| 3 / I03 | No persistent cache. Do not add IndexedDB/Cache API/disk-derived assets; reuse current-page CPU/GPU data in memory |
| 4 / I04 | After access is lost, the current session continues using loaded assets; new reads/session require access and source verification |
| 5 / G01 | Mammalian set: corvette/star/continental planet/station/minimal icons are sufficient for testing; Arkship visually later |
| 6 / G02 | Static shape and approximate materials are acceptable; no hidden stubs or geometry distortion |
| 7 / G03 | Fixed map of 16 systems, two players, each with a colony/station/fleet of three corvettes |
| 8 / G04 | In-system movement is required immediately. Details of the earlier simplification proposal are not considered approved |
| 9 / G05 | Known map, public spatial projection, private economy; negative test for hidden objects |
| 10 / G06 | Russian UI with similar layout, without pixel-perfect matching |
| 11 / G07 | Exact days and monthly economy; M3 has no saves for now, restart destroys the world |
| 12 / G08 | Current 5700X/32 GB/RTX 4070, 1080p, 60 FPS, stress test of 200 ships p95 ≤33.3 ms, import ≤60 s. Persistent-cache target ≤10 s was withdrawn due to answer 3 |

## Agreed Movement/Calendar Clarifications

The user answered “I agree with the proposals” to all three clarifications. They are closed; the numeric profile was then separately accepted in N01 below. Project-wide reference is 4.5.2 (D44); movement parameter sources are in M3-TIME-AND-MOVEMENT (D45).

| ID | Topic | Accepted M3 decision |
|---|---|---|
| M01 | In-system movement | Move to a point in the plane at constant speed, without acceleration or turn limits; fleet position is server-authoritative, ships are placed visually, and physical formation of individual ships is not simulated. One active order; a new order replaces the old one; stop halts at the current position |
| M02 | Inter-system travel | Included in M3: approach exit → preparation → hyperlane transition → appear at the next system's entry. No teleport to the exit; stop/replacement during transition takes effect after arrival. Numeric sources are in M3-TIME-AND-MOVEMENT; agreed profile is N01 below |
| M03 | Calendar | Starts 2200.01.01, 12 months of 30 days; speeds of 1/2/4 in-game days per real second. Day step while paused; economy runs once at the month boundary. M3 laboratory pacing, not a vanilla-balance promise |

## N01 — Technical Movement Profile

Research of local 4.5.2 confirms an unmodified corvette base of 7.2 units/day and 15 days of Hyper Drive I preparation. The hyperlane transition-duration formula and exit-placement algorithm were not recovered from text. Do not interpret HYPERDRIVE_INTERSTELLAR_TRAVEL_SPEED=1.0 as “one-day travel”.

Agreed on 12 October 2026 by the user's answer “Yes, suitable for M3”: [profile](M3-WORLD-FIXTURE.md) with local radius 150, exits/entries at radius 140, speed 7.2 units/day, 15-day preparation and a 5-day transition; winddown=0 per source definitions. Topology is a 4×4 grid with 24 hyperlanes. Radii/positions and fixed 5 days are a technical fixture, **not vanilla values**. N01 is closed; the numeric block on movement implementation is lifted.

## Engineering Subcontracts

The [manifest/reader](M3-ASSET-MANIFEST.md), [time/phases](M3-TIME-AND-MOVEMENT.md) and [spatial protocol](M3-SPATIAL-PROTOCOL.md) were recorded before dependent code. The limited BGRA8 reader is implemented and checked ([report](M3-IMPORT-VALIDATION.md)); other subcontracts are not yet implemented. Visual gate for station/star/planet/icons and rigid skin adapter remain ahead. Exact client interactions/interpolation are also specified before client code.

Q01 is closed project-wide: 4.5.2; source cleanliness and full DLC profile remain separate questions. For Q05, M3 boundaries are closed: required dependencies, no persistent cache and localhost. For Q09/Q10, only the M3 test environment is settled, not minimum hardware/first-game scale. Full import and LAN remain out of scope; N01 is closed only within M3. [Remaining disputed questions and checks](M3-DISPUTES.md) are tracked separately from accepted answers.
