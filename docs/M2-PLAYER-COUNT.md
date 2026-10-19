# Player Count in the Current Session

The current user clarification from 14 October 2026 replaces the two-player limits in doc17–19. It applies to the implemented economy session; it does not define empire placement in the future M3 spatial fixture.

- The lobby accepts any positive number of players, including a single player. Fixed limits of two slots and eight WebSocket connections are removed. Actual capacity depends on server resources; unlimited load is not promised. Message-size, command-rate, queue and connection-timeout limits remain.
- Each player receives a separate empire and planet with the previous starting settings. The economy is calculated for all empires; each player sees only their own planet, an approved observer sees all, and a waiting player sees none. The previous single observer slot remains.
- The first player is host. Host transfer is available to any other connected player, including the third and later players; transfer to self or an observer is rejected.
- The host can start when at least one player is present and every player is connected/ready. A missing or unready player blocks start; observers do not count. Changing a setting clears readiness only for its owner. Start leaves the session paused at month 0.
- After start, new players are rejected. Existing players return to their previous empire using their token; a disconnected empire continues its economy. Host disconnect does not transfer rights automatically.
- The client enables Start under the same conditions and shows the full participant list. Server rejection remains authoritative if state changes between snapshot and command.

Acceptance: session checks for 1, 2, 3, 10 and 20 players; rejection before all are ready and on disconnect; data isolation and calculation for all planets; reconnect and host transfer to the third/last player. Real WebSocket checks must connect more than eight participants simultaneously and complete start, economy tick, filtering and reconnect. Browser check confirms Start is available for one and three players. Existing economy and network checks pass. Capacity beyond the tested player counts is not established.
