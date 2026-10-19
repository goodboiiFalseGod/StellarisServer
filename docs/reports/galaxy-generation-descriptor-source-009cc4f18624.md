# Solar-system body descriptor defaults — Stellaris 4.5.2 (9776)

This handoff corrects the earlier statement that a missing `class` produces no
object. It does not. The native descriptor constructor installs the reserved
class mode `random`; this is why the class-less planets and moons in
`basic_init_01` create ordinary random bodies.

## 1. Native evidence boundary

- Body-descriptor constructor: `0x140505AC0–0x140505E15` (object size
  `0x580`).
- Body-descriptor field parser: `0x1405060E0–0x140506B99`.
- Initializer parser allocation of a root `planet`: `0x140507D10–0x140507D58`;
  it calls the descriptor constructor with `moon=false`.
- Recursive `planet` allocation: `0x14050621A–0x140506236`; recursive `moon`
  allocation: `0x1405062A7–0x1405062C3`; only the latter passes `moon=true`.
- Main planet executor: `0x140870DC0–0x140871E66`; body loop begins at
  `0x140871480`.
- Recursive child/moon executor: `0x1408706C0–0x140870DA3`.
- Class dispatcher: `0x140506C10–0x1405072FB`.
- Fixed-point range sampler: `0x140A0AA20–0x140A0AC03`.

The relevant source example is
`common/solar_system_initializers/misc_system_initializers.txt`,
`basic_init_01`: the ordinary `planet` and its nested `moon` omit `class` and
`size`. They therefore exercise the constructor defaults described below.

## 2. Exact constructor defaults

All offsets below are from the `0x580`-byte compiled body descriptor.

| Offset | Native constructor write | Meaning |
|---|---|---|
| `+0x08` | `0`, then optional mask `0x80` | Descriptor flags; bit 7 marks a moon descriptor. |
| `+0x0C` | `1` | `count.min = 1`. |
| `+0x10` | `0` (upper half of the qword written at `+0x0C`) | `count.delta = 0`; therefore absent `count` is exactly one body. |
| `+0x18` | token `0x2C9B`, text `random` | Absent `class` uses reserved dispatcher mode `random`. |
| `+0x138` | `-1` | Explicit-size sentinel: use the selected planet class's default size range. |
| `+0x13C` | `0` | Explicit-size range delta, inactive while the sentinel is negative. |
| `+0x140` | `1,000,000` | Default orbit-distance fixed-point minimum, i.e. `1.0`. |
| `+0x148` | `1,000,000` | Default orbit-distance fixed-point span, i.e. `1.0`; sampler domain is the native continuous/fixed-point interval starting at 1 and spanning 1. |
| `+0x150` | `9,000,000` | Default orbit-angle fixed-point minimum, i.e. `90.0` degrees. |
| `+0x158` | `18,000,000` | Default orbit-angle fixed-point span, i.e. `180.0`; effective interval starts at 90 and spans to 270. |
| `+0x160` | native null/default modifier handle | No explicit modifier. |
| `+0x168` | native null/default anomaly handle | No explicit anomaly category. |
| `+0x538` | `-1` | No assigned star ordinal. The initializer parser assigns it only to descriptors whose class token is `star`. |
| `+0x540` | `3` | Default satellite naming-policy enum, exactly `lower_case_latin_letters` under the native parser mapping below. |
| `+0x578` | `false` | `has_independent_orbital_line` is disabled. |

The constructor proof for the core defaults is contiguous:

- `0x140505AEA`: flags are zeroed;
- `0x140505AEE`: the qword at `+0x0C` becomes `1`, hence `(min,delta)=(1,0)`;
- `0x140505B1D–0x140505B54`: the class token and stored text become
  `0x2C9B / "random"`;
- `0x140505B58–0x140505B8A`: size, distance and angle defaults above;
- `0x140505D78–0x140505DE5`: star ordinal, naming policy and independent-line
  defaults;
- `0x140505DEC–0x140505DF9`: constructor argument `moon` is encoded as flag
  mask `0x80`.

The `satellite_naming_policy` parser constructs an enum sink for `+0x540` at
`0x14050683F–0x14050685C`, whose callback is `0x140A367F0`. The callback and
token-registration table (`0x1401954A5–0x14019551C`) give the exact mapping:

| Script token | Token id | Stored enum byte |
|---|---:|---:|
| `lower_case_roman_numerals` | `0x3C84` | `1` |
| `upper_case_roman_numerals` | `0x3C85` | `1` |
| `arabic_numerals` | `0x3C86` | `2` |
| `lower_case_latin_letters` | `0x3C88` | `3` |
| `upper_case_latin_letters` | `0x3C87` | `4` |

Both Roman spellings intentionally collapse to enum value 1 at this compiled
layer. Since the constructor stores 3, an absent policy has the same compiled
value as explicit `lower_case_latin_letters`; no approximation is needed.

Token registration independently maps `0x2C9B` to `random` at
`0x14017C6E4–0x14017C6EC`. The class dispatcher explicitly handles that token
at `0x140506EA2` and enters the general random-class selection path. Missing
class is therefore not the invalid/sentinel class path.

## 3. Exact executor behavior for absent fields

For every descriptor the executor consumes the count draw first, even when
`count.delta=0`; see `0x140871483–0x1408714C7` (and
`0x1408707CC–0x140870810` for children). The absent-count result is exactly 1.

For each instance:

1. Resolve `descriptor.class` through `0x140506C10`. With no scripted class,
   this is `random` and selects an eligible ordinary class using orbit/star
   constraints.
2. Consume/sample the orbit distance from `+0x140/+0x148` and advance the
   sibling accumulator (`0x14087154F–0x14087157C`). This occurs before the
   resolved-class validity check.
3. If class resolution returned the invalid sentinel, stop the instance after
   the orbit advance. This is a failed random selection, not the meaning of an
   absent `class` field.
4. Resolve size. If `+0x138 < 0`, a root/ordinary planet uses the selected
   class range at `class+0x3E8`. In the recursive executor a moon descriptor
   (flag `0x80`) uses `class+0x3E0`; a nested non-moon planet uses
   `class+0x3E8` (`0x140870925–0x140870944`). The size draw is consumed even
   when that chosen range has zero delta.
5. Allocate and attach the body.
6. Consume/sample orbit angle from `+0x150/+0x158` and advance the angle
   accumulator (`0x1408717D4–0x1408717FE`, child equivalent
   `0x1408709F6–0x140870A1D`).

The native fixed-point range object is `(minimum, span)`, not `(minimum,
maximum)`. `0x140A0AA20` returns the minimum directly when span is zero;
otherwise it deterministically scales the supplied random value into the span
and adds the minimum. Preserve the two constructor pairs literally to avoid
turning the default orbit distance into a fixed 1 or the default angle into
90–180.

For `basic_init_01`, this means:

- the star remains explicit `class=star`, count 1, distance 0, angle 1 and
  explicit size 20–30;
- each class-less ordinary planet executes as `class=random` and gets its size
  from the selected class's ordinary range;
- each class-less moon also executes as `class=random`, carries flag `0x80`
  and gets its size from the selected class's moon range;
- explicit orbit values in the script replace the constructor's distance and
  angle defaults.

### 3.1 `class=star` ordinal in a multi-star initializer

The initializer constructor zeroes its star-descriptor cursor at `+0x2A0`
(`0x1405078F0`). After parsing and appending each **root** `planet` descriptor,
the initializer parser compares its class token with `star`. On an exact match
it copies the current cursor to `descriptor+0x538` and then increments the
cursor (`0x140507D58–0x140507D77`). Non-star root descriptors do not advance
it.

This index is therefore the zero-based ordinal of the root `class=star`
**descriptor in source/compiled insertion order**. It is not the ordinal of
every root planet and it is not incremented by that descriptor's `count`.
Consequently every instance produced by a single `class=star, count>1`
descriptor reuses the same ordinal.

The class dispatcher consumes this ordinal in its `star` branch
(`0x140506C80` onward):

- if the system initializer's selected system class exposes exactly one star
  class, the dispatcher selects that sole entry;
- with multiple star-class entries, `ordinal != -1` and
  `ordinal < star_class_count` are required, then it selects entry
  `ordinal` (records have stride `0x68`; the class handle is at record
  `+0x30`);
- a missing/out-of-range ordinal falls through to the invalid/default handle
  path.

The ordinal assignment is present only in the root initializer parser. The
recursive `planet`/`moon` parser does not perform it, so a native-compatible
generator must express the stars of a multi-star system as root
`class=star` descriptors. A nested `class=star` descriptor would retain the
constructor sentinel `-1`.

### 3.2 Exact child and orbital-line execution order

Nested `planet` and `moon` parser branches do not append to separate category
lists. Both allocate a descriptor and converge on the same append operation
at `0x1405064DA–0x1405064EB`, targeting the parent's vector at `+0x4F0`
(count at `+0x504`). A nested `change_orbit` is compiled as a synthetic
`class=none` descriptor and appended to that same vector
(`0x1405063BC–0x1405064F0`). Thus planets, moons, and `change_orbit` records
retain their single compiled/source insertion order.

The recursive executor first walks that unified descriptor vector at
`0x140870791–0x140870C27`. Only after it is exhausted does it walk the
separate `orbital_line` vector at `+0x508` (count at `+0x51C`),
`0x140870C2E–0x140870D72`. Therefore the exact child-category order is:

1. all nested body/change-orbit descriptor records, interleaved in parse
   order;
2. all standalone child orbital-line records, in their own parse order.

The root initializer has the same shape: root `planet` and synthetic
`change_orbit` records share the initializer body vector appended through
`initializer+0x2B8`; the main executor reads its pointer/count at
`+0x2C0/+0x2CC` beginning at `0x140871450`. Top-level `orbital_line` records
are held separately and processed afterward from `+0x2D8/+0x2E4`
(`0x140871B4F` onward). Any reproduction that executes all moons before all
planets changes native ordering and RNG consumption.

## 4. Parser flags relevant to defaults

`0x1405060E0–0x140506B99` maps fields to the following descriptor state:

| Field | Native storage/action |
|---|---|
| `class` | Replaces the default token object at `+0x18`; parser branch `0x140506292–0x1405062A2`. |
| `size` | Replaces sentinel range at `+0x138`; branch `0x140506168–0x140506177`. |
| `orbit_distance` | Replaces range `(1.0, span 1.0)` at `+0x140`; branch `0x14050634D–0x14050635C`. |
| `orbit_angle` | Replaces range `(90.0, span 180.0)` at `+0x150`; branch `0x140506339–0x140506348`. |
| `count` | Replaces integer range at `+0x0C`; branch `0x14050699F–0x1405069AB`. |
| `has_ring` | Mask `0x40` records explicit presence; mask `0x01` records the requested bool (`0x1405061D5–0x140506215`). If absent, later class-probability ring logic remains enabled. |
| `home_planet`, `starting_planet` | Both parser tokens converge on `0x1405062F5–0x140506334` and write the same mask `0x02`. They are native aliases at this compiled-descriptor layer, not two separate booleans. |
| `anomaly=none` | Mask `0x04`, and restore the default/null anomaly handle (`0x140506605–0x140506623`). |
| `deposit_blockers=none` | Mask `0x08` (`0x140506521–0x140506531`). |
| `deposits=none` | Mask `0x10` (`0x1405065A3–0x1405065B3`). |
| `modifiers=none` | Mask `0x20` when there is no already compiled explicit modifier (`0x140506723–0x1405067E0`). |
| `moon` descriptor kind | Mask `0x80`, set only by the constructor call with `moon=true`. |
| `has_independent_orbital_line` | Separate bool byte at `+0x578`, branch `0x140506B04–0x140506B13`. |

Post-create confirms the masks: `0x140E0BC58–0x140E0BDF8` clears random
modifiers for `0x20`, suppresses random deposits for `0x10`, converts blockers
for `0x08`, applies explicit/null anomaly policy for `0x04`, and uses the
`0x40/0x01` pair for explicit versus probabilistic ring behavior.

## 5. Required correction to the reproduction contract

Replace:

> Missing/invalid class resolves to no object.

with:

> Missing `class` retains the constructor default `random`. The native random
> class dispatcher is executed. Only an explicit `none`, an unresolved invalid
> token/list, or a random selector with no eligible result advances the orbit
> without allocating a body.

Also merge `home_planet` and `starting_planet` into one compiled boolean at this
layer. A higher-level schema may retain both source spellings for provenance,
but their runtime descriptor flag is the same mask `0x02`.

These findings close the structural blocker for class-less bodies in the
ordinary misc initializers. Exact replay still depends on the previously
documented RNG state and planet-class eligibility/weight evaluation, but no
approximation is required for these descriptor defaults.
