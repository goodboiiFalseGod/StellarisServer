# M3 — проверки импорта

12 октября 2026. Это отчёт, не изменение [контракта](M3-ASSET-MANIFEST.md).

## BGRA8 UI DDS

В `tools/asset-probe/formats.mjs` добавлен ограниченный BGRA8 v1: точные masks/32bpp, один mip, pitch с padding либо tightly packed linear-size, straight alpha. Неверные layout, flags, размеры и длины отклоняются до выделения выходного массива. Лимиты стенда и прежний DXT/PDX-путь сохранены.

Основной агент повторил `node --test tools/asset-probe/formats.test.mjs tools/asset-probe/adapters.test.mjs` с `STELLARIS_GAME_ROOT`, указывающим на локальную 4.5.2: **15/15 PASS**, без SKIP. Включены synthetic BGRA8 и пять настоящих UI DDS: energy/minerals/food (18×18), fleet_size_icon (19×19), planet (33×23). Для каждого пикселя проверены перестановка B/R и сохранение G/alpha относительно исходного payload. Прежние PDX/DXT/material-adapter тесты также прошли. `node --check formats.mjs` и `git diff --check` — без ошибок; предупреждение Git о LF→CRLF не является ошибкой тестов.

Без установки разработчик также проверил 13 PASS/2 SKIP. Оригинальные файлы не добавлены в репозиторий, установка не изменялась.

Независимый ревьюер сверил diff с Gate и повторил 15/15 тестов на реальном контенте; существенных замечаний нет. Основной агент также проверил ветвление декодера, границы, padding и сохранение alpha.

## Что ещё не проверено

Это проверка декодера, **не визуальная приёмка и не готовый M3**. UI-иконки ещё не показаны в игровом Chrome-клиенте. Bind-pose adapter планеты/звезды, материалы/слои, полный manifest, folder-access lifecycle, worker/atomic replacement, память и FPS остаются отдельными этапами. Исторический ASSET-PROBE-REPORT описывает прежний эксперимент и не подменяется этим отчётом.

## Статические reader — следующий этап

Добавлены проверяемая rigid bind pose, явный locator-only режим PDX и ограниченный текстовый AST/helper для .asset/.gfx/.txt. Основной агент выполнил объединённые пять файлов тестов на локальной 4.5.2: **31/31 PASS, 0 SKIP**, включая прежние 15 проверок. У planet сохранены 5 слоёв/4 skin, у star 2/1; исходные p/n/ta/UV неизменны. Исследовательский тест original idle TRS×tx≈Identity подтверждает inverse global bind трактовку; анимации не входят в runtime-профиль. Независимое ревью static-pose и повтор 8/8 — без существенных замечаний.

Исправлена ошибка раннего выбора станции: современный outpost собирается из locator-only frame 88B и трёх render submesh (stations/corvette/battleship), а не legacy military_station_small. Контракт обновлён до profileId=m3-static-452-v2 до интеграции; проверены core→part1, ссылки root/section/.gfx и отсутствие skin. Названия одинаковых submesh не схлопываются. Браузерная визуальная приёмка и production importer ещё не завершены; этот раздел уточняет прогресс предыдущего, не объявляет весь M3 готовым.

## Статический профиль и отдельный preview

Добавлены `m3-profile.mjs`, отдельная страница `m3.html`/Three renderer и lifecycle UI. Полный объединённый Node-прогон с локальной 4.5.2: **61/61 PASS, 0 SKIP**, включая 10 прежних M2 connection regressions, 11 тестов preview UI, 4 memory guards и реальные модели/definitions/иконки. Независимое ревью профиля выявило неучтённые converted normal/specular arrays; исправлено резервированием candidate+retained до allocations. Перед build учитывается retained conversion; перед renderer load — оба CPU-профиля. Повторный импорт не обязан помещаться в лимит, отказ сохраняет прежний набор. Tests проверяют передачу обоих бюджетов, уникальные path:mode и точные границы.

Предварительный осмотр **во встроенном браузере Codex, не Google Chrome**: 44 локальных File, profile v2, raw decoded 228123324 B (217,555 MiB), первичное чтение/hash/decode около 4,6–5,9 с. Показаны корвет (1 слой), современный outpost (3), планета (5/4 skin), звезда (2/1 skin); разорванной геометрии/сдвига слоёв не наблюдалось. Материалы явно approximate; звезда — одноцветная поверхность с отдельным rim, не ванильный VFX. Иконки показаны на двух фонах; консоль без warning/error. Очистка звезды дала geometries/textures=0, пересоздание вернуло 2/0. Неполный импорт сохранил планету и иконки. Это наблюдения стенда, не SLA и не закрытие Chrome gate.

UI tests исполняют фактический app source с заменой двух imports: ошибки icons/GPU, clear/reload, pagehide/pageshow, context loss, отмена delayed file/hash/folder traversal и budgets. Реальный WebGL context-loss, folder-permission/revocation и десять lifecycle-циклов требуют отдельной приёмки. Preview пока синхронно декодирует набор и не является production worker importer. Google Chrome недоступен текущему каналу управления; инструкция ручного gate — tools/asset-probe/README.md. Исходные ассеты и screenshots не добавлялись в Git.

Реальная повторная загрузка полного набора при удерживаемой планете после исправления отклонена `converted memory budget`; прежняя сцена сохранила 5 geometry/12 textures, convertedBytes=58753152. Таким образом прежний пик около 547 MiB больше не разрешается молча.
