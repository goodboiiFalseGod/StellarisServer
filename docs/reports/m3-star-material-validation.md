# Static G-star material validation

12 October 2026. Material commit `4f35e7c`; imported-parameter mapper commit `6342076`; renderer wiring `f006f9b`, with reviewed sparse-cubemap fix `b706c97` merged before real GPU testing. Contract: [system visuals](../M3-SYSTEM-VISUALS.md). Production serving and final assembled QA are owned by the coordinator.

## Completed checks

- `node --test tools/asset-probe/m3-star-material.test.mjs`: **4/4 PASS**. Borrowed texture ownership, separate mutable uniforms, missing inputs, sparse arrays, invalid sampler types, nonfinite values, degenerate noise basis, imported HSV/intensity conversion and explicit static parameters.
- `git diff --check`: PASS. Only owned material, tests and contract were changed. Managed worktree branch: `feature/system-visuals`; common checkout untouched.
- Independent read-only review of the initial material: sparse-array and non-2D sampler acceptance were identified, fixed, and reviewed again without remaining findings. The coordinator subsequently reviewed mapper `6342076`, repeated 4/4 tests, compared HSV conversion with the source and integrated mapper/report `ba90771`.
- Synthetic IAB WebGL2 shader compile and render: **PASS within this narrow scope**. Three.js 0.186.1, one SphereGeometry, six synthetic 1×1 RGBA DataTexture cubemap faces plus two synthetic 1×1 RGBA DataTextures. ShaderMaterial used createStaticGStarParameters with explicit G-star inputs; PerspectiveCamera FOV 35, camera Z=4, 256×256 framebuffer. One shader program, no onShaderError messages, `gl.getError()=0`, center pixel `[248,207,123,255]`. DOM result and screenshot were inspected through browser automation. Temporary local server on port 5197 was stopped, temporary test file removed, temporary tab closed. No game assets or screenshots were saved or committed.

## Source correspondence

`gfx/FX/standard_vertex.fxh:23–24,89–90` explicitly stores normalize(original vertex position) in vTangent under IS_STAR, including the skinned variant. The new material samples its noise from that local position with explicit noiseBasis. This matches the static input when original positions are retained and noiseBasis is identity; it does not prove native animated orientation or cubemap GPU conventions.

Surface equations were compared with PixelPdxMeshStar, `gfx/FX/pdxmesh.shader:838–919`: noise/lava/stone, cold-stone mixing, view-dependent atmosphere, rim and HDR ×2. Output alpha=1 is an explicit Three framebuffer adaptation. HSV conversion follows the mathematical structure of `standardfuncsgfx.fxh:184–196`; treating the result as linear uniforms without gamma transfer is an explicit M3 adapter policy, not proof of native CPU conversion.

## Renderer delivery checks

- Independent read-only review of `f006f9b`: no actionable defects. Profile owns the cube, six DataTexture face wrappers and two surface DataTextures; view instances borrow those resources. No new decoded pixel copies. Template scale, original geometry, layer order and CAM code are unchanged.
- Focused material/renderer/CAM regression: **18/18 PASS**. New renderer tests also directly count release of newly staged textures after missing metadata/texture failures, while preserving the previous profile/view.
- Complete asset-probe Node suite with `STELLARIS_GAME_ROOT` pointing to the read-only 4.5.2 installation: **99 PASS, 0 FAIL, 0 SKIP**. Existing 100-switch/200-instance ownership and motion/selection regression tests included. These CPU checks are not FPS or RAM/VRAM measurements.
- Real local-source browser check: **IAB WebGL2 compile/render PASS**, not Chrome acceptance. The temporary local page selected the 49 approved source files using a file input; its HTTP server served only own scripts/npm dependencies. `buildM3Profile` yielded 261677756 decoded bytes and four templates. `M3WorldRenderer.setProfile` created the actual star material, then the two-layer star template was displayed at 800×600. Result: two shader programs, no shader errors, GL error 0, center pixel `[253,228,149,255]`, two draw calls, 3276 triangles, two geometries and three uploaded GPU textures. Profile converted-array accounting was 117473408 bytes. No frame-time/FPS claim follows from this isolated render.
- Transient screenshot inspection showed visible structure across the real star surface; the large atmosphere shell remains the previous approximate rim material. No native comparison was available. No game screenshot was saved or committed. Temporary page/server on port 5197 were closed, temporary script removed.

Production integration requires serving `m3-star-material.mjs` alongside the renderer, including the file in `Stellaris.Server.csproj`; that shared project file is intentionally left to the coordinator. The renderer source and new focused test are the only runtime/test files changed by this wiring commit.

## Pending

Integrator checkpoint: renderer `f006f9b` passed independent resource/rollback review without P1/P2 and was integrated with the production module include. Full assembled real-content Node suite: **138 PASS, 0 FAIL, 0 SKIP**. Release build: 0 warnings / 0 errors. Published-output `wwwroot/m3-modules/m3-star-material.mjs` matches the source SHA256. Actual production-route browser checks remain a separate QA step; unit results do not establish native fidelity or physical GPU memory release.

Final assembled QA; real-system comparative visual inspection; cubemap orientation/seams; GPU context restore with the new real textures; native runtime scale/light composition; atmosphere fidelity and the user's Chrome/manual acceptance. The real-asset check above isolates the star; it does not prove full-system composition, all four models rendered on GPU, target performance or native equivalence. MAP-001 remains open. This bounded renderer delivery does not start a new visual or gameplay stage.
