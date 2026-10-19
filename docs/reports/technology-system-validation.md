# Проверка системы технологий

14 октября 2026. Ветка `feature/technology-system`, база `3a226bf`; контракт [TECHNOLOGY-SYSTEM](../TECHNOLOGY-SYSTEM.md). Общая/stable ветка не изменялась. Проверка относится к ограниченному работающему срезу, не к полному native runtime Stellaris.

## Результат

- Source import: 679 определений, включая специальные; 738 проверяемых файлов (скрипты/inline/иконки). Сохранены исходные prerequisites, tiers, costs, repeatable levels, source weights/groups и точные gates. Три существующих ordinary presets сохраняют принятые 31 стартовые технологии.
- Исследования трёх направлений подключены к месячной транзакции, собственному резерву, ACK/replay/revision и POV. Прогресс сохраняется по технологии. Избыток теряется; автоматического выбора нет. Повторяемые уровни используют утверждённую пользователем формулу.
- Source upgrade edge открывается изученным набором. Старое здание работает до замены в том же слоте; отмена сохраняет его. Клиент показывает серверные цели улучшений из карточки установленного здания. Неподдержанные recurring contributions блокируют зависимый заказ до списания средств.
- UI: три цветных направления, соседняя панель выбора, список изученного с фильтром, прогресс/резерв/уведомление, pending/stale/read-only. Настоящие изображения читаются из локальной папки клиента, проверяются по SHA-256/длине, хранятся в ограниченной памяти; сервер не передаёт DDS.
- 679 технологий ссылаются на 674 разных DDS. Полный scan подтвердил hash/size и декодирование **674/674**. Добавлен только ограниченный BGR24 decoder иконок (два фактически обнаруженных варианта pitch/linear-size); общий форматный reader не менялся.

## Независимое ревью

Автор Core — `/root/research_audit`; backend — `/root/technology_integration`; UI — `/root/technology_ui` (Luna low согласно EXECUTION-POLICY). Root независимо прочитал реальные изменения всех трёх областей и проверил контракт/источники. Backend и Core работали с наследованными настройками; фактический backend-маршрут моделей и token usage не раскрыты инструментами.

Исправлены обнаруженные проблемы: UI скрывал upgrades занятых слотов; статус implemented unlock расходился между DTO/UI; selected offer ошибочно зависел от текущего веса; пустое направление не заполнялось после cross-area prerequisite; обновление фактов внутри Select порождало непубликуемую stale revision; отсутствовали BGR24 и pitch-вариант одной иконки. Подтверждённые изменения повторно прочитаны и проверены. Дополнительно восстановлен существующий диагностический construction fallback и сужены лишние зависимости importer, выявленные регрессионным набором; ожидания тестов не ослаблялись.

## Проверки

| Проверка | Результат |
|---|---|
| Release сборка Server/Core/Session | PASS, 0 предупреждений / 0 ошибок |
| ResearchChecks с настоящей 4.5.2 | PASS: математика резерва/избытка, переключение, repeatable levels, source rules, scoped facts, детерминизм/частоты, immutable candidates, review regressions |
| M3SessionChecks полный source набор | PASS: 15 базовых наборов, source startup/economy/construction и research integration |
| Повторный focused M3 source research после исправлений фактов | PASS: три пресета, source offers/прогресс; synthetic cost=1 в completion-тесте при настоящих definitions/layout/economy, настоящий lab upgrade/cancel/replacement |
| M3ServerChecks, настоящий HTTP/WebSocket сервер | 514 PASS: source выбор/прогресс, ACK/replay/stale/отказы, согласование stock/reserve; extra command fields закрывают malformed соединение |
| PlanetEconomyChecks | 7 PASS |
| ConstructionChecks после исправлений | 53 PASS, 0 FAIL, 1 SKIP: права создания symlink |
| Node client/panel/consumer/icon/DOM suites | 177 PASS, 0 FAIL / SKIP |
| Все mapped DDS | 674 PASS, 0 FAIL |
| Браузерные DOM-взаимодействия и реальная отрисовка DDS | PASS, 1920×1080 и 1366×768: выбор, активная тема, изученное, фильтр, Escape, отсутствие обрезания панелей |
| git diff --check | PASS |

Браузерный QA использует настоящие production DOM/image модули и локальные File-объекты через тестовую directory boundary; состояние науки синтетическое. Это не ручной полный цикл игрового клиента с импортом карты/GPU. DDS не раздавались HTTP и не сохранялись в tracked-файлах. Полные локальные логи и metadata scan находятся в игнорируемом `artifacts/`.

Первый запуск server checks оборвался **до исследования** на отрицательном входе protocol=1: существующий transport немедленно Abort после error-frame иногда даёт клиенту TCP reset вместо текста отказа. Один повтор без изменений transport прошёл все 514 проверок, включая новые research сценарии. Первый лог сохранён; эту нестабильность не выдаём за исправленную. Реальный TCP slow-reader exhaustion по-прежнему отдельно не проверен; controllable send-loop/deadline doubles проверены прежним набором.

Пользовательские референсы сохранены локально в игнорируемом `content-local/references/technology-system/`, без tracked PNG:

| Файл | SHA-256 |
|---|---|
| [reference-1.png](../../content-local/references/technology-system/reference-1.png) | `962CB395804A761F5F50E448B87FFF46AE8F7C472DBE25505B3AB189D0AAF53D` |
| [reference-2.png](../../content-local/references/technology-system/reference-2.png) | `268CA5D61C9E40D78E6EB0670B2143F3A31AF535496BD074921BA226050CC548` |
| [reference-3.png](../../content-local/references/technology-system/reference-3.png) | `BD8678FF7B81F11E281BA3D7CF276F73B07DAC2074C818E0957B99730105B665` |
| [reference-4.png](../../content-local/references/technology-system/reference-4.png) | `4BD9E951E0C239823B8B517A9DF4E8A09F67954B632480C6B9B226AD10F7F543` |

## Точное покрытие и остаточные условия

На старте каждого пресета: 27 eligible определений, три предложения в каждом направлении. Для всех сейчас legality-valid обычных технологий неизвестные веса устранены; 14 других legality-valid имеют исходный нулевой вес, в том числе lab2 без gas-tech/market и blocker-технологии при текущих условиях мира. Случайные начальные IDs могут отличаться; предложения устойчивы внутри сессии.

252 определения полного каталога имеют неизвестные зависимости; 136 имеют неизвестный potential, 120 — заведомо ложный potential. Среди оставшихся dependencies — DLC/host flags, специальные authority/origin/event conditions, будущие счётчики и callbacks. Это не 252 обычные доступные стартовые технологии. Все причины публикуются в `research.empires[].unavailable`; неизвестность не заменяется `true` или фиктивным нулём. Полное vanilla покрытие всех будущих обычных/DLC ветвей не подтверждено.

Численные modifiers, ship/components, edicts и прочие эффекты вне реализованного строительного unlock остаются отложенными и обозначены в UI. Empire-size cost stack, лидеры и их бонусы, tier-repeatable native indexing вне принятой prototype формулы, полный DLC-state, сохранения/межсессионное восстановление и ручная приёмка полноценной игры не закрыты этой поставкой. Иконки требуют directory mode; прежний режим ограниченных отдельных File snapshots не даёт доступ к новым изображениям.

Требуется отдельная ручная приёмка ветки перед интеграцией в stable по действующей политике. Коммит не заменяет её.

## Запуск и ручная проверка

Из корня этой ветки, задав `STELLARIS_GAME_ROOT` своей установкой 4.5.2:

```powershell
dotnet run --project src/Stellaris.Server -c Release -- --mode m3 --game-root "$env:STELLARIS_GAME_ROOT" --urls http://127.0.0.1:5198
```

Код доступа печатается сервером. Подключить два Chrome-клиента, каждому импортировать собственную папку игры и выбрать preset, отметить готовность, начать сессию. Открыть «Технологии», выбрать темы независимо в трёх направлениях, проверить прогресс после месяца и сохранение при переключении/переподключении. Проверить список изученного, отложенные эффекты и реальные иконки. Для upgrade сохраняются **настоящие** technology/resource/allow gates: изучение не выдаёт недостающие стратегические ресурсы, не отменяет исходный нулевой вес и не обходит отсутствующие механики.
