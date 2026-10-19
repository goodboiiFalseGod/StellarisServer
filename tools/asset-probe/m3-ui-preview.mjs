// Local synthetic DOM check. Runs the actual UI client against explicit adapters, not game/GPU logic.
import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
const root = new URL('../../src/Stellaris.Server/wwwroot/', import.meta.url);
const port = Number(process.env.M3_UI_PREVIEW_PORT ?? 5187);
const server = createServer((request, response) =>
{
    const path = new URL(request.url, 'http://localhost').pathname;
    if (path === '/preview-adapters.mjs')
    {
        response.writeHead(200, { 'Content-Type': 'text/javascript; charset=utf-8' });
        response.end(readFileSync(new URL('m3-ui-preview-adapters.mjs', import.meta.url))); return;
    }
    const files = new Map([['/', 'm3.html'], ['/m3.html', 'm3.html'], ['/m3.css', 'm3.css'], ['/m3-client.mjs', 'm3-client.mjs'], ['/m3-ui-presenter.mjs', 'm3-ui-presenter.mjs'], ['/m3-ui-sprites.mjs', 'm3-ui-sprites.mjs'], ['/m3-outliner-navigation.mjs', 'm3-outliner-navigation.mjs']]);
    files.set('/m3-planet-panel.mjs', 'm3-planet-panel.mjs');
    files.set('/m3-job-controls.mjs', 'm3-job-controls.mjs');
    files.set('/m3-construction-controls.mjs', 'm3-construction-controls.mjs');
    const file = files.get(path);
    if (!file) { response.writeHead(404); response.end(); return; }
    let content = readFileSync(new URL(file, root), 'utf8');
    if (file === 'm3-client.mjs')
    {
        content = content.replace(/from '\.\/(?:m3-connection\.mjs|m3-modules\/[^']+)'/g, "from '/preview-adapters.mjs'");
        content = content.replace('let profile = null;', 'let profile = { decodedBytes: 0 };');
        content = content.replace('let saved = readSaved();', "let saved = { token: 'synthetic-preview', name: 'DOM fixture' };");
    }
    if (file === 'm3.html')
    {
        content = content.replace('Stellaris Server · M3</title>', 'Synthetic DOM preview · M3</title>');
        content = content.replace('<canvas id="canvas"', '<p style="position:absolute;left:60px;top:55px;color:#698a80">СИНТЕТИЧЕСКИЙ DOM-МАКЕТ · БЕЗ ИГРОВОЙ ГРАФИКИ</p><canvas id="canvas"');
    }
    response.writeHead(200, { 'Content-Type': `${file.endsWith('.html') ? 'text/html' : file.endsWith('.css') ? 'text/css' : 'text/javascript'}; charset=utf-8`, 'Cache-Control': 'no-store' }); response.end(content);
});
server.on('error', error => { console.error(error); process.exitCode = 1; });
server.listen(port, '127.0.0.1', () => console.log(`Synthetic DOM preview: http://127.0.0.1:${port}/m3.html`));
