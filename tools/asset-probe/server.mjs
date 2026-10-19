import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

// Сервер раздаёт только собственный стенд и npm-зависимости, не читает установку игры.
const root = dirname(fileURLToPath(import.meta.url));
const port = Number(process.argv[2] ?? 5096);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Неверный порт стенда.');
const types = { '.html': 'text/html', '.mjs': 'text/javascript', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };
const server = createServer(async (req, res) => {
  try {
    if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405).end(); return; }
    const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    const name = pathname === '/' ? 'index.html' : pathname.slice(1);
    if (!['index.html', 'app.mjs', 'adapters.mjs', 'formats.mjs', 'styles.css', 'm3.html', 'm3.css', 'm3-app.mjs', 'm3-renderer.mjs', 'm3-profile.mjs', 'm3-memory.mjs', 'static-pose.mjs', 'visual-definitions.mjs'].includes(name) && !name.startsWith('node_modules/')) {
      res.writeHead(404).end(); return;
    }
    const path = resolve(root, name);
    if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
    if (!(await stat(path)).isFile()) { res.writeHead(404).end(); return; }
    res.setHeader('Content-Type', types[extname(path)] ?? 'application/octet-stream');
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self'; connect-src 'none'; img-src 'self' blob: data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'");
    res.end(req.method === 'HEAD' ? undefined : await readFile(path));
  } catch (error) {
    if (error.code !== 'ENOENT') console.error('Ошибка статического стенда:', error.message);
    if (!res.headersSent) res.writeHead(404);
    res.end();
  }
});
server.listen(port, '127.0.0.1', () => console.log(`Asset probe: http://127.0.0.1:${port}; игровые файлы выбираются только браузером.`));
process.on('SIGINT', () => server.close());
