# Объединение систем: экспериментальный RC

## Исправленный порядок лобби — 14 октября

Код `bf41416`: backend7c7de7b и UI54732ff/bf41416 прошли независимое ревью. Apply/Prepare скрыты; Start доступен хосту после готовности всех игроков, подключение/отключение зависят от транспорта. Общие настройки публикуются автоматически, у остальных read-only. Изменение настроек снимает голоса. Все Ready до генерации; один человеческий Start запускает generation и после проверки файлов автоматически завершает startup.

Actual IAB5204: economic host/scientific peer/approvedobserver до импорта. Connect/disconnect/reconnect и скрытые кнопки PASS; input-only seed виден readonlypeer; hostReadyalone Startdisabled, bothReady Startenabled до world/assets. Изменение seed снимает оба голоса, оба снова Ready. Единственный человеческий Start→generation→File933/235.3MiB/464locale0diag у каждого→automaticrunning без повторных Ready/Start. Карта/своя колония/флот/управление временем появились. Снимок ignored `content-local/references/rc-ready-first-bf41416/running-host.png`. Независимый LOCobserver: File933/235.3MiB/464locale0diag, privatehostresearch3disabled/generalclearsprivate, consolewarn/error0. Все тестовые входы очищены. Живой Folder permission/revocation остаётся ручной проверкой; модульный Directory и фактический File не объявляются её заменой.

Release publish0warnings/errors; client/connection231PASS; реальный HTTPWS574PASS/clean shutdown. Huge1000/density3/all5POVs/history64/pendingboth max1008256bytes/headroom40320 до1MiB. Артефакт `artifacts/rc-manual-bf41416` для свежей5204; старые пользовательские5200/5201/5203 и5192 сохраняются. Stable/main не обновлён, требуется ручная приёмка и отдельное разрешение. Ниже исторический checkpoint неверного порядка, не доказательство нового контракта.

Свежая человеческая сессия5204 PID41664 запущена из rc-manual-bf41416; healthprotocol2/m3 PASS, servedclient/connection SHA совпадают с проверенным source, stderrempty. Fresh UI Connecttrue/Disconnectfalse/Startfalse. OwnQA41800 остановлен после проверки identity/очистки всех трёх тестовых входов.

## Историческая передача 45fa87d — заменена исправлением выше

Версия кода `45fa87d83e1e9fcdce840341f281e8d04af18e2e`, отдельный `artifacts/rc-manual-45fa87d`, localhost5203/m3.html, свежая сессия PID36920. Пользовательские5200/5201 сохранены. Stable/main не обновлён; ручная приёмка и отдельное разрешение ещё обязательны. Ниже сохранена история, которая не отменяет этот checkpoint.

Собраны все семь редких ресурсов, source producer composites и candidate-world research closure; веса по реализованным месторождениям колоний — согласованная политика прототипа. Independent actual diff+artifact seal закрыл genuine source full-pool путь: исследование газа, producer за425 минералов из заработанного запаса, выпуск из нулевого газа, реальное исследование lab2 и upgrade за42.5 газа+510 минералов, old-building/atomic replacement/recurring upkeep. День77486: газ212.8, выпуск.275/месяц, содержание1, net−.725; это не обещание бездефицитной rare экономики. Ignored freeze и честная tool transcript лежат в galaxy-generation/artifacts/rare-resource-session-final; никакие stocks/technology grants не использовались.

Повторная блокировка Ready исправлена StateSequence-watermark подтверждения файлов: более ранний snapshot не отзывает принятый ACK. Независимое LOC review, red regression и24connectionPASS. Autostart UI independently reviewed/re-reviewed: одна кнопка Start последовательно Apply→Prepare→currentmanifest/ACK→ownReady→explicitpeerReady→Start. Blank seed, отказы, отмена импорта/picker, disconnect/right/candidate changes проверены. Bounded readiness status показывает текущую причину ожидания.

Actual IAB `e19bfca` с тем же финальным UI:2 scientific players+approvedobserver, единственный hostStart при пустом seed, server-resolved866f…14a7, automaticPrepare,919files228.9MiB/464locale0diag, automaticHostReady, только peerReady, automaticStart. Native200map/owncolony/fleet, population3200/size20, native district/building catalog, mouse research и observer read-only/POV/private clearing подтверждены; console warnings/errors0. Evidence ignored content-local/references/rc-e19bfca. Ранее предполагаемое самозакрытие колонии оказалось кликом в перекрывающую панель исследований: после её явного закрытия колония доступна; неподтверждённый code fix не делали.

Release0warnings/errors; combined client/connection221PASS; source Session13PASS, all5POVs/huge1000/density3/history64/pending каждогоowner max1007955bytes при1MiB. Full real HTTP/WS566PASS и clean shutdown, ignored rc-final-http-ws.log. Последний backend fix independently sealed: generic refusal доставляется перед bounded graceful close, вместо немедленного Abort;40real auth refusals и прежние MAX/send/privacy guards проверены. Финальный artifact отличается от browser-tested `e19bfca` только этим server rejection path; UI/source-economy trees совпадают.

Ручной путь: открыть5203 в двух окнах → подключиться/выбрать пресеты → hostStart → импортировать локальные файлы в каждом окне → второй игрокReady → автоматический переход в игру. Игра начинается на паузе. Проверить колонию/Workforce/construction/research/fleet; Folder permission/revocation остаётся human manual.

Ограничения: для четырёх особых rare текущий мир может не иметь источника; nanite upper cap неизвестен и cap-dependent credit/refund получает atomic typed gate. File research icons/panorama могут отсутствовать с явной диагностикой; zro icon text fallback. Native effect labels/scopes частично raw/unsupported с bounded ledger; не fullLOC/nativePASS. Visual approximations, unknown driverVRAM, history4096 и будущие10–20players не покрыты. Без боёв/сохранений. Полная ручная приёмка ещё не проведена.

14 октября 2026. Координатор и единственный общий интегратор RC: чат `01a13770-e8f0-7df2-8002-e456e71a74f8`. Прямое поручение пользователя передано из `01a134dc-b6ec-7d23-a3de-9eccabdb337f`. Предыдущий координатор планет сохраняет планетный поток. Старые назначения не дают права параллельно менять RC. Новый монитор не создаётся.

## Граница и приёмка

Ветка `integration/playable-systems-rc`, исходная база `b576a6ec70ad4d7d96e33ae47c4a29b13a85da1a`. Основной checkout `feature/m3-static-visuals` и `master` сохраняются. Готовые закоммиченные срезы разрешено объединять после независимого ревью в этот экспериментальный RC до общей ручной приёмки. Перенос в стабильную ветку требует ручной приёмки и отдельного явного разрешения. Push не разрешён. Сервер 5192 не трогать.

Цель: игровой мир запускается из настроек лобби; доступны хотя бы seed/size/shape/density; отдельный выбор стартовых зданий убран, нужные traits сохранены. Звёзды, планеты и луны генерируются и визуализируются из собственных локально импортированных ассетов клиента. Работают исследования, source gates улучшений, строительство/каталог/окно колонии, базовый флот и навигация. Сквозной сценарий проходит без критичных ошибок.

Сохранить атомарный старт, ошибки генерации без частичного мира, готовность клиентов после проверки локальных ассетов, точную домашнюю планету и её source startup/economy, права игрока/observer, replay/stale/reconnect. Неизвестные native правила и неподдержанные эффекты обозначаются явно, не подменяются defaults. Исходные и производные ассеты игры не входят в Git или загрузки.

## Срезы и остаток

| Поток | Подтверждённый кандидат | Граница |
|---|---|---|
| Технологии | `a58a5f6`, база `3a226bf`, контракт `8213c67` | Владелец подтвердил чистоту, независимое ревью и проверки; переносить полную цепочку startup/economy/construction, не только последний commit |
| Генератор Core | `07975d9` и четыре предшествующих producer commits | ReadyForBootstrap; independently reviewed; это ещё не запуск через лобби |
| Генератор Session/лобби | Adapter `047f6d7` → `1edc556`, projection `82b215e` → `89569cf` | Reviewed адаптер и bounded spatial перенесены; настоящий lobby/start handshake ждёт writer |
| Планеты/каталог | Catalog `d5d113a` → `0e7236b`, upgrades/keys `706deef` → `451fe8b` | Source runtime, explicit upgrade и exact keys перенесены после конфликтного review |
| UI колонии | До `394d991` → `d5eb6c4`, explicit `52e003e` → `5d71235`, RU budget `b051402` → `9210486`, source keys `f40fe6f` → `ce3dca4` | Research сохранён; research-localization ещё у UI владельца |
| Визуализация generated world | `feature/generated-world-visuals` | В работе у владельца; dynamic manifest/renderer согласуются с генератором |

Уведомлены все шесть действующих владельцев, запрошены hashes, review/check evidence, ограничения, зависимости и пересечения. Дополнительных исполнителей реализации не назначено. Независимый read-only reviewer `/root/rc_merge_review` проверяет фактический состав переноса и стыки; модель/effort не менялись, фактический backend usage недоступен.

На `451fe8b` shared Session/Protocol/Server переданы technology owner `01a136f7-42c1-78a0-ac3b-511a6fb97697` для feasible source effects и устранения server research localization. Он работает в отдельной ветке `feature/technology-source-effects`; затем writer переходит Galaxy owner через RC. UI owner работает от `89569cf` в `feature/planet-ui-explicit-upgrades`, backend/renderer не пишет. Existing heartbeat `stellarisserver-m3` уже ACTIVE с target общего RC по safe handoff прежнего координатора; нового нет.

## Проверки первого переноса

После независимого actual-diff review `/root/rc_merge_review` зафиксированы technology merge `6932609`, Core galaxy merge `2cb85a2` и отдельный adapter `1edc556` из `047f6d7`. Код кандидатов сохранён буквально; дополнительно сохранён существующий stable текст ручной приёмки. Родитель adapter `b01523c` не переносился: он включает другой исследовательский dependency. Canonical research — `a58a5f6`. Whole planet `25d3cb6` также не переносить; numerical helpers `d74fec1` адаптируются отдельно к одному runtime после текущего catalog freeze.

- Release Server/Core/Session: 0 предупреждений, 0 ошибок; output только в игнорируемом `artifacts/rc-server` этого worktree.
- Node client/planet/research/images с локальной установкой: 176 PASS, 0 FAIL/SKIP.
- ResearchChecks с настоящими определениями 4.5.2: PASS. Первый вызов использовал неверный CLI флаг; исправлен на positional game root, это ошибка команды проверки, не дефект продукта.
- M3SessionChecks с source: PASS, в том числе startup/economy/construction/research/private DTO.
- GalaxyGenerationChecks с настоящими 4.5.2 и внешним проверенным Triangle helper: 78 PASS на combined Core tree.
- Повторная combined Release после Core: 0 предупреждений, 0 ошибок.
- `git diff --cached --check`: PASS.

Core merge содержит семь унаследованных лишних пустых EOF в frozen source-evidence Markdown; evidence не переписывалось. Проверка code/tests/tools whitespace PASS. Combined adapter/projection — 6 PASS: small spatial216281 bytes, huge514027 bytes, права и actual route/month сохранены. Это только spatial payload, не весь будущий wire envelope. Генерация не подключена к Session.Start; producer PASS не означает playable RC.

## Последующие проверки и исправленные стыки

- Catalog combined: source economy8 PASS, source asset/profile16 PASS; полный source Session15 базовых наборов плюс source research/startup/economy/construction/extended building PASS.
- UI combined после конфликтов: Node203 PASS/0SKIP, source LOC17 PASS; Release0/0. Перенос сохранил atomic locale/graphics staging, bounded research image store, stale/POV/pending guards. Независимый reviewer принял итог после исправления canAfford и literal native upgrade icon action; unknown и insufficient серые/disabled.
- Explicit upgrade combined: source capital payment/pending/cancel/tech-loss refund/atomic replacement/month/graph/current startup occurrence и keys PASS. 6000 автоматических source дней без пустых construction receipts, затем actual-bank paid build/upgrade PASS; guard limits не повышались.
- Настоящие HTTP/WS проверки: 533 PASS, clean shutdown на собственном ephemeral listener. Интеграционный harness сохраняет одного reader; malformed input не считается отказом при неожиданном ACK.
- Source research после explicit UI consumer: PASS. Существующий synthetic funding fixture перенесён до server canAfford projection; проверки consumer/quote/commands не ослаблены. Synthetic cost1/funding не доказывают ordinary-start → естественное длительное исследование → paid upgrade; это отдельный ручной сценарий.
- Точный runtime checkpoint `9210486`: combined Node client/panel/DOM/research/images/localization с настоящим source root — **242 PASS, 0 FAIL/SKIP**; Release Server/Core/Session — **0 предупреждений/ошибок**. Test fixture-order fix `b342c78` независимо принят; LOC research contract `13169e6` → `2c79458`. Последующие документы runtime не меняют. Не суммировать пересекающиеся наборы разных checkpoint как количество уникальных проверок.
- Следующий runtime `ce3dca4`: source-key UI consumer `f40fe6f` перенесён без конфликтов после независимого `ui_parity_contract_review` и чтения фактического diff интегратором. Полные job definitions/plural keys для ёмкости, sourceSocialClassId отдельно от экономической категории, полученные ключи ресурсов/owner-scoped topbar. На combined tree client/runtime/panel/DOM **212 PASS, 0 FAIL/SKIP**. Research producer/consumer и browser gate остаются pending.

Actual-diff reviewer `/root/rc_merge_review` исправил/повторно принял стыки catalog occupied slots, affordability/icon action, текущий StartupBuildings target после upgrade и single-reader transport harness. Original owner reviews остаются в тематических отчётах. UI explicit consumer — independent `ui_parity_contract_review`, исходный `52e003e`.

## Остаточные интеграционные gates

Позднее human решение14.10 «пока только пресет»: после P2 review (configure traits только отображались, source pipeline использовал preset traits) пользователь выбрал фиксированные пресеты. Production custom traits editing/configure убираются, source traits пресета сохраняются; mixing/replace policy и полный редактор не реализуются. Galaxy/UI owners назначены на bounded follow-up, affected review требуется. Native visual e60→RCb1fc8ee, lifecyclefix6cac→RCd3424da: independent repairs accepted, actual31classes/8worlds/rollback/freshlegacy и strict samecanvas generated→generated/legacy GL0. Старый1282 исправлен resetState перед dispose без подавления ошибок;16 renderer regressionsPASS. FullSession final log848172 vs earlier851069 — оба<1MiB, автору поручено уточнить provenance.

UI Galaxy `0f12a06` → RC `2b13e67`: settings/prepare/currentmanifest/fingerprint ACK/ready/start/inspect, buildings selector removed/traits preserved, DTO IDs без constants. Independent LOC review нашёл P1 logout sequence floor и P2 explicit reconnect replay; оба исправлены, affected rereview accepted (3 targeted PASS). Owner transport22/client183 PASS, 6-file freeze diffhash19f9d5864bec027794a7e8e871deeae2989cee4c. Без конфликтов, backend/visual ещё не объединены; общий browser PASS не заявляется. Visual owned `e60eb177` готов с actualGPU31classes/8worlds/freshlegacy/rollback evidence, но перед переносом root потребовал production-equivalent logout/samecanvas reimport из-за отдельного1282 lifecycle эксперимента.

14.10, новое прямое resume: завершить системы и подготовить ручной прототип; scheduled task остаётся PAUSED. Galaxy/UI writers возобновлены, UI дополнительно назначен точечный m3-connection.mjs handshake. Independent visual_repairs_review принял memory/material repairs freeze8af8001d и FileSnapshot exact-path repair freeze210eec6e (дельта только worker+integrationtest, остальные28hashes прежние). Последний дефект обнаружен actualbrowser: basename-only source reader создавал ложный DDS alias; production479sourceimport после исправления1PASS/0skip. ActualGPU31classes/8worlds/legacy ещё pending. Root отдельно напомнил требование genuine research→sourcequote paidupgrade без synthetic tech/cost/stocks. Одноразовый Full reset использовал UI по отдельному human поручению с согласованным ключом; success, fresh0%/0available, ignored marker сохранён, повтор запрещён.

Checkpoint `5f2f07c`: immutable UI consumer `e0fd1da` перенесён после independent LOC review (scoped ledger/POV/logout/diagnosticCode; hashes подтверждены новым LOC owner). Интегратор сопоставил actual production/test diff с d917 wire; focused client/research в RC — 191 PASS/0 skip, `artifacts/rc-research-ui-consumer.log`. Owner evidence — 235 PASS/Release0/0. Это закрывает source consumer, но не общий browser PASS. Galaxy/UI/LOC безопасно переданы свежим владельцам из CONTEXT-HANDOFF; прежние writer отозваны, visual handoff pending. Единственный heartbeat перенаправлен, reset при свежих 97% не использован.

Checkpoint `d9178a4`: приняты source effects/LOC producer `1870dfa` после independent `effects_review`, LOC `localization_research` и final combined `tech_combined_review`. Root исправил отображаемый idle rate на RAW, active rate сохраняет speed; добавил optional `effect.diagnosticCode` для неразрешённых значений и actual Session/wire fixture. Combined source `--effects-only`/`--research-only` PASS с actual explicit UI/cancel/replacement; 16 sets/3 presets, real HTTP/WS535 PASS/clean shutdown, Release0/0. Existing synthetic expected reserve теперь снимается после Select из active DTO, funding fixture preserved. Shared writer передан Galaxy owner от exact d917; research callback consumer ещё ждёт review/commit. Ниже численные эффекты уже реализованы в bounded single-planet/owner; сохраняются multiple-colony/unknown-effect gates. При смене общего координатора роли передавать по safe handoff, не запускать второго writer.

1. Численные эффекты — canonical adapter из d74 evidence без второй engine/двойных modifiers. Idle reserve получает raw выпуск; no-leader −0.25 остаётся existing source define в additive stack, не human rule.
2. LOC-RESEARCH-001: удалить серверное чтение original переводов, включая RINFIX; technology/effect DTO IDs/verified native keys/structured values, browser локализует только штатные464 файла. Producer — technology owner, consumer — UI owner. Planet source keys259bindings/560keys проверены LOC reviewer, это не подтверждает research consumer.
3. Galaxy prepare/settings → frozen generated fingerprint/profile → local assetReady/ready/start; stale candidate, template/settings invalidation, точный generated home, отсутствие fixed16 fallback. Proposed inspectGalaxySystem несёт fingerprint даже при возврате на карту; окончательные DTO/error codes передаёт owner.
4. Generated source graph/import/renderer: SourceMip512V1 только generated2D stored DDS mip, nativecube1024; global512MiB не повышать. Explicit NeutronNoSurfaceAtmosphereMixV1 не native proof. Source system-light metadata вместо общего G fallback; system.starClassId отдельно от body.worldClassId. Actual31classes/8world CPU/GPU/staging peak, offscreen WebGL и live GPU QA ещё pending.
5. Объединённые browser/locale/folder/observer/navigation/критичные ошибки и один чистый RC/checklist. Chrome/OS permission и настоящая ручная приёмка ещё не закрыты.

Логи проверок находятся в игнорируемом `artifacts/rc-*.log`. Первоначальная сборка показала отсутствие npm-зависимостей в новой копии; `pnpm --dir tools/asset-probe install --frozen-lockfile --ignore-scripts` установил зависимости, tracked lockfile не менялся.

## Ручная приёмка после объединения

Позднее прямое указание пользователя14.10: после передачи ролей выключить scheduled task и завершить работу. Прежний RC подтвердил PAUSED для stellarisserver-m3 с сохранением target/Reset. Все новые роли остановлены. Galaxy оставил5dirty/незавершённые Session helpers, Visual30dirty/freeze8af/GPU+rereviewpending. Архивирование старого UI автоматически удалило managed checkout и ignoredQA; restore_worktree отказал из-за missing snapshot. Тот же checkout восстановлен из сохранённой e0fd ветки; код/история сохранены, ignored logs/screenshots/junction не восстановлены, localhandoff реконструирован с disclosure. Старые managed чаты повторно не архивировать. Старый LOC восстановил fdef clean detachedcd873cf и7tracked модулей/tests/docs; app owner attachment не подтверждён из-за toolerror, handoff реконструирован из контекста (5160chars/D9FCEA04), прежниеignoredlogs не восстановлены. Это сохранение передачи, не новый этап реализации.

Пока не передавать пользователю как готовый RC: отсутствуют замороженные lobby/generated-assets/catalog/UI срезы и сквозная проверка. После объединения зафиксировать точный чистый commit, отдельный свободный порт, команды запуска и фактические ограничения.

1. Два игрока импортируют собственную папку 4.5.2; observer подключается read-only. Host меняет seed/size/shape/density, проверяются ready/invalidation и атомарный старт.
2. Проверить generated map и выбор систем, разные классы звёзд/планет/лун, иерархию луны и домашнюю колонию; отсутствие или ошибка ассетов даёт явный отказ/диагностику по контракту.
3. Открыть колонию: startup, население/Workforce, jobs, economy, зоны, здания, доступный и заблокированный каталог. Заказать поддержанную стройку, проверить списание, очередь, завершение/отмену/refund и месячную экономику.
4. Выбрать исследования трёх направлений, переключить тему, переподключиться, завершить тему; проверить резерв/избыток и реальные gates улучшения здания. Не выдавать synthetic cost completion из компонентного теста за этот сценарий.
5. Переместить флот внутри системы и по гиперлинии; проверить выбор/остановку/возврат камеры. Observer не может изменять мир; второй игрок не управляет чужой империей.
6. Проверить browser console/HTTP/WebSocket, stale/replay и отсутствие критичных ошибок. Стабильную ветку не менять до отдельного решения пользователя.
