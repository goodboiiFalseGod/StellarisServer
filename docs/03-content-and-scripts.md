# Stellaris Content and Scripts

## Source and Version

Read data from the external installation selected by the host. Build a manifest of the version, active DLC, source paths and hashes. Published manifests must not contain personal absolute paths. Do not import user mods by default.

Treat every DLC as a source of definitions and dependencies. Do not mechanically include every file found: investigate load order, DLC conditions and overrides for the pinned version.

## Processing

1. Parse lexical and syntactic structure while preserving order and repeated keys.
2. Resolve variables, inline scripts, scripted effects/triggers and definition references.
3. Validate context types, arguments and supported operations.
4. Build an internal representation for execution and dependency analysis.
5. Report coverage and the content enabled for a game.

Support layered sources as a foundation for future mod support. Do not claim priority or `replace_path` semantics are implemented until they have dedicated tests.

## Script VM

Preserve the selected version's semantics for ROOT, FROM, PREV, THIS, context chains and event targets. Contexts may be represented by EntityIds and registers; copying the original engine's object architecture is unnecessary.

Classify operations as reads, writes, collection traversal, RNG or deferred execution. A substantial native effect needs its own documented contract, not a placeholder stub.

An unknown operation in active content must produce a visible error with file, line and call chain. Excluded content must appear in a report with its reason and remain unavailable to players. Define an explicit checking policy for unreachable and conditional branches before implementing a compiler.

AI weights are not used by strategic AI. However, `is_ai` conditions must not be executed as constants until controller types are defined. Agree separately on the semantics of such flags.

## Acceptance

- Repeated keys and block order are preserved.
- Invalid references and incorrect scopes produce diagnostics.
- Script transitions, nested calls, deferred effects and RNG reproduce after recovery.
- Every operation used by an enabled scenario is supported.
- Reports distinguish syntax parsing, semantic support and verified in-game behavior.
