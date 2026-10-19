# M3 — G-star surface import dependency

Current reference-driven extension: [bounded presentation dependencies](M3-PRESENTATION-IMPORT.md). It supersedes the historical v3 counts/versions below with v4/60 paths and explicit source-backed LOD, while preserving all star surface requirements.

Pre-implementation contract, October 12, 2026. Basis: `b1a37c7:docs/M3-SYSTEM-VISUALS.md`, import dependency section; existing visual milestone. Import/decoder/adapter only, without renderer/UI/camera/simulation.

Profile `m3-static-452-v3`: the previous 44 paths plus explicit `gfx/worldgfx/star_g_class.txt`, `lava_noise.dds`, `lava_d.dds`, and `stone_d.dds` in the same folder, and `common/planet_classes/00_planet_classes.txt`: 49 total. The last path was separately approved by the integrator before coding as the source for the existing pc_g_star atmosphere; this is not a planet catalog import. Schema1, importerVersion2, materialProfileVersion2, materialProfile `m3-static-star-v2`; the old profile is rejected, and the canonical digest includes the new versions/files. All four visuals are retained. There is no glob-based installation scan, upload, disk cache, or execution of game shader files.

## Reader and data

`inspectDds` fully validates layout and payload without output pixel allocations, returning dimensions/codec/mipCount/faceCount/decodedBytes. `decodeDds` preserves the old 2D shape `{width,height,rgba,codec,mipCount,decodedMip:0}`. For a cube it returns `{width,height,faces,codec,mipCount,decodedMip:0,isCubemap:true}`, with six RGBA8 Uint8Array faces in +X/−X/+Y/−Y/+Z/−Z order. No rgba alias or second decoded cube copy. Mip chains are validated in full; only each face's base mip is decoded.

Cube: legacy DXT1/3/5, square dimensions, CAPS2 cubemap and all face bits; volume, depth>1, DX10, partial/unknown CAPS2, and truncated/trailing payload are rejected before pixel allocations. Existing 2D/BGRA8 limits are not relaxed. Face bytes are preserved without arbitrary flip/rotation; a directional synthetic fixture checks DDS order and sampler direction conventions, while real GPU seam/orientation remains pending.

The profile requires noise DXT1/cube/1024²/1mip, lava DXT5/2D/1024²/11mips, and stone DXT1/2D/1024²/11mips. Before decode, sum 6×pixelBytes for the cube, 1× for 2D, and the existing geometry reserve; decoded ceiling512MiB, including retained old+staged data. Check budgets and ensure errors/cancellation preserve the previous set. Shared images are stored once; worker transfers account for all face buffers without duplicates.

`star.g.images` contains the three new images. `star.g.starSurface` contains noisePath/lavaPath/stonePath and explicit `lavaBrightColor`, `lavaHotStoneColor`, `lavaColdStoneColor` (HSV triples), with their corresponding intensity values. From the star_g_class AST, require unique finite numeric3 HSV values, finite nonnegative intensity, and exact tex_lava_* paths; missing/duplicate/wrong-type/wrong-path data throws before profile replacement. Other worldgfx fields are not executed and do not expand the allowlist.

The Three adapter receives the validated cube and creates a CubeTexture with DataTexture faces without pixel copies; flipY=false, NoColorSpace, clamp, linear, and no generated mipmaps. Lava/stone are ordinary linear data textures with repeat; do not infer sRGB from the name. The texture belongs to the calling profile owner; the material receives a borrowed texture and does not dispose it. The renderer connects the adapter in a separate integration step.

`star.g.starSurface` also contains `atmosphereColor` (HSV numeric3), `atmosphereIntensity` (finite nonnegative), and `atmosphereWidth` (finite positive), from the sole pc_g_star block and unique atmosphere_color/intensity/width values; malformed/unsupported/duplicate values are rejected before decode. Other planet classes are not imported. HSV→RGB/colortransfer/basis/time/alpha is defined by a separate material contract, not the reader, and this is not a native fidelity claim.

## Acceptance

Synthetic six distinct faces/order/directional sampling; malformed caps/layout/payload/mips; budget before decompression, retained+staged, worker transfers/cancel; uniform missing/duplicate/path errors; manifest JS/C# parity49 and digest/version mismatch. Real read-only 4.5.2 profile, all previous visuals/layers, and three new textures. All available Node regressions and server AssetChecks; no original bytes or screenshots in Git. GPU orientation/seams and visual acceptance are separate and are not replaced by CPU tests.
