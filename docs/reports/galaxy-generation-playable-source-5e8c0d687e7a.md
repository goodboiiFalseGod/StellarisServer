# Lobby/settings → galaxy generation → playable world handoff

Date: 2026-10-13. This is an API and stage contract for a prototype; shared integration was not performed. Sources were read-only. Primary `outputs` were not modified.

## 1. Goal and scope of the playable prototype

`Playable` means that downstream simulation receives a self-contained deterministic world snapshot in which:

1. a connected navigable system graph exists;
2. every available system has completed semantic initialization state, not an empty stub;
3. at least one playable country exists with a home system, capital colony, owner/species, and starting objects;
4. the home colony has valid population/economy anchors: class, size, districts/buildings/zones, or an explicitly declared simplified economy projection;
5. a starting fleet/ship or equivalent playable actor is created and bound to a country/system;
6. all IDs and references resolve; there are no dangling topology/body/ownership links;
7. resolved settings, seed bundle, content snapshot identity, coverage result, and approximation flags are passed through;
8. the world passes typed `ValidateForPlay` before handoff.

Geometric map without initializer content and player capital is called `STRUCTURAL_GALAXY`, not `PLAYABLE_WORLD`. A home planet without script/startup state is called `STARTABLE_SKELETON`, not full vanilla generation.

## 2. Proposed public API

```text
ResolveRequest(LobbySettings, ContentProfile, CapabilitySet)
    -> ResolvedGenerationRequest | ValidationFailure[]

GenerateWorld(ResolvedGenerationRequest, SeedBundle)
    -> GenerationResult {
         status: COMPLETE | COMPLETE_WITH_APPROXIMATIONS | FAILED,
         world?: WorldSnapshot,
         stageLedger,
         diagnostics,
         coverageResult
       }

ValidateForPlay(WorldSnapshot, PlayabilityProfile)
    -> PlayabilityReport

Handoff(WorldSnapshot, PlayabilityReport)
    -> WorldHandoffEnvelope
```

`ResolveRequest` applies defaults and ranges before generation. `GenerateWorld` does not accept raw UI strings. `Handoff` is allowed only when `PlayabilityReport.accepted=true`; typed failure preserves stage/definition/source/operator context.

## 3. Typed lobby settings

### 3.1 Identity and mode

| Field | Type | Default/source | Validation and semantics |
|---|---|---|---|
| `contractVersion` | nonempty string | current generator contract | Required for replay and migration. |
| `contentSnapshotId` | SHA-256/manifest ID | current resolved vanilla snapshot | Must refer to an immutable manifest; latest inventory hashes all 2,261 semantic files. |
| `dlcProfile` | ordered enabled IDs + descriptor hashes | explicit active profile | Visibility and script gates are determined from the resolved profile; unknown DLC ID is a failure. |
| `playabilityProfile` | `STRUCTURAL`, `MINIMAL_PLAYABLE`, `VANILLA_COMPLETE` | caller explicit | Default for product entry is `VANILLA_COMPLETE`; prototype preset must not be enabled silently. |
| `approximationPolicy` | set of named allowed deviations | empty | Unnamed deviations are prohibited. |

### 3.2 Geometry and topology

| Field | Type | Default/source | Validation and mapping |
|---|---|---|---|
| `galaxySize` | enum `tiny|small|medium|large|huge` or typed custom scenario ID | scenario `medium` marked default | ID must resolve to exactly one setup scenario. |
| `galaxyShape` | shape definition ID | UI/profile choice | Must be present in resolved `supports_shape` of selected scenario. No fallback to elliptical. |
| `numStars` | positive int, resolved-only | scenario | Not directly user-set for vanilla sizes. Must equal resolved scenario unless custom scenario mode. |
| `galaxyRadius` | positive fixed(100000), resolved-only | scenario | Must fit coordinate/fixed intermediate domain. |
| `hyperlaneDensity` | fixed multiplier | scenario `num_hyperlanes_default=1.0` | Must lie in scenario range; all vanilla sizes use `[0.5,3.0]`. Maps directly to pruning `h`; it never supplies a seed. |
| `maxHyperlaneDistance` | positive fixed distance, resolved-only | scenario, vanilla `50` | Consumed by pruning and partition traversal; caller cannot silently enlarge it. |
| `clusteredStarts` | bool/typed placement enum | lobby `clustered`/`player_locations` | Must map to a supported placement policy. Multiplayer and single-player exposure differ. |
| `advancedStartsNearPlayer` | bool | lobby default catalog | Preserved as start-placement input. |
| `relaxIterations` | nonnegative int, resolved define | vanilla `1` | Custom override only under custom contract. |
| `centerDummyPoints` | nonnegative int, resolved define | vanilla `256` | Dummy sites never become systems or M13 topology points. |

Scenario-owned cluster, partition and nebula fields are copied into the resolved request as typed records, not flattened UI guesses:

```text
clusterSpec { method, value, max, radius, distanceFromCore }
homePartition { minSystems, maxSystems, minBridges, maxBridges, method }
openPartition { minSystems, maxSystems, minBridges, maxBridges, method }
nebulaSpec { count, radius, minCenterDistance }
```

### 3.3 Empire and content sliders

| Field | Type | Default/source | Validation and mapping |
|---|---|---|---|
| `numEmpires` | nonnegative int | scenario `num_empire_default` | Within scenario `num_empires` range and feasible for selected shape/start policy. |
| `numAdvancedEmpires` | nonnegative int | scenario `advanced_empire_default` | `<=numEmpires`; unsupported advanced startup is failure unless caller explicitly sets zero. |
| `numFallenEmpires` | nonnegative int | scenario default | `<=fallen_empire_max`; requires fallen content capability when nonzero. |
| `numMarauderEmpires` | nonnegative int | scenario default | `<=marauder_empire_max`; requires Apocalypse or nomad capability/gate. |
| `numNomadEmpires` | nonnegative int | scenario default | `<=nomad_empire_max`; requires nomad DLC capability/gate. |
| `colonizablePlanetOdds` | nonnegative fixed multiplier | scenario `colonizable_planet_odds` | Validate against resolved setting catalog; no hard-coded UI range if catalog does not supply it. |
| `primitiveOdds` | nonnegative fixed multiplier | scenario `primitive_odds` | Drives primitive/pre-FTL initializer use. Zero explicitly disables random branch, not prescripted content. |
| `numGuaranteedColonies` | nonnegative int | resolved lobby default | Requires guaranteed-colony operator support when positive. |
| `resourceAbundance` | nonnegative fixed multiplier | resolved lobby default | Must be visible to deposit/resource effects and preserved in world metadata. |
| `playerEmpires` | ordered empire descriptors | caller/prescripted selection | At least one for playable profile; every origin/authority/civic/species/name/home initializer reference must resolve. |
| `aiEmpireDesigns` | ordered descriptors or generation policy | resolved catalog | Count must satisfy `numEmpires`; database order is semantic. |
| `disabledPrecursors` | set of precursor IDs | empty/resolved lobby | Each ID must resolve; precursor stage must honor set. |

### 3.4 Gateways, wormholes and DLC-dependent options

| Field | Type | Default/source | Validation and stage ownership |
|---|---|---|---|
| `numGateways` | int | scenario default `1`, range `0..5` in vanilla scenarios | Applied through `abandoned_gateways_01` generic initializer scaling. Nonzero requires initializer/effect/megastructure support. |
| `numWormholePairs` | int | scenario default `1`, range `0..5` | Applied by post-generator `game_start.31`, not native topology. Nonzero requires `random_system`, distance, spawn and link effects. |
| `lGateEnabled` | bool | resolved lobby default | Visible only with Distant Stars; true requires L-cluster/event capability. |
| `caravaneersEnabled` | bool | resolved lobby default | Visible only with MegaCorp; true requires corresponding initializer/event capability. |
| DLC scaling fields | typed fixed/int by token | resolved lobby defaults | Cosmic storms, voidworms, cutholoids and later DLC options must remain in metadata and be executed by their owning later stage; unsupported non-neutral value is failure. |

### 3.5 Non-generation settings preserved for runtime

Difficulty, aggressiveness, technology/tradition cost, growth/logistics multipliers, crisis selection/strength, mid/end/victory dates, ironman and similar fields do not choose initial geometry directly, but the world handoff must preserve them in `runtimeRules`. Dropping them would create a map that cannot start with the requested lobby rules.

## 4. Vanilla scenario defaults

Defaults are resolved from the selected scenario, never copied from `medium` globally:

| Size | Stars | Radius | Empires | Advanced | Fallen | Marauder | Nomad | Nebulae | Hyperlanes | Gateways | Wormhole pairs | Max lane |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| tiny | 200 | 200 | 3 | 0 | 0 | 1 | 0 | 2 | 1.0 | 1 | 1 | 50 |
| small | 400 | 300 | 6 | 1 | 1 | 1 | 1 | 3 | 1.0 | 1 | 1 | 50 |
| medium | 600 | 400 | 9 | 2 | 2 | 2 | 2 | 6 | 1.0 | 1 | 1 | 50 |
| large | 800 | 450 | 12 | 3 | 3 | 2 | 2 | 8 | 1.0 | 1 | 1 | 50 |
| huge | 1000 | 450 | 15 | 4 | 4 | 3 | 3 | 10 | 1.0 | 1 | 1 | 50 |

Maxima remain separate fields. For example tiny fallen/marauder/nomad maxima are `1/1/2`, while huge are `6/3/10`. Resolution validates defaults and user values against the exact selected scenario record.

Shape support is also scenario-specific. Tiny supports only `elliptical`, `spiral_2`, `ring`, `bar`; medium supports nine standard shapes. `spiral_6` exists in the shape catalog but is not exposed by the five vanilla size scenarios and therefore needs a typed custom-scenario opt-in.

## 5. Validation phases

### 5.1 Resolve-time validation

1. Parse settings with exact types; reject unknown fields unless an extension namespace owns them.
2. Resolve scenario, shape, content snapshot, DLC profile and empire descriptors.
3. Apply each default from its owning resolved record.
4. Check scenario ranges, shape support, empire count relations and DLC visibility.
5. Build capability requirements from non-neutral settings and selected empire/content branches.
6. Reject every required unsupported family with `{code,field,requiredCapability,source}`.
7. Freeze canonical resolved settings and compute `resolvedSettingsHash`.

### 5.2 Generation-time validation

Every stage checks its input/output invariants and commits atomically. Required failures include coordinate retry exhaustion, invalid Triangle endpoints, disconnected required topology, insufficient start systems, initializer selection exhaustion, missing content reference, unsupported trigger/effect and uncreated requested special content.

### 5.3 Handoff validation

`ValidateForPlay` checks unique IDs, all references, graph symmetry/connectivity, parent/orbit acyclicity, owner/capital/home consistency, at least one playable country, valid capital economy anchor, navigable starting system, initial actor and no unresolved required stage. `COMPLETE_WITH_APPROXIMATIONS` is acceptable only if the selected playability profile permits every named approximation.

## 6. Seed bundle policy

Public lobby accepts one optional human-facing `userSeed` plus optional advanced overrides. Seed is never inferred from hyperlane density or another setting.

Canonical `SeedBundleV1`:

```text
userSeedBytes: immutable canonical UTF-8 or supplied 64-bit integer
galaxySpecifiedSeed: uint31
pruningSeedA: uint31
pruningSeedB: uint31
scriptRng: { seedBase:uint32, counter:uint32 }
nameSeed: uint64
contentSeed: uint64
derivationVersion: "SeedBundleV1-SHA256"
```

Policy:

1. If no user seed is supplied, generate it once through the host entropy provider and return/display it before generation. The resolved seed becomes part of the request.
2. Derive each field with SHA-256 over a canonical envelope containing derivation version, user seed bytes and an ASCII domain label (`galaxy`, `prune-a`, `prune-b`, `script-0`, `script-1`, `names`, `content`). Content snapshot and settings remain separate replay inputs, so changing an unrelated lobby rule does not silently redefine the seed itself.
3. Decode digest words in one declared byte order; mask uint31 fields with `0x7fffffff`. Zero is valid.
4. Feed `galaxySpecifiedSeed` into the exact §10.1 native local-state transform. Use pruning seeds only in their corresponding passes. Keep script/names/content streams separate so adding name content does not perturb topology.
5. Serialize the entire resolved bundle in `WorldSnapshot`. Replay consumes stored fields directly, not a re-derived bundle under a newer version.
6. Advanced callers may supply explicit bundle fields; mixed override/derived mode must record provenance per field.

This policy is deterministic and supports vanilla distributions without claiming the hidden native lobby/global RNG history.

## 7. Stage priority to minimally playable world

### P0 — request integrity and structural galaxy

1. Resolved lobby/content/DLC/capability snapshot.
2. Seed bundle and stage ledger.
3. Coordinates, collision/retry, materialized systems and relaxation.
4. `BuildTopologyCandidates`, A/B pruning and symmetric hyperlane adjacency.
5. Connected-component validation and explicit repair policy if approximation is enabled.
6. Stable system IDs, coordinates and graph export.

P0 produces `STRUCTURAL_GALAXY`, not playable world.

### P1 — minimum playable semantic world

1. Initializer catalog parser/linker and minimum trigger/value operators used by selected ordinary/home initializers.
2. Star/system initializer executor, planet/moon/orbit materialization and neighbor batch semantics.
3. Planet post-create content required by selected home and ordinary systems.
4. Start-system assignment for the selected player descriptors.
5. Capital colony/startup branch for the selected origin/authority/civic/species combination.
6. Required starting districts/buildings/zones/pops/resources/technology.
7. Starting fleet/ship/station effects required by selected start.
8. World validation and immutable handoff envelope.

P1 may claim `MINIMAL_PLAYABLE` only for a declared supported content slice. The UI must constrain selections to that slice before generation; encountering other vanilla content during an allowed run is a coverage failure.

### P2 — vanilla completeness

1. All 360 initializer definitions plus 88 top-level `@` variables and their transitive content closure.
2. All player origins/authorities/civics/species branches.
3. Fallen empires, marauders, nomads, pre-FTL, hostile/guardian/crisis and DLC-special systems.
4. Full deposits/blockers/modifiers/anomalies/districts and script effects.
5. Nebulae, resource regions, precursors, gateways, wormholes and enabled DLC post-generation stages.
6. Full on-action/event startup graph and runtime rule preservation.
7. Coverage report with every used definition `parsed,linked,conditionEvaluable,executable,verified`.

## 8. Minimum playable preset

`MINIMAL_PLAYABLE` is an explicit product preset, not a fallback. A safe first preset may select:

- `tiny`, one of its supported shapes;
- `numEmpires=1`, `numAdvancedEmpires=0`, `numFallenEmpires=0`, `numMarauderEmpires=0`, `numNomadEmpires=0`;
- `primitiveOdds=0`;
- `numGateways=0`, `numWormholePairs=0`, `lGateEnabled=false`, `caravaneersEnabled=false`;
- one specifically verified ordinary player empire/start family;
- only verified ordinary/misc initializer definitions, with selection pool filtered at resolve time and fully disclosed in coverage metadata.

The preset may not silently take the scenario defaults for unsupported families and then skip their stages. Its resolved settings must visibly contain the zeros/disabled flags above. It also may not claim “full vanilla support”; it is a bounded playable slice used to bring up the world handoff.

## 9. World output schema

```text
WorldSnapshot {
  metadata {
    contractVersion, generatorBuild, gameContentVersion,
    contentSnapshotId, resolvedSettingsHash,
    resolvedSettings, seedBundle,
    status, approximationFlags[], diagnosticsSummary,
    completedStages[], stageLedgerHash, coverageResultHash
  }
  runtimeRules { difficulty, dates, costs, growth, crisis, ironman, ... }
  systems[] {
    id, generationIndex, name/nameKey,
    positionFixed{x,y}, armId, groupId, partitionId,
    clusterIds[], starClassId, initializerId,
    flags[], nebulaId?, bodyIds[], adjacencyEdgeIds[]
  }
  hyperlanes[] { id, aSystemId, bSystemId, lengthFixed, materializationOrder, flags }
  bodies[] {
    id, systemId, parentBodyId?, kind,
    classId, name/nameKey, size,
    orbit{distance,angle}, ownerCountryId?,
    flags[], deposits[], blockers[], modifiers[], districts[], anomalyId?
  }
  countries[] {
    id, category, designId, authorityId, civicIds[], originId?,
    speciesIds[], ethicIds[], homeSystemId?, capitalBodyId?,
    ownedSystemIds[], colonyIds[], fleetIds[], resources, technologies[]
  }
  colonies[] { id, countryId, bodyId, pops[], buildings[], districts[], zones[], resources }
  fleets[] { id, ownerCountryId, systemId, shipIds[], role }
  ships[] { id, fleetId, designId, systemId, role }
  bypasses[] { id, typeId, endpointSystemIds[], linkedBypassId?, state }
  megastructures[] { id, typeId, systemId, bodyId?, state }
  regions[] { id, kind, memberSystemIds[], geometry?, flags[] }
  eventState { savedTargets, scheduledEvents, onActionsCompleted[], scriptRngContinuation }
  indexes { systemById, bodyById, countryById, topologyComponents }
}
```

Serialized indexes may be rebuilt by the consumer, but logical arrays and IDs are authoritative. Native allocator addresses/generations are not part of this API.

## 10. Handoff envelope

```text
WorldHandoffEnvelope {
  schemaVersion,
  worldHash,
  worldSnapshot,
  playabilityProfile,
  validationReport {
    accepted,
    invariantResults[],
    requiredCapabilities[],
    approximationFlags[],
    unsupportedContent[]
  },
  continuation {
    simulationDate,
    scriptRngState,
    pendingEvents,
    nextStableIds
  }
}
```

Consumer must verify `worldHash`, schema/contract compatibility and `accepted=true` before mounting the world. Unsupported content list must be empty for `VANILLA_COMPLETE`; for `MINIMAL_PLAYABLE` it may list out-of-profile definitions but none may be referenced by the generated snapshot.

## 11. Allowed approximations

Allowed only when named in policy and output metadata:

- own deterministic seed bundle instead of hidden native global state;
- own stable IDs instead of native allocator handles;
- Triangle `native-contract` instead of bit certification;
- small coordinate/FP differences within declared tolerance;
- a different deterministic pruning edge identity when connectivity, density constraints and fixed own pruning seeds are preserved;
- placeholder/localisation-independent display names in `MINIMAL_PLAYABLE`, while stable semantic name keys/references remain valid;
- simplified rebuildable caches and indexes;
- explicit zeroing of optional families in the bounded minimal preset;
- simplified economy projection only under a separate declared prototype profile and only if the consumer does not expect native economy simulation.

## 12. Forbidden silent omissions

The generator must fail or require an explicit approximation flag when it would otherwise:

1. coerce an unsupported shape or out-of-range setting;
2. replace missing content references with null/default definitions;
3. treat unknown trigger as false, unknown effect as no-op or failed weight as zero;
4. omit a requested empire, guaranteed colony, gateway, wormhole, precursor or DLC family;
5. create blank systems because initializer execution is unavailable;
6. skip `init_effect`, neighbor recursion, capital startup or on-action order;
7. merge local galaxy, pruning and script RNG streams;
8. materialize B pruning without cross-group counters;
9. accept a disconnected navigable graph or asymmetric adjacency;
10. create gateways/wormholes in a fabricated common native tail;
11. claim `PLAYABLE` without player capital/economy anchor/initial actor;
12. claim `VANILLA_COMPLETE` from parsed-only coverage.

## 13. Acceptance gates

The handoff contract is ready when:

1. all five vanilla size defaults and supported-shape matrices resolve identically from content;
2. invalid combinations produce stable typed errors;
3. identical request+seed+snapshot produces identical `worldHash`;
4. stage ledger proves no stage consumed another stream's seed;
5. topology is symmetric, connected as required and has no invalid endpoints;
6. selected initializer/start content is fully green in coverage state;
7. one minimal player start reaches a valid capital colony with initial actor;
8. round-trip serialize/deserialize preserves logical `worldHash`;
9. every approximation is present in both request policy and result metadata;
10. `VANILLA_COMPLETE` is impossible while any generation-eligible definition/operator in the selected vanilla+DLC profile is unsupported, even if a particular seeded run did not select it.

## 14. Source snapshot

Current authoritative inventory: `outputs/vanilla-galaxy-content-coverage-manifest-452.md`, SHA-256 `1D21767EBE69AAD356AD49FD2495EC8D881DA67233E8B8CC6294C683BF68137E` at audit time. It identifies Stellaris Cygnus 4.5.2 (9776), a 2,261-file semantic ledger hash `E0C75A299051E07D1C8C5C076D0E44358F4A650F966BA3499238D76106CC2CC0` and generation-family ledger hash `5A3F1295C4A56B2AFFBD9DB17F03CD398838691D9760667E35F9767C6F5DEB9F`.

Reverse control flow source: `outputs/native-generation-reverse-addendum-452.md`, SHA-256 `6CABA0D9358DF77147BF288FCB8DE6BD37BF0273D26687D8554E5D4D64E5B385` at audit time. The post-relax topology boundary is additionally fixed by `work/relaxed_to_m13_handoff.md` and the independent gap audit `work/stage_gap_audit.md`.
