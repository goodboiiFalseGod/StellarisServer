# Latency Synchronization Test

Base: `4a55ff9` from the chat “Исправить недостатки UI” (“Fix UI deficiencies”). Separate experimental branch; mechanics and server timeouts are preserved.

## Contract

- The M3 client diagnostic panel selects RTT: 0, 200, 500, 1500, 5000 or 40000 ms through URL parameter `latency`. If absent, use normal WebSocket. Unknown values are explicitly marked as errors; a valid mode must be selected to start.
- RTT is added round-trip delay on top of the real network; each direction receives half the RTT with independent ±20% jitter. Message order is preserved in each direction, as with WebSocket/TCP.
- Delay all WebSocket application messages, including join, welcome, snapshots, ACK and ping/pong. HTTP, local asset import, TCP open/close and server simulation are not slowed. This tests delivery latency; it is not a bandwidth or packet-loss emulator.
- Setting applies per tab and takes effect after reload. The panel shows the mode and warns that reload clears local import. Do not put tokens/access codes in the URL.
- Each direction's queue is capped at 512 messages and 16 MiB UTF-8; overflow closes the connection with explicit diagnostics. Closing cancels delayed delivery and releases queues. No message from an old connection is delivered to a new one.
- The 40000 ms profile intentionally checks existing join/liveness timeouts. Do not increase timeouts to make the test pass.

## Acceptance

Automated: fixed clock/randomness checks both directions, FIFO under jitter, cancellation on close, queue limits, zero-latency mode and parameter validation. Existing connection/reconnect/client tests and Release build pass.

Live server: separate free loopback port, /health, page and modules accessible; real WebSocket join/snapshot/ping under delay. Check the panel UI in a browser. Manual game check: two tabs (0 and 500/1500), import own assets, ready/start, pause/speed, fleet order, construction/research queue, reconnect. ACK waiting and identical server state after delivery are expected; visual smoothness and full game acceptance are recorded separately from transport tests.
