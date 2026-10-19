# Подключение согласованного меню планеты

15 октября 2026. Техническое ревью и постановка следующему исполнителю; production не изменён. Требования: D45, `PLANET-MENU-CONSULTATION.md`, `PLANET-CONSTRUCTION-LOGIC-QUESTIONS.md`. Последний документ закрывает все L-вопросы; историческое «затем/не установлено» в отчёте 14 октября не открывает их снова. Референс задаёт UI и сценарии, не численные данные игры.

## Базы и безопасная доставка

- Одобренный HTML: `6b5bb595443a6665f946c8969e63cae79d88ce5d` (master), `docs/references/planet-menu-452.html`.
- **В этом master отсутствует M3 runtime.** Есть только M2: `Program.cs` выдаёт protocol 1; `wwwroot` содержит `index.html`, `app.js`, `styles.css`. Вставка референса сюда не подключит строительство.
- Фактическая база консультации и проверенного клиента: `4a55ff97bf2568a33910e03c642c9d6042293990`, ветка `fix/ui-reference-defects`. Здесь есть protocol 2, M3, construction, research и исправления UI. Предлагаемая база отдельной task-ветки реализации — именно этот точный commit; одобренные три документа и HTML читать из `6b5bb59`, переносить только их при необходимости.
- Существует и отдельный RC `integration/playable-systems-rc` на `323f0255f2812cb6d4d2b169017c58ee504f9928`. Это не синоним master/4a55ff9: scoped diff клиента и панели содержит 32 добавления/142 удаления относительно 4a55ff9. Не заменять файлы RC целиком и не вливать весь M3 в master под видом меню. Поставка этой задачи — собственная проверяемая ветка; дальнейшее объединение выполняет назначенный интегратор после актуальной сверки.
- Исходный checkout был чист на `feature/m3-static-visuals`, `9974b1c`; он не изменялся. Ревью ведётся в собственном managed worktree на `review/planet-menu-integration` от `6b5bb59`. Чужие worktree, процессы, порты и binaries сохраняются.
- `EXPERIMENTAL-RC-ACCEPTANCE.md`, `EXECUTION-POLICY.md`, `CONTEXT-WORKFLOW.md`, `M3-WORK-OWNERSHIP.md` отсутствуют в master-base; обязательные актуальные правила прочитаны на runtime-линии `4a55ff9` (и доступные в исходной копии). Они не должны теряться при выборе базы. Новый чат не становится вторым общим RC-интегратором. Stable/master и remote не обновлять без отдельного прямого разрешения.

## Что переносить из HTML

`planet-menu-452.html:220–360` — автономный демонстрационный сценарий. `state.orders` — строки; `drawPage`, `buildingDefinitions`, списки районов/владений, ресурсы, роли, уровни, прогресс 28%, цены, эффекты и сообщения — fixtures. `window.openai.setWidgetState`/`openai:set_globals` относятся к образцу, не к игровому соединению. Локальные DOM-мутации заказа/сноса/переключения не являются транзакциями.

Извлечь в реальные компоненты: оболочку окна/геометрию, постоянную шапку и показатели, пять вкладок, граф район→zone occurrence→slot→instance, карточки состояний, очередь, единый правый контейнер каталога/сведений, подтверждения и tooltip. Геометрию `position`/`drag` можно использовать как ориентир после проверки; семантику и identities брать из снимка. Буквенные/символьные картинки — условная графика, реальные изображения/ресурсы/локализацию брать из существующего локального importer. Не копировать игровые bytes, screenshots или машинные пути в Git.

## Точки подключения (пути относительно репозитория, runtime 4a55ff9)

| Слой | Файлы / требуемая правка |
|---|---|
| Оболочка | `src/Stellaris.Server/wwwroot/m3.html:33`, `m3.css:146–204`: заменить трёхвкладочный modal и внутреннюю третью колонку целевой композицией. |
| Контроллер | `wwwroot/m3-client.mjs`: imports 8/11; `openPanel` около 244 использует `showModal`, `closePlanetWindow` 630 сбрасывает вкладку; `canEditPlanet`, `submitPlanetConstruction` 635–690; `renderPlanetWindow` 697–809; открытие/табы 813–822. Сохранить guards и result/pending/reconnect, поменять lifecycle/геометрию/renderer bindings точечно. |
| Представление | `wwwroot/m3-planet-panel.mjs`: `planetStartupGraph`, `planetPanelModel`, `resolvePlanetInspection`, `renderPlanetPanel`. Уже есть точные zone/slot связи и дедупликация startup/live; не заменять плоской сеткой. `m3-construction-controls.mjs` строит только подтверждённые операции. |
| Wire | `src/Stellaris.Session/M3/M3Protocol.cs` (`M3ClientCommand`); `M3DevelopmentProtocol.cs`; `M3DevelopmentProjection.cs`; `wwwroot/m3-development-tables.mjs`. Сохранить compact sourceRecipe/slot-set tables, nullable данные и ограничения payload. |
| Сервер/ядро | `src/Stellaris.Session/M3/Construction/M3ConstructionAdapter.cs`, `M3ConstructionContent.cs`, `M3Session.cs`; `src/Stellaris.Core/Construction/ConstructionLiveRuntime.cs`, `ConstructionLiveContent.cs`, очереди/ledger/economy. Это последовательный sensitive scope, не механическая замена CSS. |
| Ассеты | `m3-client` callbacks `appendPlanetImage`/resource icons/localization; `tools/asset-probe/m3-planet-images.mjs`, профили и manifest. Для обычного UI нового importer не нужно; отсутствующий binding не подменять generic картинкой. |
| Сборка | `src/Stellaris.Server/Stellaris.Server.csproj`: `wwwroot/**` копируется в output; shared `tools/asset-probe` модули подключены explicit Content Link как `/m3-modules/`, Three — `/m3-vendor/`. Новые модули в wwwroot не требуют переноса в статический стенд. `tools/asset-probe/m3-app.mjs` — не игровой entry point. |

## Данные и команды: существующее → необходимое

Все новые поля ниже — **проектируемое расширение**, не существующий wire. Имена окончательно закрепить до кода в тематическом контракте. Не вычислять authority/совместимость/лимиты на клиенте по видимым именам.

| Объект | Сейчас | Необходимо для полного согласованного сценария |
|---|---|---|
| Колония | `ColonyId`, owner, `Revision`, class/size, startup graph, live economy/gates. `planetPanelModel` привязан к текущему POV. | Живые показатели шапки с явной недоступностью; capabilities отдельных действий и variant. Нет подтверждения runtime для губернатора/демографии/армий/решений/терраформирования: не брать числа образца. |
| Район | Startup instance/definition/level, startup cap modifiers; enqueue работает для city. Ресурсные типы отклонены `district-type-capacity-unresolved`. | Авторитетные completed/reserved levels; текущие global/type/shared caps и остатки; точная identity района/следующего резервирования. Повторный заказ до cap; кнопка «Улучшить» сразу заказывает свой тип, без каталога. На cap disabled без подписи/tooltip причины; pending levels отдельными сегментами, эффекты только завершённых. |
| Зона | Startup `DistrictInstanceId`, `SlotInstanceId`, `InstanceId`; каталоги существуют, lifecycle gated. | Живой graph; unlock requirement для добывающей зоны; create/replace command, quote и compatibility preview по точной occurrence. На city шесть base slots + две zone occurrences по три slots после завершения. Запрет building orders только затронутой зоны. Замена сохраняет старые effects до atomic completion, удаляет только подтверждённые несовместимые здания без refund. Preview возвращает exact removed instance IDs и revision; server повторяет проверку при принятии и завершении. |
| Здание | Installed связан slot/definition; `Upgrades` содержит source/target/zone/revision/quote. Есть `enqueueUpgrade`. | Live instance ID и normal/disabled/ruined + operation state; команды replace/repair/demolish/toggle и применимость каждой. Upgrade наследует enabled на completion; replacement всегда устанавливает enabled. Repair оставляет ruined до completion и блокирует все другие операции. Demolish немедленный, с отдельным подтверждением без refund; при связанном заказе сначала явная отмена. Сведения продолжают target того же slot после обновления, закрываются только при исчезновении target. |
| Очередь | `OrderId`, slot/definition/kind, paid, completed/required work, refund quote; wire status pending. Core уже имеет `Reorder`, но Session/command/UI его не проводят. | Порядок, authoritative N и active/waiting/paused, live speed/remaining estimate с явным unknown при отсутствии формулы. Команда reorder exact IDs+revision; обычная очередь не допускает waiting выше active; при параллельной допускается смена первых N. Pay/reserve на acceptance, прогресс ежедневно, без carry дня; sequential completion до месячной экономики. `N=0` сохраняет progress paused. |
| Возврат | Cancel принимает order/revision; quote содержит amounts. | Отдельно paid/refundable/accepted/lost по storage caps, revision запасов и заказа. При lost>0 обязательный preview/confirm; изменившийся расчёт отклоняет отмену и обновляет подтверждение. Auto-cancel объединяет причины колонии, original payer, пропавшему payer деньги не перенаправлять. Colony destruction без refund, одно уведомление. |
| Владения | Нет DTO, runtime, commands. Demo role `holdingOwner` не authority. | Owner-scoped holding instance/slot/owner empire, публичные поля для владельца планеты и capabilities для конкретного владельца владения. Planet owner только читает чужие holdings; доступ к одному не открывает остальные. До появления реального subsystem показывать честное unavailable, а не вымышленные две записи или «0 владений». |
| Каталог | `DefinitionId`, kind, image/localization, `Eligibility`, `SlotIds`/compatible sets, quote, gates, `CanAfford`. Controls выпускают enqueue только при валидном quote и slot. Текущий UI показывает также locked definitions. | Сервер различает unlocked+compatible+allowed и affordability. В каталоге только первые; нехватка ресурсов оставляет карточку, disabled заказ и красные отдельные resource pairs. Replace исключает текущее definition. Фильтры/цены пересчитываются при stock/research/conditions/slots/pending изменениях, даже если colony revision прежняя. |

Существующий envelope: `enqueueConstruction`, `enqueueUpgrade`, `cancelConstruction`, `clearBlocker`; поля `commandId`, `colonyId`, `expectedColonyRevision`, `orderId`, `definitionId`, `slotId`, для upgrade также `sourceDefinitionId`, `zoneInstanceId`. Поля цены/эффектов от браузера не принимать. `clearBlocker` сейчас gated `clearance-lifecycle-unresolved`, не считать работающим. Новые commands требуют обновить dispatch, pending classification, retry persistence, serializer/validation, result handling и тесты, а не только union кнопок.

## Состояния, ошибки и границы доверия

- `canEditPlanet` требует player/self POV, свежий transport/snapshot, running, assets, profile и точную связь economy/colony. Сервер повторяет authorization/identity/revision/quote/conditions под транзакцией. Observer POV не даёт write. Сейчас `M3DevelopmentProjection:63` вовсе не выдаёт development колонии вне player mode: полноценный observer read-only потребует отдельной безопасной проекции, а не разблокирования owner predicate.
- Сохранить command ID идемпотентности: replay того же содержания не оплачивает второй раз; другой payload с тем же ID отвергается. Stale/недостаток ресурсов/занятый slot/потеря условий не имеют частичных side effects. После отказа нужен свежий authoritative snapshot; новый ID только для нового решения пользователя. Pending transport и accepted queued order — разные состояния.
- Core имеет инженерные пределы `Orders.Count>=4096` и lifetime `_receipts.Count>=4096`; также mixed queue kinds gated. Это не игровой лимит. Полное меню не готово, пока новая разрешённая очередь упирается в эти guards. Исправлять отдельным reviewed транзакционным этапом с доказательством replay/retention, не простым снятием bound.
- UI хранит лишь общую геометрию/вкладку между колониями и открытиями; очищает приватную модель/selection/confirm на смене POV/сессии. Inspector держит slot identity, а не старый definition fingerprint: текущий `planetInspectionSourceKey` закрывает его при смене definition, это надо изменить для upgrade/replacement/repair.
- Открытый каталог reconciliation по стабильным IDs сохраняет scroll/focus; исчезнувшая карточка → следующая; пустой список → «Нет доступных вариантов», focus close. Включить stock/capability/catalog digest в invalidation: текущего пересоздания DOM по JSON model недостаточно для сохранения фокуса.
- Modeless окно (например, dialog.show с проверенным управлением focus) оставляет карту интерактивной; прозрачный backdrop при `showModal` не решает inert. Внешний клик не закрывает, другой объект заменяет основное окно. Смена колонии сохраняет геометрию/вкладку, закрывает каталог. Arkship вместо армии, переход с обычной «Армии» → «Инфраструктура»; специальную вкладку не выдумывать.
- Min 720×480 CSS px при 100%, без масштабирования текста. Каталог/сведения фиксированной ширины примыкают справа по высоте; сначала сдвиг окна, затем overlay внутри под шапкой; обратно наружу автоматически. Scroll только содержимого. После изменения viewport/UI scale доступны вся шапка и resize handle. Viewport меньше min требует живой проверки доступности и явного раскрытия ограничения, без молчаливого изменения принятого min.

## Первая итерация и порядок миграции

Причина: UI зависит от отсутствующих server capabilities; риск переписи — потеря reservations, privacy, live economy и accepted pending/replay. Решение — вертикальные этапы с явным непокрытием, не имитация всех операций.

1. В отдельном worktree/task-ветке от `4a55ff9` зафиксировать базу, владельца и короткий coverage contract. Первый законченный результат: новая modeless оболочка, геометрия, пять вкладок и правый контейнер; реальные существующие graph/экономика/features/Workforce, работающий путь supported enqueue→queue→completion/cancel и upgrade. Недостающие системы явно unavailable; без fake кнопок с локальными мутациями. Это первая итерация, **не полная приёмка D45**.
2. Для первого code slice ограничить запись `wwwroot/m3.html`, `m3.css`, `m3-client.mjs`, `m3-planet-panel.mjs`, `m3-construction-controls.mjs`, новыми `m3-planet-window*.mjs`/tests и существующими planet/runtime tests. Renderer/importer/lobby/research/package/lock/shared docs исключить. Новые modules в wwwroot предпочтительны. Не менять server только ради вёрстки.
3. Отдельно замкнуть authoritative caps/reservations и reorder (существующий Core использовать), queue lifecycle/receipt issue; затем zone create/replace+confirm+completion; затем building actions+effects; затем отдельный holding authority/runtime slice. Перед каждым расширением перечислить точные Session/Core/wire/tests paths, согласовать единственного writer с текущим интегратором. Старые L-вопросы не переоткрывать; неизвестные source-cap/compatibility rules блокируют только зависимые операции. Полноту ordinary/hive/machine/mobile нельзя объявлять по ordinary fixture.
4. Шапка/управление/армии/Arkship и holdings требуют своих реальных систем. Они остаются в полном target; отсутствие их данных не блокирует первый реальный UI slice и не разрешает создать новые gameplay системы без определения границ. Полное подключение закрывать по coverage всех требований, а не внешнему сходству.
5. Каждый готовый этап: independent actual-diff/contract review → исправления/re-review → относящиеся проверки → содержательный commit. Доставка — clean exact task commit с URL/checklist/ограничениями; master не трогать.

## Проверки и запуск для исполнителя

На **runtime** базе, не на master M2; перед source-проверками локально задать `STELLARIS_GAME_ROOT` на read-only установку 4.5.2. Иначе live checks не запустятся, а acceptance пропустит source-сценарий; пропуск не считать PASS покрытия:

```powershell
pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts
dotnet build src/Stellaris.Server -c Release
node --test tests/client/m3-planet-runtime.test.mjs tests/client/m3-development-tables.test.mjs tools/asset-probe/m3-ui-planet-panel.test.mjs
dotnet run --project tests/Stellaris.PlanetConstructionLiveChecks -c Release
dotnet run --project tests/Stellaris.ConstructionChecks -c Release -- --game-root $env:STELLARIS_GAME_ROOT
dotnet run --project tests/Stellaris.PlanetAcceptanceChecks -c Release
```

После изменения wire/Session добавить M3SessionChecks/M3ServerChecks по связанному сценарию; изменения economy проверить PlanetEconomyChecks. Новые meaningful tests: geometry/overlay/неинертная карта; identity при completion; focus после live catalogue updates; exact actions и guards, повтор/stale/foreign/observer; stock-change confirmation, caps/reservations/reorder/N=0/потеря N, monthly boundary и disable/replace/repair effects в соответствующем этапе. Не адаптировать assertions так, чтобы старые locked-карточки стали требованием нового каталога.

Запуск из output выбранного task commit на проверенном свободном порту (пример 5226, **сначала проверить занятость**):

```powershell
dotnet run --no-build --project src/Stellaris.Server -c Release -- --mode m3 --game-root $env:STELLARIS_GAME_ROOT --urls http://127.0.0.1:5226 --join-code Planet-menu-local
```

`STELLARIS_GAME_ROOT` пользователь задаёт локально. `/health` должен вернуть protocol 2/mode m3; `/m3.html`, JS/CSS/modules должны быть из точного commit (сверить SHA256 served/source, отсутствие 404/console errors). Не использовать docs HTML или tools preview как доказательство integration. Не останавливать пользовательские серверы; собственный процесс завершить после QA либо явно передать его пользователю.

Ручной checklist: два игрока+observer, настоящий local import 4.5.2 и обычный старт без fake stocks/all-tech; открыть ту же колонию с карты/outliner; пять вкладок; drag/resize/min/узкий viewport/scale; правые панели наружу↔внутрь; карта pan/zoom/select и внешний клик; другая колония/объект/close/reopen; заказ/оплата/queued slot→completion/экономика; upgrade/cancel и ещё поддержанные операции; недостаток одного/нескольких ресурсов и live unlock; сохранение focus/scroll; disconnect/reconnect/stale/POV; ошибочная/чужая команда без списания. Снимки хранить только локально в ignored `content-local/references/`, в отчёт ссылки/хеши. Synthetic fixture не заменяет живую игру; полная native численная/Chrome/GPU/OS-permission приёмка этим ревью не выполнялась.

## Доказательства ревью и открытые зависимости

Прочитаны обязательные правила и тематические требования, actual client/DTO/projection/adapter/runtime/build files; независимый `/root/backend_contract_review` подтвердил server gaps и проверил сохранённый отчёт без оставшихся конкретных замечаний по своей области; ведущий перепроверил dispatch, guards, projection и Core reorder. На неизменённом `4a55ff9` запущены три Node-файла из команды выше: **34 PASS, 0 FAIL, 0 SKIP**. Сборка/сервер/браузер не запускались: production не менялся, это ревью подготовки, не аттестация runtime.

Реальные зависимости: явная фиксация runtime-base и владения shared files перед кодом; source-backed live caps/compatibility и modifier N для соответствующих типов; новые безопасные preview/revision/holding projections; реальная модель отсутствующих систем шапки/вкладок. Общих нерешённых вопросов логики L01… нет. Клавиатурные детали и внутренняя Arkship-композиция отложены контрактом и не блокируют ordinary slice. Настройки lead/reviewer не менялись; фактические usage/effort metrics недоступны.
