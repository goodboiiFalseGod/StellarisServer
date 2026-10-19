# Galaxy core/dust — minimal import extension

12 October 2026. Integrator approved contract `9fb4f53` for implementation. Graphics confirmed the three-source minimum and exact galaxyLayers API; independent contract review verified source hashes/layouts and arithmetic with no substantial findings. Implementation/test status is recorded in [validation evidence](reports/m3-star-import-validation.md). Preserves the completed [v4 import](M3-PRESENTATION-IMPORT.md), all four models and its consumer APIs. This extension supplies decorative rendering inputs only, without changing topology, coordinates, visibility, simulation or picking.

## Evidence and explicit inference

Read-only Stellaris 4.5.2 sources:

- `gfx/FX/galaxy_center.shader:10–16` declares CenterTexture with linear/wrap sampling. Lines 56–57 construct `(x,-1.5,z)` and multiply XZ by 1.5; lines 74–76 sample the center texture, multiply by 0.5 and apply intel. Lines 84–95 disable depth and use ONE/ONE additive blending.
- `gfx/FX/galaxy_dust.shader:10–25` declares DustTexture with linear/wrap and ColorTexture with linear/clamp sampling. Lines 81–108 scale, rotate, tilt and position patches, deriving color UV from world XZ and DustCloudUV. Lines 130–143 sample dust and color, combine border/intel, then multiply. Lines 151–161 disable depth and use SRC_ALPHA/INV_SRC_ALPHA.
- Reference answer `01a13049-e023-73f0-8cb7-b5c4beb3efc0` records matching candidate paths in executable strings. These names and sampler roles support the explicit **inferred bindings** center.dds → CenterTexture, dust.dds → DustTexture, galaxycolor.dds → ColorTexture. Exact native runtime binding, patch distribution, UV mapping and source color-transfer conventions remain unverified. Do not present browser calibration as a recovered native formula.

These are distinct galaxy passes; `sky_core.dds` remains the system sky, not a substitute for galaxy core/dust. Shaders/includes/intel/border textures are evidence, not new executable imports or transitive allowlist additions. Browser placement is deterministic, noninteractive presentation calibration against the supplied screenshots. It cannot fabricate discoverable objects or hidden world data.

## Exact dependency set and identity

Profile `m3-static-452-v5`, schema 1, importer 4, material 4, material profile `m3-static-galaxy-v4`. Preserve all 60 v4 paths and add only these three, for **63**. Browser/server allowlists, canonical digest and versions must agree; old profiles are incompatible. Hash original complete DDS bytes, not decoded images. The hashes below record this audited local source, not a replacement for the server/client manifest comparison or a universal installation signature.

| Relative path | Required source layout | Encoded bytes | Retained base RGBA bytes | Audited SHA-256 |
|---|---|---:|---:|---|
| gfx/map/center.dds | DXT5, 1024×1024, 11 mips, 1 face | 1,398,256 | 4,194,304 | 25877c1ae840c8c22d6b6767f5d17efd81b451a66c717ee5b762d4cedb28b90c |
| gfx/map/dust.dds | DXT5, 1024×1024, 11 mips, 1 face | 1,398,256 | 4,194,304 | 4d08f9d176682e4e1ed5fa6b6b34e18f06774428c551ec89200aa24cda55d691 |
| gfx/map/galaxycolor.dds | DXT5, 2048×2048, 12 mips, 1 face | 5,592,560 | 16,777,216 | 51b27b3ec6e7ceae7608cad7de303bbef0ee8dc48bbe06c9d6ce324f3124d34a |

Existing strict 2D decoder is sufficient: validate the complete declared mip chain and exact payload before allocation, decode base mip only (`decodedMip=0`), no new resampling/LOD API. Preserve straight RGBA bytes including alpha, without premultiplication, CPU color conversion, flipping or full-image scratch copies. Native mip payloads are validated but not retained as decoded arrays. No fonts, other galaxy textures, shaders, LUT, filesystem crawling, source upload or persistent cache.

## Consumer API and ownership

`profile.galaxyLayers: Map<string, GalaxyLayer>` has exactly `core`, `dust`, `color` keys. Each layer is `{image, sourcePath, encoding:'raw-rgba'}`; image is the existing validated 2D DDS result. Paths map respectively to the table above. No copies into icons/backgrounds and no overload of their APIs.

Importer owns one shared decoded image per source. Renderer owns borrowed-image GPU wrappers, materials and patches, retains shared assets across ordinary view changes, and disposes wrappers on profile replacement/pagehide. Material disposal must not dispose borrowed shared textures. Failed staging/cancellation must preserve the previous profile. No new conversion buffers: use NoColorSpace raw sampling initially, preserve straight alpha, and apply any explicitly documented color calibration in the shader. This is a browser approximation because native transfer is unverified. Core uses additive blending at source half-intensity; dust multiplies dust/color samples with straight-alpha blending; both disable depth writes/tests and are nonpickable. Center/dust use repeat with linear filtering; color uses clamp with linear filtering, matching source samplers. Renderer documents UV/patch calibration and optional GPU mip generation separately.

## Allocation and failure boundaries

Three new encoded inputs total **8,389,072 bytes**; decoded outputs total **25,165,824 bytes (24 MiB)**. No additional full-image CPU scratch/conversion arrays. New totals: encoded **128,489,971**, decoded **337,550,852**, existing converted **117,473,408**, decoded + converted **455,024,260 bytes (433.94 MiB)**. Remaining accounted-array headroom is **81,846,652 bytes (78.06 MiB)**, not permission to allocate arbitrary scratch. The unchanged ceiling is **536,870,912 bytes (512 MiB)**; geometry reserve and predecode refusal remain unchanged.

Initial encoded + decoded + converted estimate is **583,514,231 bytes (556.48 MiB)**; it is not measured total RAM and does not redefine the decoded-array guard. Raw inputs are released after validation/build/recheck as before. Old v5 + staged v5 decoded + converted would be **910,048,520 bytes**; even two decoded profiles alone exceed the ceiling. Reject replacement before output allocation, preserve the old profile and advise **“Reload the page, then reimport.”** Never silently evict active assets.

GPU base texture estimate increases from 310,411,392 to **335,577,216 bytes (320.03 MiB)**, excluding existing 2D generated mips, geometry, UI canvases, framebuffers and driver overhead. Generating mips for only these three new textures adds approximately **8 MiB**; shader wrappers share decoded pixels and create no CPU pixel copies. This is not measured VRAM or a total browser-memory claim.

Missing/hash-mismatched/changed files, unsupported layout, unexpected dimensions/codec/mips/faces or insufficient budget are visible failures, not silent substitutes. Validate every selected file and the aggregate reserve before geometry/image output allocation. Cancellation and late replies obey existing worker ownership rules.

## Acceptance and review gate

Before implementation, graphics/integrator confirm the minimal three-source set and exact Map API. Tests must cover exact 63-path JS/C# parity, version/digest rejection, malformed source metadata/layout, predecode aggregate-budget refusal, unique worker transfers/shared image identity and cancellation preserving active data. Real-source test decodes all required paths and checks the new totals without changing existing models/layers/icons/backgrounds/UI sprites. Repeat full Node and C# AssetChecks, independently review implementation, and report skips explicitly. Real GPU blending/color/orientation, screenshots at far/near presets, resource lifecycle and final user acceptance are separate integration gates. No source assets or screenshots enter Git.
