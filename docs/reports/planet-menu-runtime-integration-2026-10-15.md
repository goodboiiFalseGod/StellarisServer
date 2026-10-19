# Меню планеты: подключение согласованной вёрстки

Актуальное прямое указание пользователя: взять уже свёрстанное меню, подключить
актуальные код и ассеты; исправлять сервер, парсеры и игровую логику по необходимости.
Ранее реализованная оболочка со старой внутренней сеткой не завершает эту задачу.
Текущий UI-срез переносит композицию `6b5bb59:docs/references/planet-menu-452.html`:
шапка, показатели, городской блок с зонами, ресурсные районы, правая очередь,
внешний каталог и пять вкладок. Данные и команды берутся из runtime; demo-числа,
фиктивные состояния и неподтверждённые действия не переносятся. Критерии:
реальные изображения после локального импорта, точные slot/order ID, сохранение
прав/POV/revision guards, доступная карта и проверка фактического интерфейса.
Неизвестные механики блокируют только зависимые действия. Завершённость всего
меню требует последующих серверных этапов, а не только визуального сходства.

## Актуальный UI-срез по прямому уточнению пользователя

Подключены `m3-planet-window-view.mjs` и отдельный scoped CSS. Композиция
собирается из настоящего runtime renderer, без копирования demo-модели HTML.
Сохранены изображения районов, зон, зданий, профессий и ресурсов из локального
импорта; карточки и действия сохраняют исходные обработчики и ID. Цены и
содержание показывают иконку и количество, нехватка конкретного ресурса
вычисляется только при наличии полученного stock. Пустая инфраструктура без
development сохраняет сообщение и месячный отчёт. Неоднозначные экземпляры
одного типа района не получают выдуманные текущие уровни из общей суммы.

Проверки: 256/256 Node; isolated Release build в
`artifacts/planet-menu-visual-build` — 0 предупреждений и ошибок. Ревью root-кода
`/root/planet_contract_review`: две находки по unavailable fallback и повторным
типам исправлены и повторно проверены, новых findings нет. CSS и первоначальные
шесть тестов `/root/window_geometry` (Luna low) прочитаны ведущим; дополнительный
регрессионный тест ведущего проверен независимым reviewer. Правки CSS после
браузерных находок прочитаны ведущим; это не server/gameplay review.

Браузер: два игрока, реальные собственные колонии, локальный импорт 921 файла
и EN/RU (464 файлов, 0 import diagnostics). Проверены городские 6 базовых мест,
две зоны по 3 места и три ресурсных района, внешний каталог 300 px, цены,
масштабирование окна, вкладки, собственная экономика второго игрока. Main не
имеет горизонтального переполнения (clientWidth = scrollWidth = 730).
Окно `open=true`, `:modal=false`; карта остаётся снаружи. Команда
`setWorkforceLimitMax` из нового меню получила ACK; следующий шаг успешно
перешёл с 2200.08.30 на 2200.09.01 и обновил реальный месячный баланс. Армии и
владения показывают отсутствие серверных данных. Console error/warn пусты.
Новый visual adapter не проходил отдельный observer smoke; предыдущий
owner/POV smoke ниже относится к прежнему UI-срезу. Новые handlers не обходят
имеющиеся client/server guards. Native/GPU/OS и полная визуальная parity не
объявляются проверенными.

Локальные доказательства (ignored, без оригинальных ассетов в Git):
`content-local/references/planet-menu-reference-runtime/infrastructure.jpg`,
`catalog.jpg`, `economy-peer.jpg`. Согласованный HTML отдельно отрисован для
сравнения в локальном ignored build; inline CSS/script вынесены во временные
файлы из-за CSP сервера, игровая модель этим не менялась.

SHA256 локальных доказательств:

```text
infrastructure.jpg 3847F0BD81B0366034466E691A4AC102EAB16D24CBB01A8F1F552217B3F81EB2
catalog.jpg D2AB5F0EBD3276D2DBC910F91CADF5BEA17BCC44569ED4844D4D43471B01EB28
economy-peer.jpg 398B01C3228139A4E2F0CA698D4360A389EB1817D69CBC8EF7EF67470C8CEC37
```

Оставшийся функциональный объём: live global/type/shared caps и резервы,
reorder очереди, смена зон с preview несовместимых зданий, lifecycle
toggle/demolish/replace/repair, армейская и holdings authority, проекция
показателей шапки и импорт панорамы. Доступный каталог пока содержит явную
кнопку покупки, целая карточка как purchase target ещё не подключена.
Нативные неизвестные правила требуют источника/эксперимента и блокируют
только свои операции. Это список незавершённого, а не завершённость D45.

15 октября 2026. Ветка `feature/planet-menu-integration`, managed worktree
`planet-menu-integration`; база `4a55ff97bf2568a33910e03c642c9d6042293990`.
Основной checkout `9974b1c`, master `6b5bb59` и RC `323f025` не изменялись.
Исходное дерево было чистым. Remote push и перенос в stable не выполнялись.

## Контракт и владение

Основание: постановка `0829596:docs/reports/planet-menu-integration-review-2026-10-15.md`;
согласование/закрытые L-вопросы и HTML читаются из `6b5bb59`, без слияния всего master.
Первый срез ограничен оболочкой и существующими runtime-операциями. Числа/строки
заказов/графика демонстрационного HTML не переносятся в игровую модель.

Writer общего клиента/panel/HTML/CSS и относящихся tests — текущий исполнитель.
Новый `m3-planet-window.mjs` и его tests делегированы `/root/window_geometry`
с явными `gpt-6-luna`, `low`, без вложенной делегации. Ведущий интегрировал
full-height внешней панели. Lead/reviewer настройки сохранены; usage неизвестен.
Context audit до назначений: required 11993/12000, handoff 4749/6500,
workflow 8480/9000, превышений нет; метрики сессий unknown.

Поведение: modeless `dialog.show()` оставляет карту доступной; общая геометрия
и активная вкладка сохраняются в памяти при закрытии и смене колонии. Смена
колонии очищает inspection; смена POV/session очищает приватную модель прежними
guards. Минимум 720×480 CSS px, без масштабирования текста. При малом viewport
ограничение явно показано; для viewport меньше минимума полноценная доступность
содержимого требует отдельной ручной приёмки. Правая панель шириной 300 px
сначала сдвигает окно влево, затем при нехватке места накрывает содержимое под
шапкой и автоматически возвращается наружу при расширении viewport.

Пять вкладок: Инфраструктура; Управление (реальные features/blockers);
Экономика (реальные Workforce/jobs); Армии; Владения. Последние две честно
показывают unavailable. Существующий graph district→zone→slot→installed,
экономика, pending/revision/authority/connection guards и команды
enqueueConstruction/enqueueUpgrade/cancelConstruction сохранены. Клиент не
подменяет команды локальными игровыми мутациями. Сведения следуют identity
слота при completion; исчезновение definition возвращает к месту, исчезновение
самого места закрывает сведения. Каталог скрывает неразрешённые определения,
сохраняет доступные при недостатке ресурсов и обновляется по полной live-модели.

Ошибки/stale/foreign/replay обрабатываются существующим транзакционным путём;
новых DTO или серверных механик нет. Наличие вкладки не даёт новых capabilities.

## Независимое ревью и проверки

`/root/planet_contract_review` прочитал actual diff и untracked module/tests
против контракта. Один P2: потеря фокуса кнопки заказа при обновлении каталога.
Исправлен stable action identity и fallback next enabled/close; meaningful
регрессия покрывает same action → next action → close. Повторное ревью одобрило
ограниченный первый UI-срез без оставшихся воспроизводимых P1/P2.

- Focused Node: 44 PASS, 0 FAIL, 0 SKIP.
- Полный `tests/client/m3-client.test.mjs`: 204 PASS. Harness получил modeless
  boundary и пять вкладок; старые assertions «заказ только из сведений» обновлены
  по согласованному прямому заказу из каталога. Выявленная регрессия старого
  navigation callback при model change/same revision исправлена проверкой
  точного render key; повторно независимо одобрена.
- Release: 0 warnings, 0 errors, после frozen install --ignore-scripts;
  package/lock не изменены.
- PlanetConstructionLiveChecks с read-only 4.5.2: PASS; реальные quotes/slots,
  authorization/idempotency/payment/cancel, lifecycle районов. Часть политик synthetic.
- ConstructionChecks с game-root: 53 PASS, 0 FAIL, 1 SKIP; пропуск не считается покрытием.
- PlanetAcceptanceChecks с game-root: 8 групп PASS, 0 FAIL, 0 SKIP;
  source binding/placement/POV, без native/browser acceptance.
- `/health` отдельного порта 5226: protocol 2, mode m3. Порт проверен свободным
  перед запуском; пользовательские серверы и binaries не останавливались.
- SHA256 served/source совпали для m3.html, m3.css, m3-client.mjs,
  m3-planet-panel.mjs и нового m3-planet-window.mjs.

## Запуск и оставшаяся приёмка

Локально задать `STELLARIS_GAME_ROOT`, `STELLARIS_TRIANGLE_WORKER` и
`STELLARIS_TRIANGLE_WORKER_SHA256`. Использован уже имеющийся worker с закреплённым
SHA `CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71`.
Машинные пути/игровые bytes в Git не добавлены.

```powershell
dotnet build src/Stellaris.Server -c Release
dotnet run --no-build --project src/Stellaris.Server -c Release -- --mode m3 --game-root $env:STELLARIS_GAME_ROOT --urls http://127.0.0.1:5226 --join-code Planet-menu-local
```

URL: `http://127.0.0.1:5226/m3.html`. Перед поставкой собственные QA-процессы
20092/60400 остановлены; выполнена обычная чистая Release-сборка финального
кода `5de247b` (0/0). Запущена новая пустая сессия из этого output на 5226;
PID и stdout/stderr находятся только в ignored `artifacts/planet-menu-*`.
Она оставлена для ручной проверки; старые QA-токены относятся к завершённой
сессии — в старой вкладке нужно «Очистить вход». Порт 5227 освобождён.

## Живая browser QA и границы доказательств

На собственной 5226-сессии поэтапно собранного UI: два реальных browser-клиента,
экономический/научный пресеты, штатный Start, 200 систем, read-only импорт 921
файла в каждом браузере. Основной клиент перепроверялся после reload/reimport;
импорт 229,3 MiB, EN/RU 464 файла. Игровые bytes и снимки не добавлены в Git.
Настоящее локальное состояние не заменялось fixture/all-tech/fake stocks.

Проверено: outliner→реальная колония; пять итоговых вкладок; graph и реальные
значки/экономика/Workforce; modeless `open=true`, `matches(':modal')=false`;
resize 900→1098 px и внешний каталог→overlay под шапкой (фактические rects);
close/reopen сохраняют вкладку «Управление» и rect 1098×620;
управление временем при открытом окне; live недостаток средств→доступный заказ;
штатные Shift min и absolute Workforce-limit; импорт и reconnect.
Отдельный API viewport override не изменил фактические 1280×720 в IAB:
узкие viewport/scale и полная карта pan/zoom/select остаются ручным checklist.

Оплаченный заказ гидропонной фермы в точный government building-slot/2:
346,5→6,5 минералов, серверный order/резервирование, реальная работа
0→110,4→155,25 из 360. Ручная отмена вернула 340, освободила слот;
356,9 включает естественные месячные поступления. Приёмка completion и
research→upgrade в браузере не выполнена; они покрыты относящимися
source/client tests только в их явно указанных пределах.

Наблюдения baseline: при временном обнулении производящих профессий сервер
атомарно останавливал месяц из-за consumer_goods; позже научный пресет второго
игрока исчерпал minerals. Продолжение достигнуто реальными Workforce-командами
(ремесленники 250, металлурги 0), без правок запасов. Эти сообщения и отсутствие
автоочистки прежнего alert не исправлялись в UI-срезе. Поздний вход нового
observer в running-сессию отклонён базой: полный живой observer-сценарий не
проверен; права/POV/stale остаются проверенными автоматикой, не browser PASS.

Cancel QA выявила унаследованное отображение terminal receipts в очереди и
progress свободного слота. Follow-up `5de247b` исключает только `completed` и
`cancelled` из UI, сохраняя DTO/history. Независимый
`/root/planet_contract_review` сверил enum и lower-case adapter serialization,
одобрил actual diff двух файлов. Итоговый объединённый Node-набор:
**249 PASS, 0 FAIL, 0 SKIP**. Final source/served smoke выполняется на новом
чистом запуске; полная игровая browser QA после последних двух точечных
исправлений (stale callback guard и terminal filter) не повторялась.

Скриншоты просмотрены через локальный browser tool, tracked screenshot assets
не создавались. Console error/warn основного клиента на проверенном игровом
пути: пусто; не проверенные native/GPU/OS условия не объявляются PASS.

Финальная проверка 5226 после сборки `5de247b`: health protocol2/m3;
SHA256 served/source совпали во всех шести файлах HTML/CSS/client/panel/window/
construction-controls. Final panel SHA:
`5DA27B386CBFAEFA9F8B17313471608252B26F8123EB49AD9B1A110C1B9E117D`.
Новый browser entry point открыл чистое лобби, console error/warn пусто,
stderr собственного процесса 51768 пуст. Документирующий коммит поверх кода
меняет только этот отчёт; runtime-файлы совпадают с проверенным `5de247b`.
Основной checkout остался чистым, master=`6b5bb59`, общий RC=`323f025`.

Не покрыто полной D45: authoritative caps/reservations/reorder/N/receipt bound;
zone create/replace/preview; building replace/repair/demolish/toggle; holding
authority/runtime; реальные governor/demography/армии/Arkship. Пересчёт отдельных
красных resource pairs и полная reference visual parity требуют последующего
среза. Старые L-вопросы не открываются. Эти продолжения требуют точного назначения
Session/Core/wire/tests единственным текущим интегратором, по исходной постановке.
Не заменять целиком отличающиеся файлы RC и не переносить весь M3 в master.

Checklist ручной приёмки: два игрока+observer; обычный старт и локальный импорт;
карта/outliner→колония; пять вкладок; drag/resize/720×480/viewport/scale;
outside↔overlay; карта pan/zoom/select; close/reopen/другая колония/POV;
paid enqueue→queue→completion/cancel и upgrade; live unlock/stock/focus/scroll;
disconnect/reconnect/stale/foreign без списания. Automated/source evidence не
подменяет эту проверку или полную native/GPU приёмку.
