# Local Asset Workbench

This is a technical probe, not the M2 server/client. It requires Node.js (tested with 24.19.0), pnpm and your own Stellaris 4.5.2 installation. Run the PowerShell commands from the repository root; do not copy Markdown fences into the terminal.

```powershell
Set-Location .\tools\asset-probe
pnpm install --frozen-lockfile --ignore-scripts
node server.mjs 5096
```

Open `http://127.0.0.1:5096/`. The workbench listens only on loopback; stop it with Ctrl+C. It serves its own code and npm dependencies, does not read game files and accepts no uploads. Internet access is needed for the initial dependency install, not for reading assets.

Select the game folder with the button, or select these five files from `gfx/models/ships/mammalian_01/` using “Выбрать файлы образца” (“Choose sample files”):

- `mammalian_01_corvette_S1.mesh`;
- `mammalian_01_corvette_diffuse.dds`;
- `mammalian_01_corvette_normal.dds`;
- `mammalian_01_corvette_specular.dds`;
- `_mammalian_01_ships_meshes.gfx`.

The file input has been checked. The folder picker is implemented, but its system permission and revocation flow have not passed manual acceptance. The API requires a secure context; do not bypass browser protections. The installation is not modified. An incomplete set shows an explicit error and preserves the previous valid scene.

Checks include model and texture visibility in both canvases; drag/wheel camera controls; clicking a ship → `fleet:0`; one/200 instances; frame comparison; 10 lifecycle cycles; and cleanup/recreation. The JSON output below contains hashes, GPU capabilities, measurements and limitations. Measurements depend on vsync and browser and are not a gameplay SLA.

Run tests separately from the active workbench:

```powershell
$env:STELLARIS_GAME_ROOT = Read-Host 'Path to your Stellaris installation'
node --test *.test.mjs
```

Without the variable, tests using real content are SKIP; synthetic tests still run. Do not add `node_modules`, original/derived assets or screenshots to Git. See the [ASSET-PROBE-REPORT](../../docs/ASSET-PROBE-REPORT.md).

The reader has a limited BGRA8 implementation for five M3 UI icons; those tests use the same command. The previous `index.html` is retained, and icons appear in a separate M3 gate below; this does not complete the production M3 importer. See the [M3 asset manifest](../../docs/M3-ASSET-MANIFEST.md) and [current validation and limits](../../docs/M3-IMPORT-VALIDATION.md).

## M3 Static Visual Gate

In Google Chrome with WebGL2, use the same server at `http://127.0.0.1:5096/m3.html`. Select your Stellaris 4.5.2 root folder and grant read-only access. The importer checks all 63 paths in profile `m3-static-452-v5`; the list is shown on the page. Alternatively, the File input requires all 63 files with unique basenames, not the entire installation. See `docs/M3-GALAXY-PRESENTATION-IMPORT.md` for core/dust inputs, `docs/M3-PRESENTATION-IMPORT.md` for bounded background/UI dependencies and `docs/M3-STAR-IMPORT.md` for preserved star requirements. A replacement that exceeds the retained-array budget preserves the active profile: reload the page, then import again. Material integration and GPU comparison are separate gates.

Select a corvette, a modern outpost, a continental planet and a G-type star in sequence. Expected layer counts are 1/3/5/2, with no broken geometry or independent layer displacement. Check textures, light/dark edges on the five icons, camera rotation/zoom and reset. Star/atmosphere materials are deliberately approximate, not vanilla shaders.

For each model, click “Освободить сцену” (“Release scene”): geometries/textures should reach 0; “Пересоздать” (“Recreate”) should restore the scene without rereading files. Repeat ten times; counters must not grow. A subsequent incomplete import should fail while preserving the previous scene. If the shared decoded budget is insufficient, replacement is also rejected; free the GPU scene before importing again. Reloading the page requires selecting files again; there is no persistent cache.

Numeric import and preliminary review in the embedded browser have passed. **Required checks in Google Chrome, folder permission/revocation, context loss on real GPU, 1080p/FPS and 200 instances have not been completed.** This is not the M3 network client or the production worker importer. Save the JSON text with duration and hashes for the report; do not commit screenshots of game models.
