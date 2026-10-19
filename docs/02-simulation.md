# Server Simulation Architecture

## Server Responsibilities

The server is the sole authority for game state. It validates permissions and commands, runs the simulation, and builds the views each client is allowed to receive. Clients and controllers do not modify components directly.

Layers: content import → validated internal representation → Script VM and simulation systems → visibility projections → network transport. Networking, the browser and disk storage do not determine the order of game calculations.

## Data

- Stable EntityIds with protection against reusing IDs of deleted entities.
- Dense storage for hot numeric data and batched operations.
- Indexes for ownership, location, fleet membership and planet affiliation.
- Immutable content definitions kept separate from game state.
- Diplomatic and other relationships may use specialized tables; not every relationship needs to become an ECS entity.
- Population groups and Workforce allocation are modeled explicitly; a separate entity for every Pop is not required.

## Execution

Use fixed boundaries for game days and months. Agree on the exact phase order before implementing the economy and combat; subsystem lists in documents do not define the original Clausewitz order.

Incoming commands receive a server sequence number and execution tick. Structural changes are applied at defined points. Receiving the same command again must not repeat its effect.

Parallelize independent work with explicit dependencies. Result order, RNG and rounding must not depend on thread scheduling. Disk writes and network queues need size limits and overload handling; a slow client must not stall the simulation.

## Performance

Recompute derived data based on dependencies and changes, but first establish a correct baseline. Speed must not be gained by silently skipping required effects. Compiling repeated conditions and maintaining indexes matter more than using ECS in name only.

## Decisions Required Before Implementation

Confirm the .NET version, ECS library, number format, phase order, RNG management, thread budget and component read/write model. Evaluate ECS candidates against the economy, relationships and structural changes, not only an empty component loop.

Acceptance: identical command inputs produce identical checksums in single-threaded and multithreaded runs on the agreed test set; client rendering does not affect the result.
