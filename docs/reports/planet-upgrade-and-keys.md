# PLANET-UPGRADE-AND-KEYS-001 — attempt 1

Контракт до кода, 13 октября 2026. Изолированная ветка feature/planet-catalog-runtime, clean база 769f40a3afadfcfc1f20e8fc49df032b9095fc6d после независимой приёмки baseline. Commit/push/integration запрещены до нового independent review. Visual UI и research engine не принадлежат этому этапу; existing Session/protocol/codec — один writer здесь, research owner создаёт отдельные modules.

## Lifecycle и граница источников

Human prototype rules из 3a226bf: источник остаётся установленным с jobs/effects/upkeep до завершения; оплаченная цена фиксируется при заказе, скорость live/daily/no carry; завершение атомарно заменяет definition в том же slot и zone до monthly settlement. Cancel/condition/ownership loss сохраняет старое и возвращает paid costs по caps; destruction без refund. Source edge и target tech/potential/allow/destroy проверяются enqueue + pending; can_build=no запрещает только from scratch. Эти lifecycle правила — human decision, не native evidence. Source definitions задают graph/recipes/prerequisites, composite costs/upkeep не обнуляются. Unknown блокирует зависимый target.

## Добавочный wire proposal для root/UI

Новая strict команда enqueueUpgrade: ColonyId, ExpectedColonyRevision, SourceDefinitionId, DefinitionId (target), SlotId, ZoneInstanceId, OrderId. Старые команды сохраняют shape и reject extra fields. Сервер сверяет source definition/edge/current installed slot/zone, owner/assets/phase/revision/receipt и bank. Нельзя превратить upgraded target с can_build=no в from-scratch construction.

Optional installed `Upgrades`: target/source definition, slot/zone, expected revision, server quote, CanAfford nullable, CanEnqueue, Gates/reason, CommandType. CanAfford отдельно от source eligibility; action только enqueueUpgrade при доступном operation, клиент не источник цены/условий. Optional order UpgradeFromDefinitionId/ZoneInstanceId сохраняют связку source/target. Старые fields/WorkUnit не переименовываются.

LocalizationKey/DescriptionKey добавляются source-bound к job/economy/installed/queue/features/catalog/startup. Для jobs — конечные проверенные bindings job→source localization keys, не IconAlias и не универсальное вычисление job_prefix. Description без подтверждённого source key остаётся null. Локализованные тексты, reader и активные неизвестные swaps здесь не изобретаются.

## Research seam proposal — без cross-writer

M3ConstructionContent.Context получает optional immutable IReadOnlySet<string> researchedTechnologies; existing starting set — только когда настоящий provider ещё не подключён. M3GameSession получает optional доверенный read callback по participant/country и создаёт M3ConstructionEvaluation на Project (около 310), Execute (584), AdvanceDay (704). Callback вызывается под session gate, его set копируется существующим ConstructionContext. Research owner обновляет свой настоящий country state из monthly science; update order/atomic candidate должен быть согласован при интеграции его modules. Здесь нет set/unlock tech wire, all-unlock или QA preset. Unit explicit tech fixture допустима, manual readiness до actual research integration не объявляется.

## Приёмка

Source-backed unit fixtures: старое до/во время работы, cancel/refund без замены, completion ровно один target и exact slot-zone, composites, altered source/target/order replay, stale/owner/observer/assets, false pending conditions refund, no from-scratch bypass. Session candidate: atomic bank+installed+economy/month, no duplicate jobs/upkeep, zero/user limits сохранены, rollback при dependent failure. Optional wire/metadata strict/immutable/source key checks. Real transport/Release после changed code; независимый actual-diff review перед commit. Live/manual upgrade только после настоящего research acquisition.

## Реализация / focused evidence, 14 октября

Проверяемый startup capital → major unit уже прошёл pending/cancel/refund/tech-loss/completion/next month/next city mutation; фактический installed graph остаётся единственным источником structural refresh и не восстанавливает old capital. Population allow берётся из импортированной source population 3200 (в текущем bounded runtime нет population growth); missing population остаётся unknown, 2499 не проходит source threshold 2500. Указание can_build=no обходится только после проверенного source edge в upgrade path. Additive cost blocks сохраняют minerals + strategic resources с отдельными upkeep. Исходные special country/species modifiers пропускаются только при доказанно false potential по полным scoped facts; active/unknown остаётся gated.

Конечные localization bindings 165 buildings / 29 zones / 31 jobs / 19 resources / 4 districts / 11 features проверены на exact keys original English 4.5.2; строки не хранятся, RINFIX исключён. Source metadata evidence в ignored content-local/catalog-runtime/localization-binding-evidence.json. Runtime reader не создан. Name/description/plural не берутся из IconAlias; неизвестный ID не получает вычисленный job prefix. Base/default source job branch сохраняется; неизвестные active swaps не выдумываются. Owner economy `JobDefinitions` включает все 31 source definitions, включая неназначенные/gated, для lookup JobCapacity IDs. Unknown recipe отмечен typed gate, без нулевого fake recipe.

Все catalog zones сохраняют Kind=zone, real icon/source keys и lifecycle gate. UI root согласовал readonly просмотр 29 definitions из реальной startup zone identity, без псевдо building SlotIds или выдуманной replacement compatibility. Новый исполняемый zone operation не добавлен.

Исследования: accepted dependency d74fec1 не переносился в dirty stage. Текущий read callback researchedTechnologies(participantId) потребляется под session gate и копируется в ConstructionContext; StartingTechnologies fallback используется только до подключения настоящего research driver. Owner API Start Initialize / post-AdvanceMonth PrepareMonth(beforeBalances,netFlows,areaSpeedBonuses) и atomic reserves/Completed/effects/epoch update будет связан отдельным stage после independent acceptance. Никакого selectResearch/unlock wire или ложного manual research PASS в этом diff нет.

## Freeze — 14 октября, ready for independent review

Код заморожен на base HEAD **769f40a3afadfcfc1f20e8fc49df032b9095fc6d**, feature/planet-catalog-runtime. Исполняемый backend candidate завершён; независимое review/commit/root integration ещё не выполнены. Ownership, текущее дерево и исходный clean state сохранены. Commit/push/main/server deploy не делались. User 5192 не менялся; только изолированный root-approved ephemeral test harness, clean shutdown подтверждён.

### Final outcomes

| Команда (GAME_ROOT read-only) | Log в content-local/catalog-runtime | Результат |
|---|---|---|
| dotnet run --project tests/Stellaris.PlanetEconomyChecks -- --game-root GAME_ROOT | upgrade-key-source-checks.log | exit 0, 8 PASS, 0 FAIL; 259 finite key bindings проверены на exact original keys, unknown job не получает prefix |
| dotnet run --project tests/Stellaris.M3SessionChecks -- --game-root GAME_ROOT | upgrade-full-session-checks.log | exit 0, 15 legacy suites + source startup/economy/live/extended hydroponics + startup capital upgrade PASS |
| dotnet run --project tests/Stellaris.M3SessionChecks -- --game-root GAME_ROOT --upgrade-only | upgrade-session-checks.log | focused upgrade PASS; последующий full run содержит новые population/unsupported-country assertions |
| dotnet run --project tests/Stellaris.M3SessionChecks -- --game-root GAME_ROOT --long-automatic-only | long-automatic-before-fix.log / long-automatic-after-fix.log | до fix: elapsed=4096/capacity-limit; после fix: 6000 automatic days без receipts + paid build/upgrade PASS; final full Session run тоже PASS |
| dotnet run --project tests/Stellaris.ConstructionChecks -- --game-root GAME_ROOT | upgrade-construction-checks.log | exit 0, 53 PASS / 0 FAIL / 1 symlink capability SKIP |
| dotnet run --project tests/Stellaris.PlanetConstructionLiveChecks -- GAME_ROOT | upgrade-live-checks.log | exit 0, source 3 presets/quotes/paid lifecycle, exact slots/refunds/guards PASS |
| dotnet run --project tests/Stellaris.M3ServerChecks -- --game-root GAME_ROOT | upgrade-real-transport-checks.log | exit 0, **503 PASS**, real HTTP/WS/source monthly funding/private observer/reconnect, strict upgrade shape/required/null/foreign quote fields, clean shutdown |
| node --test tests/client/m3-client.test.mjs tools/asset-probe/m3-ui-planet-panel.test.mjs | upgrade-client-regressions.log | exit 0, 150 PASS / 0 FAIL / 0 SKIP; UI files не менялись |
| dotnet build src/Stellaris.Server/Stellaris.Server.csproj -c Release | upgrade-release-build.log | exit 0, **0 warnings / 0 errors** |
| git diff --check HEAD | — | PASS |

Capital unit fixture использует настоящее source startup graph/population **3200**, установленный startup capital, конкретную дополнительную tech_colonial_centralization и отдельно явно funded test balances (без fake production). Подтверждены fixed paid debit, receipt replay/conflict, stale/foreign/observer/source/zone/edge guards, запрет can_build=no from scratch, pending old capacity/installed identity, cancel paid refund/old remains, tech-loss pending refund, atomic same-slot/zone target, politician capacity rise и zero limit. После next month и завершения нового city district target остаётся единственным installed в slot, старое не восстанавливается; monthly building upkeep равен installed sum. Это unit integration evidence, не ручной research/upgrade PASS.

Composite source quote fixture foundry1→2 подтверждает **510 minerals + 85 volatile_motes** с functional-architecture cost modifier и upkeep **5 energy + 2 volatile_motes**; вторые resource cost/upkeep containers не потеряны. Capital source threshold **2499** отклонён, missing population unknown; synthetic active corporate country modifier не отбрасывается и блокирует target.

### Ограничения / следующий owner stage

Позднее прямое указание пользователя от 14 октября: ручная приёмка выполняется после объединения всех систем в isolated experimental RC. Отсутствие отдельного branch manual upgrade не задерживает интеграцию этого среза после exact commit + independent review + automated checks. После общего merge нужны сквозная сборка/browser QA и единый URL/commit/checklist; stable main до общей human приёмки и отдельного разрешения не обновляется. Это условие передаётся root/RC-координатору этим handoff.

- Live research acquisition ещё не подключён в эту ветку. Provider/read seam готов, обычные starting technologies не изменены. Принятые source research modules d74fec1 и checklist 3353cbd сохраняются как evidence; после нового handoff full technology a58a5f6 самостоятельный research integration stage приостановлен root до выбора canonical engine; no all-unlock / QA preset / generatedworld/lobby/research UI scope здесь.
- Manual/browser upgrade и effect changes на реальном researched technology пока не проверены. Source medical2 remains gated из-за doctor-upkeep/условных strategic gas flows; gates не ослаблены ради выбранной цепочки.
- Zone catalog 29 real source icons/keys доступен readonly; совместимость/zone replacement lifecycle не выдуманы. Active special job swaps, отсутствующие в accepted base context, не выбираются по IconAlias.
- Базовая soldier capacity сохраняется, только прежний precise governor-dependent capital term исключён одинаковым shared policy. Human lifecycle не объявлен native engine proof.
- Asset profile/security limits unchanged. Новые asset/world visual owner paths, localization reader и других checkout не редактировались.

### Critical empty-queue day-receipt follow-up — final candidate

Прямой root finding после первого freeze воспроизведён `--long-automatic-only`: Session automatic Tick (тот же authoritative clock path, который вызывает server Runtime.TickOnce) остановился ровно на elapsedDay=4096: Construction day rejected: capacity-limit. Ежедневных stepDay-команд, reset сессии, fake production или искусственного stock funding нет. Только обычные source WorkforceLimit choices делают месячную экономику solvent.

Minimal production fix: после SynchronizeRevision в AdvanceDay пустая PendingOrderIds сохраняет runtime candidate и пропускает только adapter.AdvanceDay/Stage. Обычный общий day commit по-прежнему синхронизирует final participant/colony revision, refresh graph и monthly settlement выполняются как раньше. Непустая очередь продолжает eligibility/recheck/refunds/daily work/completion. Core Apply/receipt limit 4096 и outer command limit не изменены.

После fix та же сессия прошла **6000 automatic source days**, у обоих runtime receipts.Count=0 и revisions равны owner view revisions. Из настоящего накопленного банка оплачена и завершена hydroponics_farm; затем оплачено и завершено startup capital→major в той же сессии через automatic Tick. Для target technology используется только explicit unit prerequisite fixture (не manual acquisition). Focused log `long-automatic-after-fix.log` и финальный full Session log exit 0. Final real transport повторён: 503 PASS/clean shutdown. Final Release: 0 warnings/errors. Source identity/keys/Core limits не менялись этим follow-up.

Изменены два уже owned пути: src/Stellaris.Session/M3/M3GameSession.cs и tests/Stellaris.M3SessionChecks/Program.cs. Старый frozen patch a3c964fc9cf2f1f8bd548d3847b8004112963dbd0ef0876e38b811769deeec02 сохранён в `upgrade-freeze-before-idle-fix.patch`; final hashes ниже его заменяют. Review должен учитывать именно этот финальный минимальный follow-up. Исторический failing log сохранён, не переименован в PASS.

Существующий лимит 4096 actual construction/user receipts сохранён: исправление устраняет расходование его пустыми днями, не вводит бесконечный cache. Масштабирование истории непустых очередей — отдельная политика, не молчаливое снятие guard.

### Evidence / hashes / actual owned paths

Final frozen code patch (без собственного отчёта): `content-local/catalog-runtime/upgrade-freeze.patch`, SHA-256 **aeb8e814bc156bccd16df6ef9664703b4fee4e2a3dc480fe73c48ca4b4689035**. File manifest: `upgrade-freeze-files.json`, SHA-256 **181d07b72fe3c088c7e06cdf3ce0d03680111929052e4e1063a25cdca4d0e4dd**. Собственный report исключён из рекурсивного file hash. Все actual owned paths ниже; новые Core metadata/policy файлы включены в diff intent-to-add. Commits/integration/review автором не выполнялись.

| Путь | SHA-256 | Bytes |
|---|---|---:|
| `docs/M3-WORK-OWNERSHIP.md` | `a5c92aa6329eca5f49a3c1743af5b3db794588fb3ca54d1a67d35c7931f5b40d` | 17719 |
| `src/Stellaris.Core/Construction/ConstructionAssessment.cs` | `48b713fe7787c86551234d5846d6b4aa1938f84e2734318fd02b0a20deef4a39` | 41839 |
| `src/Stellaris.Core/Construction/ConstructionEconomyTermPolicy.cs` | `85d2f874dcd442d54fb191eeda46aa2c126332a6b279d10e988939807aaf0cf5` | 728 |
| `src/Stellaris.Core/Construction/ConstructionLiveConditions.cs` | `6fd7060aed521250c7fc8140a8087c9bc26a7d67c5f934f0dceb762139c5fdda` | 3222 |
| `src/Stellaris.Core/Construction/ConstructionLiveContent.cs` | `462d631fb77f3fa0fe5ce1858348db6651c2a0fb9f17834ba1bb065a5a9e2354` | 8637 |
| `src/Stellaris.Core/Construction/ConstructionLiveRuntime.cs` | `e62113f7ba0fe3016bb100542983909821a49759b6b0f4a8d2b0d0223ea4a50b` | 17674 |
| `src/Stellaris.Core/Construction/ConstructionTypes.cs` | `c805da63b49744144fb7e3ae4e7eecdb6dc181b9f861d7c432961a1f1ceeeb91` | 4839 |
| `src/Stellaris.Core/Content/PlanetEconomy/PlanetEconomyImporter.cs` | `df0a933ecffbb655fc28ba0f21c657c68d981b4f39aa4d167c11a6657aade4a5` | 81610 |
| `src/Stellaris.Core/Content/PlanetEconomy/PlanetEconomyTypes.cs` | `e0df7b2a559a71292eb43a69a4d892eaea337775309ec447f4453be5990a71eb` | 11727 |
| `src/Stellaris.Core/Content/PlanetEconomy/PlanetLocalizationBindings.cs` | `5871a2113a55e885fbc29effba112f28a94642a0d526d1f079f05fc7833dbe34` | 31462 |
| `src/Stellaris.Server/M3Transport.cs` | `37b3c1a15817249f7f3196d967cd5aef688c1bbefc10f8504922619004247f2a` | 11206 |
| `src/Stellaris.Session/M3/Construction/M3ConstructionAdapter.cs` | `bbd7ae9d8420350314496e613ace87bd02a16518cc1476cdbc78c12d1b8d6b12` | 22012 |
| `src/Stellaris.Session/M3/Construction/M3ConstructionContent.cs` | `2b1b964df3fddff969cfd2ac670535891bfa9a8abff70a41ba6661276c7774e3` | 14888 |
| `src/Stellaris.Session/M3/M3BaselineEconomyProfile.cs` | `1211d35f9fce6ddad5a845c1f439c0727a59517752c1651dd9bdbbaf76af389b` | 33790 |
| `src/Stellaris.Session/M3/M3DevelopmentProjection.cs` | `08851baaaab2d57a203cece6e18d398002350ada64cc98c1685a79cd4e742a80` | 10328 |
| `src/Stellaris.Session/M3/M3DevelopmentProtocol.cs` | `c6b1c1e886301c7e161c4f28a8ed815f248f2f165bff37692d6efdc767ce14b1` | 7035 |
| `src/Stellaris.Session/M3/M3GameSession.cs` | `fa62460e4c8993b66cde54ff9718c5d61c56201c2a17901f8d1de6aed47d29f5` | 61271 |
| `src/Stellaris.Session/M3/M3Protocol.cs` | `9d49d2ad22c001670ebbdf9e4396e655d1da00650890d7ce36ad26de0c4505d5` | 4838 |
| `tests/Stellaris.M3ServerChecks/Program.cs` | `0d2427aeddc65ead899d13b2038723794abb286a07879751048aada0e55dba34` | 57154 |
| `tests/Stellaris.M3SessionChecks/Program.cs` | `20e2617accfef9f8e1abb7e3b1d2f5ff4a5b27d2b3e05339a668b6d5bca0d249` | 107268 |
| `tests/Stellaris.PlanetEconomyChecks/Program.cs` | `40b67e76c006907c0b22fcba706589c24d7b93549bc6f3ca906f7b1c9b41751f` | 50362 |
