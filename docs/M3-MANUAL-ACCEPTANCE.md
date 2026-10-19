# Чек-лист самостоятельной приёмки M3

Updated for the reference revision on 12 October 2026, based on final production `bccaddc` (63-file v5 profile, full-viewport HUD, personal perspectives and measured panel label exclusion), verified in stable main at `88338b8`. The final report-only stable-main commit is supplied with the testing notification; record the actual checkout hash. This checklist is not a PASS report. See the [visual comparison and limitations](reports/m3-visual-milestone-validation.md); MAP-001/CAM-001 still require user confirmation.

## Personal outliner and observer views

After host approval, the observer starts in general view with no personal outliner. Use the observer selector to choose each player, then general view again. Wait for the acknowledged server snapshot: the outliner/resources must change to that player's own assets/economy, prior selection/details must clear, and the observer must remain read-only with its original identity. An offline player remains selectable. In the two player tabs, outliners must contain only their own colonies/fleets/supported owned objects; map navigation remains separate. Check reconnect preserves the observer's selected POV after reimport/readiness. Public system positions may be identical; foreign economy links and movement orders must remain redacted in a player POV. Contract: [personal perspectives](M3-PLAYER-PERSPECTIVES.md).

Compare all four named camera presets at 1280x720 and 1920x1080 game viewports: no page scrolling, readable overlays, coherent source-backed sky/core/dust, relative body sizes and contiguous star corona. Renderer composition and core color transfer are documented browser calibrations, not exact recovered native rendering. Repeat physical Alt-drag after panning and selecting objects; record unresolved conditions separately from automated checks.

Основной сценарий относится к игровому клиенту M3. Отдельный статический стенд сохранён ниже для диагностики моделей. Требования и подробности: [запуск](M3-RUN-AND-TEST.md), [fixture](M3-WORLD-FIXTURE.md), [импорт](M3-LOCAL-ASSET-IMPORT.md), [интерфейс](M3-CLIENT-INTERACTIONS.md). Фактические автоматические результаты хранятся отдельно в [интеграционном отчёте](M3-INTEGRATION-VALIDATION.md).

Будущие проверки оригинальной игры: [отложенный пакет строительства 4.5.2](CONSTRUCTION-452-MANUAL-ACCEPTANCE.md). Он не входит в объём M3 и не требует проверки сейчас.

## Подготовка и запуск

Записать фактический коммит, ОС, версию Google Chrome, CPU/RAM/GPU и размер viewport. Проверять на одном ПК, localhost, с собственной Stellaris **4.5.2**. Не переключать ветку и не сбрасывать чужие изменения ради проверки; если checkout изменился, сначала сверить актуальную инструкцию запуска. Не запускать второй сервер на занятом порту.

Record the exact game version/checksum, DLC, playset and mods. Matching the **63** manifest files confirms only profile `m3-static-452-v5`, not the entire installation or all native mechanics. Explicitly mark unverified reference purity; modded results are not vanilla evidence. Available combined visual QA and stable-main build/startup checks passed; subsequent user acceptance remains separate.

В PowerShell выполнить по очереди:

```powershell
Set-Location -LiteralPath '..'
git rev-parse --short HEAD
git status --short
pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts
dotnet build src/Stellaris.Server -c Release
dotnet run --no-build --project src/Stellaris.Server -c Release -- --mode m3 --game-root "C:\Path\To\Stellaris" --urls http://127.0.0.1:5089 --join-code M3-local-test-code
```

Оставить терминал работающим. На `http://127.0.0.1:5089/health` ожидаются `protocol: 2`, `mode: "m3"`. Клиент: `http://127.0.0.1:5089/m3.html`. Отдельный preview-сервер не требуется.

Открыть три новые, недублированные вкладки: A — первый игрок/хост, B — второй игрок, O — наблюдатель. Каждая вкладка отдельно выбирает папку игры и завершает локальный импорт. Войти с разными именами и кодом `M3-local-test-code`. Хост разрешает наблюдение **до старта**; затем оба игрока подтверждают готовность, хост запускает.

## Основной игровой проход

Для каждого ID записывать PASS / FAIL / BLOCKED / не проверено. Сейчас все строки имеют статус **не проверено пользователем**.

| ID | Действие | Ожидаемый результат |
|---|---|---|
| G01 | Cancel folder selection, then allow read-only access to local 4.5.2. | Cancellation completes; successful import validates all 63 files. Missing/incompatible files produce a visible rejection. Replacing an active complete profile exceeds the bounded staging budget: visible refusal preserves current assets; reload the page before importing again. |
| G02 | Проверить неверный код, готовность, разрешение O и запуск. | Неверный вход отклонён; без готовности нет старта. O до разрешения не получает мир/экономику. |
| G03 | Открыть галактику, S01 и S16. | 16 систем, 24 гиперлинии; у каждого игрока колония, станция и флот из трёх корветов. |
| G04 | Осмотреть модели и пять иконок. | Корвет — 1 слой, outpost — 3, continental — 5, G-звезда — 2. Нет разрывов/сдвигов слоёв, чёрной сцены или пропавших моделей. Иконки энергии/минералов/пищи/флота/планеты имеют корректные прозрачные края. Материалы статические, приближённые. |
| G05 | Проверить выбор, двойной клик, drag, Alt+drag, колесо, resize, сброс/центр камеры, Escape. | Камера и навигация работают по инструкции; drag не выбирает объект случайно. Ввод X/Z не двигает камеру. Выбранный командный флот сохраняется при выборе назначения. |
| G06 | На паузе дать своему флоту X=20, Z=-80 и шагать днями. Затем дать X=151, Z=0. | Движение только по серверным дням, скорость 7,2 единицы/день; прибытие точно в цель. Недопустимая точка отклонена без clamp и потери прежнего приказа. |
| G07 | Заменить локальную цель, затем нажать «Стоп». Повторить приказ ПКМ. | Цель заменяется; stop сохраняет текущую позицию. Все три клиента видят согласованное положение. |
| G08 | Отправить флот в другую систему через DOM и ПКМ в галактике. | Фазы approachingExit → charging → inTransit → прибытие; подготовка 15 дней, переход 5 дней на ребро плюс подлёт. |
| G09 | Во время inTransit дать stop/другую систему. | Приказ отложен до прибытия, без телепортации; локальная точка недоступна. В системе транзитный флот скрыт, в галактике виден между endpoints. |
| G10 | Проверить паузу, шаг, скорости 1/2/4 дня/с. От старта сделать 30 шагов. | Старт 2200.01.01; после 30 шагов 2200.02.01 и ровно один месячный расчёт. Дата совпадает у всех; глобальное управление только у хоста. |
| G11 | Inspect a foreign planet/fleet as player; check observer O. | Public objects remain visible; foreign economy and commands stay private. O sees both economies only in general view, or only the selected player's economy in POV, always read-only. |
| G12 | Проверить вакансии 2 → 1 → 0.5 → 0 на последовательных границах месяцев. | Отчёт обновляется на границе месяца, расходы профессий/зданий разделены; общий расход равен их сумме. Числа контрольного сценария приведены ниже. |
| G13 | Отключить B во время движения, вернуть; повторить с reload. | Старые данные отмечены устаревшими, приказы заблокированы; сервер продолжает прежний приказ. Возврат к тому же участнику/флоту без дублей. После reload нужен новый импорт. |
| G14 | Отключить хоста на паузе и во время хода; отдельно передать права B. | Пауза/скорость сохраняются, автоматического нового хоста нет. При передаче права получает B, A теряет глобальное управление. AFK после 60 реальных секунд допускает возврат. |
| G15 | Реально вызвать потерю WebGL-контекста и восстановить его. | Видимая диагностика и восстановление сцены; приказы ждут свежего snapshot, самостоятельные игровые приказы не отправляются. |
| G16 | Отозвать разрешение папки; затем reload и повторный импорт. | Уже загруженный набор доступен текущей сессии; новые чтения требуют разрешения. Reload требует нового импорта. |
| G17 | Проверить Network и хранилища браузера. | Игровые файлы не отправляются серверу; нет постоянного кеша ассетов в IndexedDB/Cache Storage или производных пакетов на диске. Токен входа в sessionStorage не является кешем ассетов. |
| G18 | Последним остановить сервер Ctrl+C и запустить заново. | Старый мир утрачен, токены отклонены. Очистка входа и новый вход создают новую партию; сохранения отсутствуют. |

### Контрольные значения экономики

Свежая партия: A выбирает только «Изобретательные» и «Энергосеть», B оставляет пустые настройки. Начальные запасы 1000. Менять только вакансии техников A; каждую строку рассчитывать на следующей границе месяца. В M3 месяц — 30 дней, а не один шаг.

| Расчётный месяц | Вакансии A | W / бонусная W | Выпуск энергии | JobUpkeep | BuildingUpkeep | Чистый поток | Энергия A | Энергия B |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 2 | 200 / 30 | 14.95 | 0 | 2 | 12.95 | 1012.95 | 1012 |
| 2 | 1 | 100 / 15 | 7.475 | 0 | 2 | 5.475 | 1018.425 | 1024 |
| 3 | 0.5 | 50 / 7.5 | 3.7375 | 0 | 2 | 1.7375 | 1020.1625 | 1036 |
| 4 | 0 | 0 / 0 | 0 | 0 | 2 | −2 | 1018.1625 | 1048 |

Отображение может округляться, расчёт не должен. В этом наборе JobUpkeep нулевой: ручной сценарий не доказывает масштабирование ненулевого содержания профессии. Такие формулы проверяются отдельными автоматическими тестами.

## Связь и фоновые вкладки

- Активная вкладка на паузе 45 секунд: неизменная дата сама по себе не вызывает потерю связи.
- Реальный blackhole без FIN/RST и раннего close: после 30 секунд без корректного ответа последнее состояние устаревшее, команды запрещены, запускается возврат. Восстановление доставки возвращает прежнего участника и свежий snapshot без дублей. Обычный обрыв с close проверяет другой случай. Отключение Wi-Fi не нарушает loopback-связь.
- Скрыть вкладку более чем на 45 секунд и вернуть. Если ответы обрабатывались, связь может сохраниться; если срок истёк, до отправки команды нужна проверка и синхронизация.
- Реальная заморозка проверяется отдельно: точное срабатывание таймера во время freeze не требуется; после возобновления просроченное соединение нельзя считать свежим.
- Если способ blackhole/freeze или потери ACK не обеспечен, соответствующий случай остаётся «не проверено». Отсутствие видимых дублей не заменяет воспроизведение потерянного подтверждения.

## Ресурсы и производительность

На 5700X/32 ГБ/RTX 4070, viewport 1080p, тестируемая вкладка на переднем плане: импорт ≤60 с, базовая сцена 60 FPS, «200 корветов · тест» p95 кадра ≤33,3 мс. Stress — локальные экземпляры, не 200 серверных кораблей; игровые приказы отключены.

Повторить 100 переключений galaxy/system/stress; после прогрева счётчики одного и того же вида не должны постоянно расти. Записать RAM/VRAM и фактические FPS/p95. Показания HUD не заменяют измерение и не подтверждают целевой минимум Ryzen 5 3600/16 ГБ. Физический TCP slow-reader/исчерпание сетевых буферов остаётся отдельной непроведённой проверкой. Production send deadline 3 с, отмена, FIFO/последний snapshot и единственная конкурентная отправка проверены автоматически с управляемым WebSocket; это не испытание физической медленной сети.

## Отдельный статический стенд

Базовая версия стенда **871ce37**, профиль `m3-static-452-v2`; подробности в [README](../tools/asset-probe/README.md). Для диагностики, отдельно от игрового клиента:

```powershell
Set-Location -LiteralPath '..\tools\asset-probe'
node server.mjs 5096
```

Открыть `http://127.0.0.1:5096/m3.html`. Для каждой из четырёх моделей повторить 10 циклов «Освободить сцену» → «Пересоздать»: после очистки geometries/textures=0, после пересоздания прежний состав ресурсов без постоянного роста. Данные остаются в памяти страницы. Неполный импорт/превышение бюджета сохраняют прежнюю сцену; reload требует нового источника. Реальные context loss/restore и alpha пяти иконок проверять отдельно. Этот стенд не заменяет игровой проход G01–G18.

## Результаты и обновление

### Historical decisions and earlier manual observations

The paragraphs below record earlier development stages. Their statements about suspended automatic checks, acceptable galaxy presentation and untested camera variants are historical, not the current revision status. Current automated evidence and remaining limits are in the [current delivery report](reports/m3-visual-milestone-validation.md); current user instructions allow implementation and available automated checks, with manual acceptance deferred until afterward.

Решение пользователя от 12 октября 2026: оставшиеся ручные тесты отложены до следующего этапа разработки. Существующие результаты сохраняются; полная приёмка не закрыта. Проведение автоматических проверок добавлено в бэклог как TEST-001; сейчас проверки и реализация автоматизации не запускаются.

Подтверждения пользователя для активной вкладки на паузе 45 секунд и возврата из фоновой вкладки через 60 секунд сохранены в [журнале ручной приёмки](M3-MANUAL-RESULTS.md). Это не проверки TCP blackhole или реального freeze.

Очередь ручных замечаний 12 октября 2026: нестабильное применение вакансий и возврат 1 → 2 — [ECON-001](BACKLOG.md); первая остановившаяся попытка межсистемного перехода при успешном повторе — [MOVE-001](BACKLOG.md). Причины не установлены; эти случаи не закрыты повторным успехом. По актуальному указанию пользователя диагностика и исправления отложены, продолжается ручная приёмка независимых сценариев.

Отдельная очередь камеры: пользователь подтвердил работу zoom/панорамы/вращения/сброса, затем уточнил обязательный центр вращения — выбранный объект, без выбора центр текущей галактики/системы. Сообщил о вращении вокруг фокусной точки и подтвердил, что поведение не меняется при выборе разных объектов: [CAM-001](BACKLOG.md#cam-001-центр-вращения-остаётся-фокусной-точкой-камеры) — **FAIL**, подтверждённый дефект. Варианты без выбора в галактике/системе остаются pending. Контракт — M3-CLIENT-INTERACTIONS.md.

Ручной проход 12 октября 2026: пользователь сообщил о значительном искажении вида системы; визуальная приёмка этой части **FAIL**, приоритетный дефект [MAP-001](BACKLOG.md#map-001-значительно-искажён-вид-звёздной-системы). Карта галактики пользователя пока устраивает. Это не подтверждает остальные пункты приёмки; движение в этом проходе не проверено.

Техническая очередь импорта, источники — [интеграционный отчёт](M3-INTEGRATION-VALIDATION.md) и [проверки metadata](M3-CORE-VALIDATION.md). На коде 4f5bcc6 AssetChecks: **9 PASS/1 SKIP/0 FAIL**. Подмена между hash-проходами, отмена/освобождение 45 handles и реальные Windows junction root/ancestor/internal-path проверены автоматически; повторять их вручную не требуется. Junction не заменяет symlink.

**Отложенный symlink-сценарий — pending:** в отдельной Windows-среде с уже доступным Developer Mode/необходимой привилегией повторить AssetChecks; directory symlink в корне и file symlink на обязательном пути должны безопасно отклоняться, target должен сохраниться. Использовать synthetic fixtures теста, не менять установку игры. Настройки ОС автоматически не менять и немедленную проверку не требовать. На текущем ПК вне sandbox получена `System.UnauthorizedAccessException`, HResult `−2147024891 (0x80070005)`, `Administrator privilege required for this operation`; это SKIP по среде, не PASS защиты symlink.

Для последующего запуска в подготовленной среде из корня проекта, отдельно от работающего сервера:

```powershell
dotnet run --project tests/Stellaris.AssetChecks -c Release -- --game-root "C:\Path\To\Stellaris"
```

Сохранить фактические PASS/SKIP/FAIL и причину SKIP; отсутствие нужной привилегии оставляет symlink pending. Chrome/FPS/RAM/VRAM и физическая медленная TCP-сеть также остаются непроверенными.

Полная ручная приёмка **не завершена**; перечисленный выше FAIL сохраняется отдельно от требований. Остальные пункты без конкретного результата остаются непроверенными. Node/серверные PASS и предварительный осмотр встроенным браузером не превращаются в Chrome PASS. Подробные результаты следующих проходов сохранять отдельно от требований, например в `M3-MANUAL-RESULTS.md`.

Карточка результата: коммит / ОС / Chrome / оборудование / viewport; ID и статус; шаги; ожидалось; получилось; дата/роль; время события; ошибка и фрагмент лога. Не передавать код доступа, токены или полный sessionStorage; игровые файлы и screenshots не коммитить.

Перед следующей поставкой M3 сверить итоговый коммит, команды, UI, fixture и известные ограничения с интеграционным отчётом; обновить этот чек-лист до самостоятельной приёмки пользователя. Дефекты не исправлять изменением ожидаемого результата без согласованного изменения контракта.
