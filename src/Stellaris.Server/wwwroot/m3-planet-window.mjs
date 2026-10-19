const MIN_WIDTH = 300;
const MIN_HEIGHT = 320;
const UI_SCALE = 1.2;
const DEFAULT_GEOMETRY = Object.freeze({ left: 14, top: 22, width: 650, height: 460 });
let savedGeometry = { ...DEFAULT_GEOMETRY };

const numberOr = (value, fallback) => Number.isFinite(Number(value)) ? Number(value) : fallback;

/** Чистый расчёт размера и размещения окна колонии и панели сведений. */
export function calculatePlanetWindowGeometry(geometry, viewport, options = {}) {
  const minWidth = options.minWidth ?? MIN_WIDTH;
  const minHeight = options.minHeight ?? MIN_HEIGHT;
  const panelWidth = options.panelWidth ?? 230;
  const headerHeight = options.headerHeight ?? 40;
  const viewportWidth = Math.max(0, numberOr(viewport.width, 0));
  const viewportHeight = Math.max(0, numberOr(viewport.height, 0));
  const constrained = viewportWidth < minWidth || viewportHeight < minHeight;
  const requestedWidth = Math.max(minWidth, numberOr(geometry.width, DEFAULT_GEOMETRY.width));
  const requestedHeight = Math.max(minHeight, numberOr(geometry.height, DEFAULT_GEOMETRY.height));
  const width = constrained ? minWidth : Math.min(requestedWidth, viewportWidth);
  const height = constrained ? minHeight : Math.min(requestedHeight, viewportHeight);
  const requestedLeft = Math.max(0, numberOr(geometry.left, DEFAULT_GEOMETRY.left));
  const requestedTop = Math.max(0, numberOr(geometry.top, DEFAULT_GEOMETRY.top));
  const panelFits = options.hasPanel === true && width + panelWidth <= viewportWidth;
  const leftLimit = Math.max(0, viewportWidth - width - (panelFits ? panelWidth : 0));
  const left = Math.min(leftLimit, requestedLeft);
  const top = constrained
    ? 0
    : Math.min(Math.max(0, viewportHeight - height), requestedTop);
  const panelMode = options.hasPanel !== true
    ? 'none'
    : panelFits ? 'outside' : 'overlay';
  const panelLeft = panelMode === 'outside'
    ? left + width
    : Math.max(0, Math.min(left + width - panelWidth, viewportWidth - panelWidth));
  return {
    left, top, width, height, constrained,
    panel: {
      mode: panelMode,
      left: panelLeft,
      top: top + (panelMode === 'outside' ? 0 : headerHeight),
      width: panelWidth,
      maxHeight: Math.max(0, Math.min(height, viewportHeight - top) - (panelMode === 'outside' ? 0 : headerHeight)),
    },
  };
}

/** Управляет modeless <dialog>, перемещением и размером окна колонии. */
export function createPlanetWindow(dialog, { header, onLayout } = {}) {
  if (!dialog) throw new TypeError('Требуется элемент dialog.');
  // Масштабируем готовый интерфейс целиком; геометрия ниже остаётся в его локальных единицах.
  dialog.style.zoom = String(UI_SCALE);
  let resizeHandle = dialog.querySelector('[data-planet-resize]');
  if (!resizeHandle) {
    resizeHandle = document.createElement('button');
    resizeHandle.type = 'button';
    resizeHandle.dataset.planetResize = '';
    resizeHandle.className = 'pm-resize';
    resizeHandle.setAttribute('aria-label', 'Изменить размер окна колонии');
    resizeHandle.textContent = '◢';
    dialog.append(resizeHandle);
  }
  let restriction = dialog.querySelector('[data-planet-window-limit]');
  if (!restriction) {
    restriction = document.createElement('div');
    restriction.dataset.planetWindowLimit = '';
    restriction.setAttribute('role', 'status');
    restriction.textContent = 'Окно сохраняет минимум 300 × 320 CSS px, который больше viewport. Шапка закреплена сверху; изменение размера доступно у края окна.';
    dialog.prepend(restriction);
  }

  let destroyed = false;
  let dragging = null;
  let resizing = null;
  const listeners = [];
  const listen = (target, type, listener, options) => {
    target.addEventListener(type, listener, options);
    listeners.push(() => target.removeEventListener(type, listener, options));
  };
  const viewport = () => ({ width: window.innerWidth / UI_SCALE, height: window.innerHeight / UI_SCALE });
  const readStyle = (key, fallback) => {
    const parsed = Number.parseFloat(dialog.style[key]);
    return Number.isFinite(parsed) ? parsed : fallback;
  };
  const readGeometry = () => ({
    left: readStyle('left', savedGeometry.left), top: readStyle('top', savedGeometry.top),
    width: readStyle('width', savedGeometry.width), height: readStyle('height', savedGeometry.height),
  });
  const findPanel = () => dialog.querySelector('.planet-context');
  const headerHeight = () => {
    if (!header) return 40;
    const dialogTop = dialog.getBoundingClientRect().top;
    return Math.max(0, header.getBoundingClientRect().bottom - dialogTop) / UI_SCALE;
  };
  const apply = () => {
    if (destroyed) return null;
    const panel = findPanel();
    const result = calculatePlanetWindowGeometry(readGeometry(), viewport(), {
      hasPanel: Boolean(panel),
      headerHeight: headerHeight(),
    });
    dialog.style.position = 'fixed';
    dialog.style.margin = '0';
    dialog.style.left = `${result.left}px`;
    dialog.style.top = `${result.top}px`;
    dialog.style.width = `${result.width}px`;
    dialog.style.height = `${result.height}px`;
    dialog.style.minWidth = `${MIN_WIDTH}px`;
    dialog.style.minHeight = `${MIN_HEIGHT}px`;
    dialog.style.overflow = 'visible';
    restriction.hidden = !result.constrained;
    if (result.constrained) {
      resizeHandle.style.position = 'fixed';
      resizeHandle.style.right = '4px';
      resizeHandle.style.bottom = '4px';
    } else {
      resizeHandle.style.position = 'absolute';
      resizeHandle.style.right = '0';
      resizeHandle.style.bottom = '0';
    }
    if (panel) {
      panel.style.position = 'fixed';
      panel.style.left = `${result.panel.left}px`;
      panel.style.top = `${result.panel.top}px`;
      panel.style.width = `${result.panel.width}px`;
      panel.style.height = `${result.panel.maxHeight}px`;
      panel.style.maxHeight = `${result.panel.maxHeight}px`;
      panel.style.zIndex = '31';
    }
    savedGeometry = { left: result.left, top: result.top, width: result.width, height: result.height };
    onLayout?.(result);
    return result;
  };
  const endPointer = () => { dragging = null; resizing = null; };
  const movePointer = event => {
    if (dragging) {
      dialog.style.left = `${dragging.left + event.clientX / UI_SCALE - dragging.x}px`;
      dialog.style.top = `${dragging.top + event.clientY / UI_SCALE - dragging.y}px`;
      apply();
    } else if (resizing) {
      dialog.style.width = `${resizing.width + event.clientX / UI_SCALE - resizing.x}px`;
      dialog.style.height = `${resizing.height + event.clientY / UI_SCALE - resizing.y}px`;
      apply();
    }
  };
  const startDrag = event => {
    if (event.button !== 0 || event.target.closest?.('button,input,select,textarea,a,[contenteditable="true"]')) return;
    dragging = { x: event.clientX / UI_SCALE, y: event.clientY / UI_SCALE, ...readGeometry() };
    header?.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };
  const startResize = event => {
    if (event.button !== 0) return;
    resizing = { x: event.clientX / UI_SCALE, y: event.clientY / UI_SCALE, ...readGeometry() };
    resizeHandle.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };
  if (header) listen(header, 'pointerdown', startDrag);
  listen(resizeHandle, 'pointerdown', startResize);
  listen(window, 'pointermove', movePointer);
  listen(window, 'pointerup', endPointer);
  listen(window, 'pointercancel', endPointer);
  listen(window, 'resize', apply);
  listen(dialog, 'close', endPointer);

  return {
    open() {
      if (destroyed) return null;
      if (!dialog.open) dialog.show();
      return apply();
    },
    layout: apply,
    destroy() {
      if (destroyed) return;
      savedGeometry = readGeometry();
      destroyed = true;
      listeners.splice(0).forEach(remove => remove());
      resizeHandle.remove();
      restriction.remove();
    },
  };
}
