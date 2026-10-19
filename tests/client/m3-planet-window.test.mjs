import test from 'node:test';
import assert from 'node:assert/strict';
import { calculatePlanetWindowGeometry } from '../../src/Stellaris.Server/wwwroot/m3-planet-window.mjs';

test('сохраняет минимальный размер и ограничивает позицию viewport', () => {
  const result = calculatePlanetWindowGeometry(
    { left: 2000, top: 2000, width: 200, height: 100 },
    { width: 1200, height: 800 },
  );
  assert.deepEqual({ left: result.left, top: result.top, width: result.width, height: result.height },
    { left: 480, top: 320, width: 720, height: 480 });
  assert.equal(result.constrained, false);
});

test('маленький viewport сохраняет минимум и сообщает об ограничении', () => {
  const result = calculatePlanetWindowGeometry({ left: 80, top: 90, width: 720, height: 480 }, { width: 500, height: 350 }, { hasPanel: true });
  assert.deepEqual({ left: result.left, top: result.top, width: result.width, height: result.height },
    { left: 0, top: 0, width: 720, height: 480 });
  assert.equal(result.constrained, true);
  assert.equal(result.panel.mode, 'overlay');
});

test('правая панель сдвигает окно, а при нехватке места накладывается под шапкой', () => {
  const outside = calculatePlanetWindowGeometry({ left: 200, top: 30, width: 720, height: 480 }, { width: 1300, height: 900 }, { hasPanel: true, headerHeight: 52 });
  assert.deepEqual({ mode: outside.panel.mode, left: outside.left, panelLeft: outside.panel.left },
    { mode: 'outside', left: 200, panelLeft: 920 });
  assert.equal(outside.panel.top, 30);
  assert.equal(outside.panel.maxHeight, 480);

  const overlay = calculatePlanetWindowGeometry({ left: 100, top: 30, width: 900, height: 600 }, { width: 1100, height: 800 }, { hasPanel: true });
  assert.equal(overlay.panel.mode, 'overlay');
  assert.equal(overlay.panel.top, 70);
  assert.equal(overlay.panel.maxHeight, 560);
  assert.equal(overlay.panel.left, 770);
  assert.equal(overlay.panel.width, 230);
});

test('панель автоматически возвращается наружу при расширении viewport', () => {
  const geometry = { left: 100, top: 20, width: 900, height: 600 };
  assert.equal(calculatePlanetWindowGeometry(geometry, { width: 1100, height: 800 }, { hasPanel: true }).panel.mode, 'overlay');
  assert.equal(calculatePlanetWindowGeometry(geometry, { width: 1400, height: 800 }, { hasPanel: true }).panel.mode, 'outside');
});

test('сдвигает окно для наружной панели и не двигает окно, если панели нет', () => {
  const geometry = { left: 350, top: 0, width: 720, height: 480 };
  const withPanel = calculatePlanetWindowGeometry(geometry, { width: 1200, height: 600 }, { hasPanel: true });
  assert.equal(withPanel.left, 250);
  assert.equal(withPanel.panel.mode, 'outside');
  const withoutPanel = calculatePlanetWindowGeometry(geometry, { width: 1200, height: 600 });
  assert.equal(withoutPanel.left, 350);
  assert.equal(withoutPanel.panel.mode, 'none');
});

test('уменьшает размер до viewport и удерживает нижний край вместе с ручкой', () => {
  const result = calculatePlanetWindowGeometry({ left: 0, top: 700, width: 1400, height: 900 }, { width: 1000, height: 700 });
  assert.deepEqual({ left: result.left, top: result.top, width: result.width, height: result.height },
    { left: 0, top: 0, width: 1000, height: 700 });
});

test('корректно сохраняет нулевые координаты', () => {
  const result = calculatePlanetWindowGeometry({ left: 0, top: 0, width: 720, height: 480 }, { width: 1000, height: 700 });
  assert.equal(result.left, 0);
  assert.equal(result.top, 0);
});
