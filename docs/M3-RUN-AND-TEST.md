# M3 — запуск и ручная проверка

Один ПК, один сервер, три независимые вкладки Google Chrome: два игрока и наблюдатель. Нужны .NET SDK 8, Node.js и pnpm (команда проверена с 11.25.0), WebGL2 и локальная Stellaris **4.5.2** без изменений обязательных файлов. Это инструкция, не отчёт PASS. Границы: [fixture](M3-WORLD-FIXTURE.md), [интерфейс](M3-CLIENT-INTERACTIONS.md), [импорт](M3-LOCAL-ASSET-IMPORT.md).

## Запуск сервера

Before rebuilding, stop any previous M3 server in its terminal with Ctrl+C. This releases its executable and port 5089; M3 has no saves, so the previous session ends. A running old server does not automatically become the new build when the source checkout changes.

Откройте PowerShell. Выполняйте команды по очереди. Копируйте **только строки внутри блоков**: не вставляйте строки с тройными обратными кавычками, слово `powershell` или приглашение `PS ...>`. Параметры `--project` ниже относительные, поэтому первая команда обязательна.

```powershell
Set-Location -LiteralPath '..'
pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts
dotnet build src/Stellaris.Server -c Release
```

Зависимости устанавливаются по `pnpm-lock.yaml`; lifecycle-скрипты пакетов для браузерного клиента не требуются. Дождитесь успешного завершения каждой команды. После успешной сборки запустите сервер; замените путь после `--game-root` на корневую папку **своей** игры (в ней `stellaris.exe`, `common`, `gfx`). Пример для ранее указанной установки:

```powershell
dotnet run --no-build --project src/Stellaris.Server -c Release -- --mode m3 --game-root "C:\Path\To\Stellaris" --urls http://127.0.0.1:5089 --join-code M3-local-test-code
```

Оставьте окно работающим. Откройте `http://127.0.0.1:5089/health`: ожидается `protocol: 2`, `mode: "m3"`. Игра: `http://127.0.0.1:5089/m3.html`. Не открывайте HTML через `file://`; отдельный Vite/preview сервер не требуется. Порт 5089 должен быть свободен; этот запуск доступен только локально.

## Три клиента и старт

1. Open three new tabs by entering the game URL separately; do not duplicate a logged-in tab because Chrome may copy its participant token. Use the ⚙ button ("Подключение, импорт и лобби") to open setup, including after reload/reconnect when a saved token leaves the dialog closed.
2. В каждой вкладке нажмите «Выбрать папку Stellaris», укажите корень собственной 4.5.2 и разрешите чтение. На одном ПК можно выбрать одну установку три раза, но разрешение и импорт принадлежат каждой вкладке. Дождитесь «Готово» без ошибки. Сервер не раздаёт игровые ассеты; они не загружаются на сервер и не сохраняются в постоянный кеш.
3. File-input fallback: expand the required-file list and select exactly all **149** files for profile `m3-static-452-v7`, without renaming or duplicates. If the dialog cannot select across folders, prepare personal temporary copies outside the repository, preserving filenames. These are file snapshots; the main Chrome workflow is folder selection. Do not disable browser protection. Replacing a complete active profile exceeds the staging budget and preserves current assets with a visible refusal; reload the page, then import again. Current stable-checkout verification and bounded browser results are in the [intermediate candidate report](reports/intermediate-playable-rc-2026-10-13.md); live construction remains incomplete.
4. В первой вкладке: имя `Игрок 1`, код `M3-local-test-code`, роль «Игрок», «Подключиться». Во второй: `Игрок 2`, та же роль/код. В третьей: `Наблюдатель`, роль «Наблюдатель». Первый игрок — хост.
5. Before starting, the host approves the pending observer. Pending observers receive no world/economy. Approved observers default to general overview (both economies, no personal outliner); the observer selector switches to a player's server-filtered POV and personal outliner/resources while keeping observer identity and read-only commands. See the [perspective checklist](M3-MANUAL-ACCEPTANCE.md#personal-outliner-and-observer-views).
6. If needed, players configure traits/buildings and save setup. Both mark ready, then the host starts. Latest command feedback appears near the upper right; full ACK history is in "Диагностика M3". Only the host controls time.

## Основные сценарии

Отмечайте результат отдельно для каждого пункта: PASS / FAIL / не проверено. Для FAIL запишите вкладку/роль, дату, шаги, ожидаемое и фактическое, текст ошибки/подтверждения, при необходимости скриншот и серверный лог.

### Карта, модели и ввод

- The galaxy contains 16 systems and 24 hyperlanes; starting systems are S01/S16. The outliner is on the right, selection/orders on the left. Click selects; double-click a system opens it. Systems show the original star/planet, plus a station and three corvettes in starting systems. Materials remain static approximations; missing layers/models or a black scene are defects. See the [visual comparison and limitations](reports/m3-visual-milestone-validation.md).
- ЛКМ drag — панорама, Alt+ЛКМ drag — вращение, колесо — масштаб. Drag не должен случайно выбирать объект; ПКМ не панорамирует и не открывает контекстное меню. «Сброс камеры», «Центр на выборе», «Галактика» работают. Resize не теряет выбор; ввод X/Z и кнопки DOM не двигают камеру. Escape вне поля ввода сначала снимает выбор, затем возвращает галактику.
- Select an owned fleet, then a destination system: the "Приказы" panel must retain the owned command fleet independently of the inspected destination. Foreign fleets remain inspectable but cannot become command fleets. Observers have no gameplay orders.

### Движение, переход и stop

1. Откройте стартовую систему своего флота. На паузе введите X=20, Z=-80 → «К точке»; затем «Шаг дня» несколько раз. Позиция меняется только с серверными днями, фаза `movingLocal`, при достижении точные координаты и `idle`. Повторите ПКМ по плоскости системы. X=151, Z=0 должен быть отклонён без clamp к границе и без потери предыдущего приказа.
2. Во время локального движения задайте другую точку, затем «Стоп»: цель заменяется, stop удерживает текущую позицию. Проверьте одинаковое состояние флота в двух игроках и наблюдателе.
3. Выберите другую систему в поле «Система» → «Переместить»; повторите ПКМ по системе в галактике. Наблюдайте `approachingExit` → `charging` → `inTransit` → вход в следующую систему. Для fixture подготовка 15 дней, переход 5 дней на ребро; локальная скорость 7,2 единицы/день. Подлёт также занимает дни. Это согласованные параметры M3, не полная ванильная навигация.
4. Во время `inTransit` «К точке» недоступна. «Стоп» и новая система допускаются как pending и действуют **после прибытия**, не телепортируют флот. В виде системы транзитный флот исчезает, в галактике отображается между серверными endpoints; после прибытия снова доступен в системе.

### Время и приватная экономика

- Хост: «Продолжить»/«Пауза», скорости 1/2/4 дня/с, «Шаг дня» только на паузе. Дата всех клиентов совпадает; пауза не меняет день сама. Старт 2200.01.01, месяцы по 30 дней: **30 шагов** от старта дают 2200.02.01 и первый экономический отчёт, не 30 отчётов. Проверяйте это до длительных тестов движения либо от текущего месяца до следующей границы.
- Open "Лаборатория экономики". Players see their own economy; an observer sees both only in general view, and only the selected player's economy in player POV. Inspecting a foreign planet never reveals its economy to a player. During running, change owned job counts and wait for the next month. The report separates jobs and buildings: total upkeep is their sum, and net is production minus total upkeep. Building upkeep persists at zero job slots and does not scale with job Workforce. Districts, combat and research are outside this slice.

### Отключение, возврат и перезапуск

- Во время движения нажмите «Отключиться» у игрока: сцена остаётся устаревшей, приказы блокируются; ранее отданный приказ продолжает выполняться на сервере. «Подключиться» в той же вкладке возвращает того же участника. При полном reload импортируйте папку заново и подключитесь с сохранённым входом вкладки. Не нажимайте «Очистить вход» для обычного возврата.
- Повторите для хоста: скорость/пауза не меняются, права автоматически не передаются; обычный AFK после 60 секунд допускает возврат. Для отдельного теста до отключения используйте «Передать хоста» рядом со вторым игроком.
- **Последним тестом** остановите сервер Ctrl+C и запустите той же командой. Старый мир утрачен, старые токены не действуют: в каждой вкладке «Очистить вход», новый локальный импорт и вход. Это новая партия, не загрузка сохранения.

## Stress и границы проверки

«200 корветов · тест» — локальные экземпляры, не 200 серверных кораблей; игровые приказы здесь отключены. Вернитесь через «Галактика». Повторите 100 переключений galaxy/system/stress: ресурсные счётчики могут различаться между видами, но после прогрева одного и того же вида не должны постоянно расти. Ошибки, зависшие импорты и утрата управления — FAIL.

Для замеров держите тестируемую вкладку на переднем плане: Chrome ограничивает фоновые вкладки. Запишите Chrome/GPU, размер viewport, FPS/p95 и время импорта. Цели на 5700X/32 ГБ/RTX4070, 1080p: базовая сцена 60 FPS, stress200 p95 ≤33,3 мс, импорт ≤60 с. Показания HUD сами по себе **не подтверждают** приёмку и не доказывают Ryzen 5 3600/16 ГБ. Отдельно остаются реальная Chrome-проверка разрешений папки, внешнего вида всех моделей/иконок, context loss/restore и GPU/FPS; непроведённые пункты отмечать «не проверено», не PASS. После context restore приказы должны ждать свежий серверный snapshot; самостоятельных игровых приказов восстановление не отправляет.
