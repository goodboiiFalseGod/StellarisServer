# Native range parser semantics for exceptional initializer fields

Target: Stellaris 4.5.2 (9776), `stellaris.exe` from the read-only Steam
installation used by the main reverse report. This note closes only four
parser questions: one-sided integer ranges, reversed integer ranges, the
`355..5` orbit-angle range, and duplicate body fields.

## Result

The parser does not normalize, reorder, clamp, or infer a missing endpoint.
A braced range is compiled through a fresh zero-initialized temporary:

```text
minValue = 0
maxValue = 0
visit every min/max assignment in source order
compiled.base = minValue
compiled.span = maxValue - minValue
```

`min` and `max` independently replace their corresponding temporary member.
Consequently:

| Source | Compiled `(base, span)` | Runtime domain |
|---|---:|---|
| `count = { min = 1 }` | `(1, -1)` | integer `1` or `0`, inclusive |
| `count = { min = 2 }` | `(2, -2)` | integer `2`, `1`, or `0`, inclusive |
| `size = { min = 15 max = 10 }` | `(15, -5)` | integer `15..10`, descending, inclusive |
| `orbit_angle = { min = 355 max = 5 }` | `(355.0, -350.0)` | fixed samples descend from `355` toward `5`; this is **not** the short circular arc through zero |

The last valid occurrence of `size`, `orbit_distance`, or `orbit_angle` in a
body descriptor wins. Every occurrence compiles a complete fresh range and
immediately replaces the same descriptor member. It does not merge with the
constructor default or with an earlier occurrence.

## Native parser evidence

The body-field callback is `0x1405060E0–0x140506B99`.

- `size`: `0x140506168` takes `descriptor+0x138` and calls integer-range
  parser `0x1404A7940`.
- `orbit_angle`: `0x140506339` takes `descriptor+0x150` and calls fixed-range
  parser `0x1403C8AE0`.
- `orbit_distance`: `0x14050634D` takes `descriptor+0x140` and calls the same
  fixed-range parser.
- `count`: `0x14050699F` takes `descriptor+0x0C` and calls integer-range
  parser `0x1404A7940`.

There is no `already seen` test in any of these branches. Each callback
passes the same final member address to the parser and returns. The generic
script visitor invokes callbacks in source order, as also required by the
already-proved interleaving of nested `planet`, `moon`, and `change_orbit`
records. Therefore a later occurrence overwrites the earlier compiled pair.

### Integer range object

`0x1404A7940–0x1404A7973` constructs the parser adapter on the stack. Before
dispatch it writes zero to the qword that contains the temporary signed
32-bit `min` and `max`; hence both missing endpoints start as zero. It calls
`0x140A0AE00` for a scalar/block value.

For a braced value, callback `0x140A0AE70` recognizes token `0x2A3` (`min`)
and parses it directly into temporary `+0x18`; token `0x2A4` (`max`) goes to
temporary `+0x1C`. Finalizer `0x140A0AED0–0x140A0AEE2` writes:

```text
destination.span = temp.max - temp.min
destination.base = temp.min
```

For a scalar, `0x140A0AE00–0x140A0AE57` writes the scalar to `base` and zero
to `span`.

The runtime count sampler at `0x140871497–0x1408714C4` and size sampler at
`0x140871737–0x140871762` both implement the same signed inclusive rule:

```text
if span == 0: result = base
else:
    direction = +1 when span > 0, otherwise -1
    result = base + direction * (draw mod (abs(span) + 1))
```

Both callers obtain their local random draw before testing `span`, so a
zero-span scalar also consumes its normal draw. With negative span this is a
descending inclusive range; no endpoint swap occurs. Count values `<= 0`
skip the creation loop (`0x1408714C7–0x1408714CA`). Thus the vanilla
`astral_scar` descriptor at
`common/solar_system_initializers/astral_planes_initializers.txt:328` really
creates either zero or one body from `count={min=1}`.

The reversed star size in `fallen_hive_war_3` at
`fallen_empire_initializers.txt:4567`, and the same form in
`living_planet_system` at `distant_stars_initializers.txt:2642`, is therefore
the valid inclusive set `{15,14,13,12,11,10}`.

### Fixed range object

`0x1403C8AE0–0x1403C8B30` constructs the fixed-range parser adapter and also
zero-initializes its temporary endpoint pair. Its block callback
`0x140A0AD20` writes token `0x2A3` (`min`) to temporary `+0x18` and token
`0x2A4` (`max`) to `+0x20`. Finalizer
`0x140A0AD60–0x140A0AD77` writes:

```text
destination.span = temp.max - temp.min
destination.base = temp.min
```

Scalar parsing through `0x140A0AC50` stores the scalar as base and clears the
span. Values are signed fixed point with scale `100000`.

The already-proved evaluator `0x140A0AA20` is unchanged for a negative span:

```text
k = draw mod 1000
value = base + trunc_toward_zero(span * k / 1000)
```

Therefore `megacorp_caravaneer_init` at
`megacorp_intializers.txt:202`, `orbit_angle={min=355 max=5}`, compiles to
`base=35,500,000`, `span=-35,000,000`. It yields 1000 descending fixed values:
`355.00` for `k=0`, `354.65` for `k=1`, ..., `5.35` for `k=999`; the literal
upper endpoint `5.00` is not reached. The parser has no angle-specific
circular branch. Later angle accumulation/trigonometry may reduce angles
modulo a period, but that does not turn this range into `[355,360) U [0,5]`.

## Duplicate-field precedence

The rule is ordinary last-assignment replacement for these range-valued
descriptor members. Concrete vanilla checks:

- `fallen_xeno_zoo`, `fallen_empire_initializers.txt:2325–2332`:
  `orbit_distance=15` then `orbit_distance=0`; final compiled distance is
  scalar `(0,0)`. `orbit_angle={90,270}` then `orbit_angle=60`; final angle is
  scalar `(60,0)`.
- `ghost_ship_system_initializer_01`,
  `special_system_initializers.txt:1731–1734`: `size=15` then
  `size={min=5 max=10}`; final size is `(5,+5)`, sampled inclusively as
  `5..10`.

An implementation must preserve source assignment order inside each body
record. A dictionary parser that keeps the first key, rejects duplicate keys,
or canonicalizes keys before compiling changes these vanilla definitions.

Each later braced occurrence starts from `(min,max)=(0,0)`. For example, a
hypothetical `size={min=5 max=10}` followed by `size={min=7}` ends as
`(base,span)=(7,-7)`, not `(7,+3)` and not `(7,0)`.

## Bounded implementation contract

For the initializer compiler:

1. Preserve all field occurrences in source order until the typed body
   descriptor is compiled.
2. Scalar integer/fixed values compile as `(base=value, span=0)`.
3. For every braced occurrence create fresh zeroed `min` and `max` locals.
4. Apply every `min`/`max` assignment in block order; repeated endpoint keys
   are also last-wins within that block.
5. Store `(base=min, span=max-min)` using signed arithmetic. Do not swap
   endpoints and do not fill a missing endpoint from the other endpoint.
6. Replace the descriptor member after each valid occurrence. The last valid
   occurrence is the runtime value.
7. Sample integer count/size with the signed inclusive algorithm above.
8. Sample fixed orbit fields with the 1000-step signed half-open evaluator.
   Apply normal downstream angle-period handling only where the native caller
   does so; do not reinterpret a reversed angle range as a short circular arc.
9. Reject arithmetic outside the supported signed domain in a safe replica;
   the cited vanilla values are all representable.

These semantics are exact for the cited vanilla exceptions and remove them
from the initializer parser-gap list. General malformed-node diagnostics and
arbitrary mod overflow behavior remain outside this narrow contract.
