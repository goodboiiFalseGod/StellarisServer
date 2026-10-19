const $ = id => document.getElementById(id);
const storageKey = "stellaris.m2.tab";
const format = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 6 });
const resourceNames = { energy: "Энергия", minerals: "Минералы", food: "Пища" };
let saved = readSaved();
let socket = null;
let state = null;
let welcomed = false;
let synchronized = false;
let stopped = true;
let reconnectTimer = null;
let retries = 0;
let activeSessionId = saved.sessionId ?? null;
let sequence = -1;
let chosenPlanet = null;
let jobsPlanet = null;
let configDraft = null;
let sendTimer = null;
let livenessTimer = null;
let lastResponseAt = 0;
const responseTimeout = 30000;
const sentOnConnection = new Set();
const pending = new Map((saved.pending ?? []).map(command => [command.commandId, command]));
const commandRows = new Map();

$("name").value = saved.name ?? "";
$("join-code").value = saved.joinCode ?? "";
$("role").value = saved.role ?? "player";
$("connect").textContent = saved.token ? "Вернуться" : "Подключиться";
for (const command of pending.values()) showCommand(command.commandId, command.type, "pending", "Ожидает повторного подтверждения");

function readSaved() {
  try { return JSON.parse(sessionStorage.getItem(storageKey) ?? "{}"); }
  catch { return {}; }
}

function persist() {
  saved.pending = [...pending.values()];
  try { sessionStorage.setItem(storageKey, JSON.stringify(saved)); }
  catch { showError("Браузер запретил сохранение входа. После перезагрузки возврат может быть недоступен."); }
}

function text(tag, value, className) {
  const element = document.createElement(tag);
  element.textContent = value;
  if (className) element.className = className;
  return element;
}

function showError(message) {
  $("error").textContent = message;
  $("error").hidden = !message;
}

function online() { return checkLiveness() && welcomed && synchronized && socket?.readyState === WebSocket.OPEN; }

function checkLiveness() {
  if (!socket) return false;
  if (performance.now() - lastResponseAt < responseTimeout) return true;
  showError("Сервер не отвечает 30 секунд. Последнее состояние устарело.");
  invalidateConnection();
  return false;
}

function invalidateConnection() {
  const previous = socket;
  socket = null;
  welcomed = false;
  synchronized = false;
  clearInterval(livenessTimer);
  livenessTimer = null;
  cancelSending();
  clearTimeout(reconnectTimer);
  reconnectTimer = null;
  if (!stopped && saved.token) {
    const delay = Math.min(1000 * 2 ** Math.min(retries++, 3), 8000);
    reconnectTimer = setTimeout(connect, delay);
    connectionStatus(`Связь потеряна. Возврат через ${delay / 1000} с…`);
  } else connectionStatus("Отключено");
  // Не ждём close: при потере сети событие может вообще не прийти.
  previous?.close();
}

function connectionStatus(message) {
  $("connection-status").textContent = message;
  $("stale").hidden = !state || online();
  $("disconnect").disabled = !socket && !reconnectTimer;
  $("connect").disabled = !!socket || !!reconnectTimer;
  for (const id of ["name", "join-code", "role"]) $(id).disabled = !!saved.token || !!socket;
  render();
}

$("join-form").addEventListener("submit", event => {
  event.preventDefault();
  if (!saved.token) {
    saved.name = $("name").value.trim();
    saved.joinCode = $("join-code").value.trim();
    saved.role = $("role").value;
  }
  if (!saved.name || !saved.joinCode) { showError("Укажите имя и код доступа."); return; }
  persist();
  stopped = false;
  retries = 0;
  connect();
});

function connect() {
  clearTimeout(reconnectTimer);
  reconnectTimer = null;
  welcomed = false;
  synchronized = false;
  cancelSending();
  sentOnConnection.clear();
  showError("");
  const current = new WebSocket(`${location.protocol === "https:" ? "wss:" : "ws:"}//${location.host}/ws`);
  socket = current;
  lastResponseAt = performance.now();
  livenessTimer = setInterval(checkLiveness, 1000);
  connectionStatus("Подключение…");
  current.addEventListener("open", () => {
    if (socket !== current || !checkLiveness()) return;
    current.send(JSON.stringify({ type: "join", protocol: 1, name: saved.name, role: saved.role, joinCode: saved.joinCode, ...(saved.token ? { token: saved.token } : {}) }));
    connectionStatus("Проверка входа…");
  });
  current.addEventListener("message", event => {
    if (socket !== current || !checkLiveness()) return;
    try {
      if (receive(JSON.parse(event.data)) && socket === current) lastResponseAt = performance.now();
    }
    catch (error) { showError(`Ошибка сообщения сервера: ${error.message}`); stopped = true; invalidateConnection(); }
  });
  current.addEventListener("error", () => { if (socket === current) showError("Не удалось установить связь с сервером."); });
  current.addEventListener("close", () => {
    if (socket !== current) return;
    invalidateConnection();
  });
}

function receive(message) {
  if (message.type === "welcome") {
    if (message.protocol !== 1 || typeof message.sessionId !== "string" || typeof message.token !== "string" || !Number.isInteger(message.participantId)) throw new Error("Неверное сообщение входа или версия протокола.");
    if (activeSessionId !== message.sessionId) {
      state = null; chosenPlanet = null; jobsPlanet = null; configDraft = null;
      $("traits-options").replaceChildren();
      $("buildings-options").replaceChildren();
    }
    sequence = -1;
    activeSessionId = message.sessionId;
    saved = { ...saved, token: message.token, sessionId: message.sessionId, participantId: message.participantId };
    welcomed = true;
    retries = 0;
    persist();
    $("identity").textContent = `Участник №${message.participantId}. Вход сохранён только в этой вкладке.`;
    $("connect").textContent = "Вернуться";
    connectionStatus("Подключено · синхронизация…");
  } else if (message.type === "snapshot") {
    const next = message.state;
    if (!welcomed || next.sessionId !== activeSessionId || next.protocol !== 1 || !Number.isSafeInteger(next.sequence)) return false;
    if (next.sequence <= sequence) return true;
    sequence = next.sequence;
    state = next;
    synchronized = true;
    connectionStatus("Подключено");
    scheduleSending();
  } else if (message.type === "result") {
    if (!welcomed || typeof message.result?.commandId !== "string" || typeof message.result.accepted !== "boolean") return false;
    const result = message.result;
    const command = pending.get(result.commandId);
    pending.delete(result.commandId);
    persist();
    showCommand(result.commandId, command?.type ?? "Команда", result.accepted ? "accepted" : "rejected", result.message || (result.accepted ? "Принято сервером" : "Отклонено"));
    render();
  } else if (message.type === "error") {
    if (typeof message.message !== "string") return false;
    showError(message.message || "Сервер отклонил запрос.");
    if (!welcomed) { stopped = true; invalidateConnection(); }
  } else if (message.type === "pong") {
    if (!welcomed) return false;
  } else throw new Error("Неизвестный тип сообщения.");
  return true;
}

$("disconnect").addEventListener("click", () => {
  stopped = true;
  invalidateConnection();
});

$("clear-login").addEventListener("click", () => {
  if (!confirm("Удалить токен этой вкладки? Вернуться к прежней империи без него будет невозможно.")) return;
  stopped = true;
  invalidateConnection();
  state = null;
  activeSessionId = null;
  sequence = -1;
  saved = {};
  pending.clear();
  commandRows.clear();
  $("commands").replaceChildren();
  configDraft = null;
  jobsPlanet = null;
  $("traits-options").replaceChildren();
  $("buildings-options").replaceChildren();
  sessionStorage.removeItem(storageKey);
  $("identity").textContent = "";
  $("connect").textContent = "Подключиться";
  showError("");
  connectionStatus("Вход очищен");
});

setInterval(() => { if (online()) socket.send(JSON.stringify({ type: "ping" })); }, 10000);
document.addEventListener("visibilitychange", checkLiveness);
window.addEventListener("pageshow", checkLiveness);

function send(type, args = {}) {
  if (!online()) { showError("Команда недоступна без связи с сервером."); return; }
  if (pending.size >= 100) { showError("Очередь из 100 команд заполнена. Дождитесь подтверждений сервера."); return; }
  const commandId = [...crypto.getRandomValues(new Uint8Array(16))].map(value => value.toString(16).padStart(2, "0")).join("");
  const command = { commandId, type, ...args };
  pending.set(commandId, command);
  persist();
  showCommand(commandId, type, "pending", "Ожидает подтверждения");
  scheduleSending();
  render();
}

function cancelSending() {
  clearTimeout(sendTimer);
  sendTimer = null;
}

function scheduleSending() {
  if (sendTimer !== null || !online()) return;
  // Единый лимит 8 команд/с для новых команд и повторов после свежего снапшота.
  sendTimer = setTimeout(() => {
    sendTimer = null;
    if (!online()) return;
    const command = [...pending.values()].find(item => !sentOnConnection.has(item.commandId));
    if (!command) return;
    sentOnConnection.add(command.commandId);
    try { socket.send(JSON.stringify(command)); }
    catch { showError("Отправка прервана. Команда сохранена для возврата с прежним ID."); socket?.close(); return; }
    scheduleSending();
  }, 125);
}

function showCommand(id, type, status, message) {
  let row = commandRows.get(id);
  if (!row) { row = text("li", ""); commandRows.set(id, row); $("commands").prepend(row); }
  row.className = status;
  row.textContent = `${type}: ${message}`;
  row.dataset.commandId = id;
  while ($("commands").children.length > 40) {
    const oldest = $("commands").lastElementChild;
    commandRows.delete(oldest.dataset.commandId);
    oldest.remove();
  }
}

function render() {
  $("session").hidden = !state;
  $("stale").hidden = !state || online();
  if (!state) return;
  const me = state.participants.find(person => person.id === state.you);
  const can = online();
  const player = me?.role === "player";
  const host = !!me?.isHost;
  const lobby = state.phase === "lobby";
  const ownPlanet = state.planets.find(planet => planet.ownerId === state.you);
  $("phase").textContent = lobby ? "Лобби" : "Сессия";
  $("month").textContent = `Месяц ${state.month}`;
  $("timing").textContent = `${state.paused ? "Пауза" : "Время идёт"} · ${state.speed} месяц/с · последний расчёт ${format.format(state.lastTickMilliseconds)} мс`;
  $("session-error").textContent = state.lastError ?? "";
  $("session-error").hidden = !state.lastError;
  $("host-controls").hidden = !host || lobby;
  $("pause").textContent = state.paused ? "Продолжить" : "Пауза";
  $("pause").disabled = !can;
  $("step").disabled = !can || !state.paused;
  $("speed").disabled = !can;
  $("speed").value = String(state.speed);
  $("observer-status").textContent = me?.role === "pendingObserver" ? "Ожидайте разрешения хоста. Экономика пока скрыта." : me?.role === "observer" ? "Наблюдатель: видны все планеты, управление недоступно." : "Видна только собственная экономика.";
  renderParticipants(host, can, lobby);
  $("lobby").hidden = !lobby || !player;
  if (lobby && player && ownPlanet) {
    if (!configDraft) configDraft = { traits: [...ownPlanet.traits], buildings: [...ownPlanet.buildings] };
    renderChoices("traits", state.catalog.traits, configDraft.traits, can);
    renderChoices("buildings", state.catalog.buildings, configDraft.buildings, can);
  }
  $("configure").disabled = !can;
  $("ready").textContent = me?.ready ? "Снять готовность" : "Готов";
  $("ready").disabled = !can;
  $("start").hidden = !host;
  const players = state.participants.filter(person => person.role === "player");
  $("start").disabled = !can || players.length === 0 || !players.every(person => person.connected && person.ready);
  $("economy").hidden = !state.planets.length;
  if (state.planets.length) renderPlanet(can && player && !lobby);
}

function renderParticipants(host, can, lobby) {
  $("participants").replaceChildren();
  for (const person of state.participants) {
    const role = person.role === "player" ? "игрок" : person.role === "observer" ? "наблюдатель" : "ожидает разрешения";
    const row = text("li", `${person.name}${person.id === state.you ? " (вы)" : ""} · ${role}${person.isHost ? " · хост" : ""}`);
    row.append(text("div", `${person.connected ? "В сети" : person.afk ? "AFK" : "Не в сети"}${lobby && person.role === "player" ? person.ready ? " · готов" : " · не готов" : ""}`, "hint"));
    const actions = document.createElement("div");
    actions.className = "actions";
    if (host && person.role === "pendingObserver" && lobby) actions.append(action("Разрешить наблюдение", "approveObserver", { targetParticipantId: person.id }, !can, `approve-observer-${person.id}`));
    if (host && person.role === "player" && person.id !== state.you) actions.append(action("Передать права хоста", "transferHost", { targetParticipantId: person.id }, !can || !person.connected, `transfer-host-${person.id}`));
    row.append(actions);
    $("participants").append(row);
  }
}

function action(label, type, args, disabled, testId) {
  const button = text("button", label);
  button.type = "button";
  button.disabled = disabled;
  button.dataset.testid = testId;
  button.addEventListener("click", () => send(type, args));
  return button;
}

function renderChoices(kind, catalog, selected, can) {
  const container = $(`${kind}-options`);
  if (!container.children.length) {
    for (const item of catalog) {
      const label = document.createElement("label");
      label.className = "choice";
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = item.id;
      checkbox.dataset.testid = `${kind}-${item.id}`;
      checkbox.addEventListener("change", () => { configDraft[kind] = [...container.querySelectorAll("input:checked")].map(input => input.value); });
      const info = text("span", item.name);
      info.append(text("small", `${item.id} · ${item.source}`));
      label.append(checkbox, info);
      container.append(label);
    }
  }
  for (const input of container.querySelectorAll("input")) { input.checked = selected.includes(input.value); input.disabled = !can; }
}

function renderPlanet(editable) {
  if (!state.planets.some(planet => planet.id === chosenPlanet)) chosenPlanet = state.planets[0].id;
  const picker = $("planet-picker");
  if (picker.options.length !== state.planets.length || [...picker.options].some((option, index) => Number(option.value) !== state.planets[index].id)) {
    picker.replaceChildren(...state.planets.map(planet => { const option = text("option", planet.name); option.value = planet.id; return option; }));
  }
  picker.value = String(chosenPlanet);
  $("planet-picker-label").hidden = state.planets.length < 2;
  const planet = state.planets.find(item => item.id === chosenPlanet);
  $("planet-name").textContent = planet.name;
  $("population").textContent = `Население: ${format.format(planet.population)} · обычная Workforce: ${format.format(planet.workforce)}`;
  $("balances").replaceChildren(...Object.entries(planet.balances).map(([id, amount]) => { const card = text("div", "", "balance"); card.append(text("span", resourceNames[id] ?? id), text("strong", format.format(amount))); return card; }));
  const report = planet.report;
  $("report-status").textContent = report ? `Последний успешный расчёт: месяц ${planet.reportMonth ?? state.month}. Вакансии и настройки могут ожидать следующего месяца.` : "Ещё не рассчитано. Первый отчёт появится после шага месяца.";
  if (jobsPlanet !== planet.id) {
    jobsPlanet = planet.id;
    $("jobs").replaceChildren();
    for (const job of state.catalog.jobs) {
      const row = document.createElement("tr");
      row.dataset.job = job.id;
      const name = text("td", job.name);
      name.title = job.source;
      const editor = document.createElement("td");
      const wrap = document.createElement("div");
      wrap.className = "job-editor";
      const input = document.createElement("input");
      input.type = "number"; input.min = "0"; input.max = "6"; input.step = "0.5";
      input.setAttribute("aria-label", `Вакансии: ${job.name}`);
      input.dataset.testid = `jobs-${job.id}`;
      const button = text("button", "Применить");
      button.type = "button";
      button.dataset.testid = `set-jobs-${job.id}`;
      button.addEventListener("click", () => { if (input.reportValidity() && input.value !== "") send("setJobs", { planetId: planet.id, job: job.id, count: Number(input.value) }); });
      wrap.append(input, button); editor.append(wrap);
      row.append(name, editor, text("td", "—"), text("td", "—"), text("td", "—"));
      $("jobs").append(row);
    }
  }
  for (const row of $("jobs").children) {
    const job = row.dataset.job;
    const input = row.querySelector("input");
    if (document.activeElement !== input) input.value = planet.jobs[job] ?? 0;
    input.disabled = !editable;
    row.querySelector("button").disabled = !editable;
    const allocation = report?.allocated[job];
    for (const [index, key] of [[2, "workforce"], [3, "bonusWorkforce"], [4, "effectiveWorkforce"]]) row.children[index].textContent = allocation ? format.format(allocation[key]) : "—";
  }
  $("flows").replaceChildren();
  for (const resource of Object.keys(planet.balances)) {
    const row = document.createElement("tr");
    row.append(text("td", resourceNames[resource] ?? resource));
    for (const key of ["produced", "jobUpkeep", "buildingUpkeep", "upkeep", "net"]) row.append(text("td", report ? format.format(report[key][resource] ?? 0) : "—"));
    $("flows").append(row);
  }
  $("unemployed").textContent = report ? `Незанятая Workforce: ${format.format(report.unemployedWorkforce)}` : "";
  renderSelected("planet-traits", planet.traits, state.catalog.traits);
  for (const [job, multiplier] of Object.entries(planet.bonusMultipliers)) $("planet-traits").append(text("li", `${state.catalog.jobs.find(item => item.id === job)?.name ?? job}: серверная доля бонуса ${format.format(multiplier)}`));
  renderSelected("planet-buildings", planet.buildings, state.catalog.buildings);
}

function renderSelected(target, ids, catalog) {
  $(target).replaceChildren(...ids.map(id => { const item = catalog.find(entry => entry.id === id); return text("li", item ? `${item.name} · ${item.source}` : id); }));
  if (!ids.length) $(target).append(text("li", "Не выбраны"));
}

$("configure-form").addEventListener("submit", event => { event.preventDefault(); if (configDraft) send("configure", { traits: configDraft.traits, buildings: configDraft.buildings }); });
$("ready").addEventListener("click", () => send("ready", { ready: !state.participants.find(person => person.id === state.you).ready }));
$("start").addEventListener("click", () => send("start"));
$("pause").addEventListener("click", () => send("pause", { paused: !state.paused }));
$("step").addEventListener("click", () => send("step"));
$("speed").addEventListener("change", () => send("speed", { speed: Number($("speed").value) }));
$("planet-picker").addEventListener("change", () => { chosenPlanet = Number($("planet-picker").value); render(); });
connectionStatus(saved.token ? "Сохранён вход. Нажмите «Вернуться»." : "Не подключено");
