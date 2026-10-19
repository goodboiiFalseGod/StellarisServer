# Reference presentation import — bounded dependency contract

12 October 2026. Implements the import assignment in [reference comparison](M3-REFERENCE-COMPARISON.md), without renderer/client/camera/csproj changes. All seven reference images inform the requested backgrounds and compact frame hierarchy; they do not establish native formulas. Lifecycle clarification `abcd62d` applies: retain shared profile resources across views; failed replacement preserves the previous valid profile.

## Fixed source set and versions

Separate proposed follow-up: [galaxy core/dust import](M3-GALAXY-PRESENTATION-IMPORT.md). It does not change this completed v4 checkpoint until reviewed and implemented.

Profile `m3-static-452-v4`, schema 1, importer 3, material 3, material profile `m3-static-presentation-v3`. Preserve the 49 prior paths; add exactly 11 below, total 60. Hash every original source file, not a derived LOD image. No fonts, shader execution, LUT/environment import, filesystem crawling, upload or persistent cache. Center/dust/galaxycolor are deferred: candidate files exist but native sampler binding/placement is unproven; graphics requested the minimum starfield and one sky first.

| Additional source path | Relationship / format | Encoded bytes / retained decoded bytes |
|---|---|---|
| gfx/worldgfx/default.txt | gfx_settings world=default; sole always background → stars.dds, ycocg=no; HSV shift 0/0/0 | 2147 / no pixel arrays |
| gfx/worldgfx/stars.dds | legacy DXT1 cube 2048², 6 faces, 12 mips; select existing mip 1 → 1024² | 16777424 / 25165824 |
| gfx/map/sky_core.dds | existing star_g_class.txt first background, distance_to_core_percent < .50, ycocg=yes; DXT5 cube 2048², 6 faces, 1 mip | 25165952 / 25165824 after bounded sampling |
| interface/main.gfx | named topbar/system-panel sprite metadata | included in metadata total below |
| interface/outliner.gfx | named outliner/member metadata | included in metadata total |
| interface/government_view.gfx | named header metadata | included in metadata total |
| gfx/interface/main/topbar_background.dds | GFX_topbar_background, BGRA8 36×36, 1 mip; border 8/8 | 5312 / 5184 |
| gfx/interface/tiles/outliner_tile.dds | GFX_tile_outliner_bg, BGRA8 180×180, 1 mip; border 80/30 | 129728 / 129600 |
| gfx/interface/tiles/green_bg_tile.dds | GFX_outliner_header_bg, BGRA8 20×20, 1 mip; border 8/8 | 1728 / 1600 |
| gfx/interface/buttons/outliner_entry.dds | GFX_outliner_member_bg, BGRA8 765×50, 1 mip; 3 horizontal frames 255×50 | 153128 / 153000 |
| gfx/interface/main/starsystem_panel.dds | GFX_starsystem_panel, BGRA8 440×49, 1 mip; no border | 86368 / 86240 |

Three new metadata files total 94097 encoded bytes. All eleven additions total 42415884 encoded bytes. Named definitions must be unique, use the exact texture path/type/frame count/border and known effectFile (buttonstate_onlydisable for topbar/outliner/member; buttonstate for header; absent for system panel). Metadata is validated, shaders are not imported/executed. Other definitions in those files are not traversed. No font dependency; UI retains its documented system-font approximation.

## Cube LOD and color policy

Extend `inspectDds(input, options)` and `decodeDds(input, options)` with optional `{mipLevel:0,downsampleFactor:1}` defaults, preserving the old API. Nondefault options apply only to legacy compressed cubes. Both validate the entire source face/mip layout and exact payload first, including six complete CAPS2 faces, square dimensions, supported codec, no volume/DX10/trailing data. mipLevel is an integer within the declared chain; downsampleFactor is a positive power of two dividing the selected mip dimension. Invalid options fail before output allocation.

Stars use mipLevel 1 / downsampleFactor 1. Sky uses mipLevel 0 / downsampleFactor 2: deterministic nearest-center texel `(2*x+1,2*y+1)` from each source face, selected directly while decoding DXT blocks into the 1024² output. No full 2048² RGBA face or full-resolution cube may be allocated first. Do not average packed YCoCg channels; source byte values and alpha remain intact. Face order +X/−X/+Y/−Y/+Z/−Z, no unproven flip/rotation. Result keeps source codec/mipCount and decodedMip; width/height are output dimensions. Add sourceWidth/sourceHeight/downsampleFactor for nondefault LOD. Inspector decodedBytes counts six output faces, not the skipped base pixels.

Texture data uses NoColorSpace. Stars sample RGB directly as in sky.shader non-YCoCg branch. Sky remains packed R=Co,G=Cg,B=scale,A=Y; graphics reconstructs RGB and applies pow(abs(rgb),2.2), then source HSV shift. Native LUT/intel composition is not reproduced by import. sky_core selection is an explicit fixture presentation calibration, not an inferred native distance_to_core_percent. GPU seams/orientation and final visual fidelity remain separate checks.

## Consumer API and ownership

`profile.backgrounds: Map<string, Background>` has exactly `galaxy.stars` and `system.sky`. Background fields: `image` (validated decoded cube), `sourcePath` (exact relative string), `encoding` (`linear-rgb` or `ycocg`), `lod:{version:1,sourceWidth:2048,sourceHeight:2048,mipLevel,downsampleFactor}`, `hsvShift:number[3]`. Shifts are read strictly from default.txt and existing star_g_class.txt; missing/duplicate/nonfinite triples fail. System sky definitions are validated as the known ordered core/mid/rim trigger references, but only core bytes are required and imported; no automatic transitive crawl.

`profile.uiSprites: Map<string, UiSprite>` keyed by the five exact DDS paths. UiSprite fields: `image` (validated 2D RGBA image), `frames` (positive integer), `border:{x:number,y:number}` (nonnegative integer source pixels). Frames are horizontal; frames must divide image.width exactly; twice the border must not exceed one frame's dimensions. Missing frames default 1 and absent border defaults 0 only when the exact selected source definition omits them. Importer owns shared CPU buffers; UI owns derived canvas/styles and disposes superseded resources on replacement/pagehide. No copies into the existing icons Map. GPU owners borrow decoded pixels; materials never dispose borrowed textures.

## Memory and failure boundaries

Existing decoded payload 261677756 bytes (249.56 MiB); retained normal/specular conversion arrays 117473408 bytes (112.03 MiB). Add cube LOD 50331648 bytes and UI 375624 bytes. Projected decoded payload 312385028 bytes (297.91 MiB); decoded + converted 429858436 bytes (409.94 MiB), below the unchanged 536870912-byte (512 MiB) accounted-array ceiling. Geometry reserve retains its existing conservative preallocation rule.

Encoded original inputs: existing 77685015 + additions 42415884 = 120100899 bytes (114.54 MiB), counted separately from the decoded-array limit and released after build/recheck. LOD output is allocated once per face; additional full-face RGBA scratch 0 bytes, only existing tiny DXT block palettes/JS overhead (not claimed as total RAM). No second YCoCg → RGB CPU array is produced.

GPU base texture estimate: existing 260079744 bytes plus two LOD cubes 50331648 = 310411392 bytes (296.03 MiB), excluding derived UI canvases 375624 bytes, generated mips, geometry, framebuffer/driver overhead. Existing 2D mip generation may add approximately one third for those textures; cubes generate no mips. GPU estimates are not part of the CPU array counter and not measured VRAM.

Old v4 + staged v4 decoded + converted peak would be 859716872 bytes (819.89 MiB), so replacement while holding a complete active profile must fail before decode. Even old v3 + staged v4 exceeds 512 MiB. Preserve the active profile; never evict it silently. To replace after such refusal, the user reloads the page (disposing old assets) and explicitly imports again; no persistent cache. Initial encoded + decoded + converted estimate 549959335 bytes (524.48 MiB) is NOT total RAM and does not redefine the 512 MiB decoded-array guard. Old + staged encoded retention/JS overhead may be higher; reject staging by available decoded budget first, before allocating output arrays. The visible refusal must advise: "Reload the page, then reimport."

## Verification

Synthetic: source mip offsets per face; directional LOD samples; malformed options/layout/payload; exact preallocation budgets; unique transfers; fail/cancel preserves old profile. Strict selected sprite metadata, frame divisibility and border bounds, malformed/duplicate background references and HSV. JS/C# parity 60 and canonical version mismatch. Real 4.5.2 decode all required paths with LOD dimensions/bytes and old visual layers unchanged. No original images/bytes in Git. Integration owner updates module includes/client readiness/UI label; importer does not edit those files.
