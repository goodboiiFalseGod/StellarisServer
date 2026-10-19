# PLANET-ORDINARY-CATALOG-RUNTIME-001 — attempt 1, baseline freeze

Статус: **partial / baseline frozen для independent actual-diff review**. База и HEAD `77bc838766316aed43e5c7b2a8ca09302b19d3c2`, ветка `feature/planet-catalog-runtime`. Исходная назначенная копия была clean на `feature/planet-catalog-expansion`, `e32d79011ef484506d0e52f8b1846cffd6abc655`. Принятые `d259d12` и `e32d790` перенесены `cherry-pick --no-commit`; конфликты разрешены с сохранением `PlanetEconomyApplicationScope`. Commits, push, интеграция и собственный сервер не создавались. Автор не выполняет independent review. Настройки модели не менялись; фактические backend model/usage сведения инструментами не раскрываются.

## Реализованный baseline

Authoritative construction импортирует все **165 ordinary buildings / 29 zones** и четыре обычных district definitions. Все 644 обнаруженные source definitions покрыты: included 194, excluded specific 450, unresolved 0. Source job closure сохранён: 31 импортированный job (16 base + 15 additions); 59 capacity targets (29 covered / 30 typed gated / 0 missing), 15 flow resources (12 covered / 3 gated / 0 missing). Runtime компилирует только разрешённые job recipes; остальные имеют явную typed coverage omission, а установленная неизвестная capacity не допускается. Неизвестный job/flow gate зависит от definition и не останавливает три обычных пресета.

Новая гидропонная ферма реально оплачивается из authoritative stock после законного WorkforceLimit/month funding, строится, добавляет farmer capacity и меняет monthly food production / building energy upkeep. Проверены exact slot-zone identity, отсутствие дубликата и сохранение пользовательского zero foundry limit. Fixed paid price, live speed, daily work/no carry, completion before month, owner/observer/stale/idempotency/atomic bank+colonies остаются действующими. Source cost multiplier отделён от recurring ResourceMultiplier, поэтому zone_default не блокирует старт. Per-attached-district zone capacity и static zone slots/upkeep сохранены.

Расширенный source-каталог не объявлен полностью исполняемым: зависимые modifier/context/lifecycle операции адресно gated. В экономическом preset fixture доступны пять building recipes (foundry_1, hydroponics_farm, precinct_house, research_lab_1, stronghold) плюс city district; остальные entries остаются в каталоге с причинами/совместимостью/исходными термами. Это не утверждение, что все ordinary technologies открыты.

Legacy diagnostic construction без source runtime снова использует существующий fail-closed RejectUnresolvedConstruction; source runtime guards не ослаблены. Это восстановило существующие privacy/readiness/refusal/retry проверки, а не включило legacy строительство.

## Optional DTO — implemented / pending

Implemented добавочно к existing M3ConstructionCatalogView: `SourceTier` (только явное source tier, иначе null), `Prerequisites`, `UpgradesTo`, `UpgradesFrom`, `CompatibleSlotIds`, `SourceRecipe`, `CanAfford`, `UpgradeOperationGate`, `LocalizationKey`, `DescriptionKey`. Existing `SlotIds` — исполняемые свободные места, existing `Quote` — исполняемый quote; совместимость сохраняется для locked/occupied entries. CanAfford вычисляет authoritative bank отдельно от eligibility; отсутствие quote/bank даёт null, не false/0. SourceRecipe содержит Kind/TargetId/nullable Value/Expression/ConditionDisposition/ApplicationScope; никакие source path/line/hash не публикуются. Точное provenance и причины остаются в server content RuntimeGateEvidence / локальном evidence log.

Catalog LocalizationKey несёт только явный source NameKey; без него null и consumer использует полученный definition ID как fallback. DescriptionKey пока null. Нового localization reader здесь нет, локализованные строки/ID не переписаны. M3DevelopmentEconomyJobView получил `SourceSocialClassId` и `SocialClassImageDefinitionKey` отдельно от прежнего economic CategoryId.

Pending: installed localization/description keys и source-validated job key aliases, installed upgrade target quote/action binding/canEnqueue, отдельный district capacity/group DTO, portrait composition. UI wwwroot client/panel/CSS/HTML/widgets/layout tests не изменялись; root передаёт этот wire контракт UI и localization владельцам.

## Assets

C#/JS согласованы: profile `m3-static-452-v10`, importer 8, schema 1, material profile 4; prior v9 отклоняется strict compatibility. **291 image bindings, 263 уникальных DDS, 315 planet closure paths, 373 manifest files**; game source bytes 134 476 052, общий decoded profile 342 559 856 bytes; уникальные planet pixels **5 012 892 bytes < 8 MiB**. Security/memory limits сохранены. Manifest digest `f8ef26470d02f821c40033782edc5fd9b3d7e67f36129a6ff566b56433004f85` совпал C#/JS.

Mappings включают все included buildings/zones, 31 referenced jobs, поддержанные resource icons и четыре social classes. Shared upgrade textures и explicit icon overrides читаются из источника. `ruler/specialist/worker` подтверждены в common/pop_categories/00_social_classes.txt; `civilian` находится в 02_other_categories.txt и имеет GFX_civilian в planet_view.gfx. Nanites — проверенный local-resource:nanites texture convention с отсутствующей GFX registration, это не выдуманный GFX. Advanced_logic: GFX_crisis_icon_advanced_logic → crisis DDS, frames=2; только этот binding получает sprite-frame-selection-unresolved, whole-strip icon не выдаётся за выбранный кадр.

Генератор пишет только authored mappings/path closure; оригинальные/производные game bytes не попадают в Git. Node dependencies подключены разрешённым локальным junction к существующему store без установки/изменения store. Build/package содержит m3-ordinary-planet-assets.mjs, запуска HTTP не было.

Panorama остаётся отдельно pending: planet_view.gui:261–280 planet_portrait_window → GFX_portrait_planet; core.gfx:251–256 portraitType(type=planet), не обычный sprite; gfx/portraits/portraits/00_portraits_main.txt:96–150 задаёт sky/environment/city layering. Отдельный pc_continental_l01.dds не выдаётся за подтверждённую композицию/selection. Frame 48×48 не используется как panorama fallback.

## Upgrades — следующий отдельный owned этап

В baseline upgraded targets адресно gated `upgrade-lifecycle-unresolved`, включая запрет обхода через свободный slot. Tier metadata не считается real upgrade. Source graph/target costs/prerequisites сохранены; graph проверен на циклы и coverage каждого target. Root source researcher передал pointers: common/buildings/00_example.txt:172–173 upgrades edge, 04_manufacturing_buildings.txt:4–199 foundry tier2/3 can_build=no и tech_alloys_1/2, 00_capital_buildings.txt:134–170 target allow/prerequisites, planet_view.gui:7344 upgrade button, common/on_actions/00_on_actions.txt:2984–2994 completion hook. Эти scripts сами по себе не доказывают timing old effects/cancel/recheck.

Human prototype rules теперь подтверждены отдельно и сохранены root в **3a226bf:docs/DECISIONS.md**: old effects/jobs/upkeep до completion; atomic replacement того же slot/zone; cancel сохраняет старое и возвращает paid costs; edge + target tech/potential/allow при enqueue и pending; потеря условия отменяет/refund; can_build=no запрещает только from-scratch. Это HUMAN правила, не native proof. Они не включены в этот frozen baseline. По указанию root дальнейший upgrade diff начинается только после baseline review/приёма; автор baseline код больше не меняет до findings.

## Финальные команды и outcomes

Все команды выполнялись только в назначенном checkout; game root read-only. Logs находятся в ignored content-local/catalog-runtime/:

| Проверка | Команда / log | Outcome |
|---|---|---|
| Source catalog/semantic inventory/upgrade graph | dotnet run --project tests/Stellaris.PlanetEconomyChecks -- --game-root GAME_ROOT; planet-economy-checks.log | 8 PASS, 0 FAIL |
| Real construction/paid/refund/slots/availability | dotnet run --project tests/Stellaris.PlanetConstructionLiveChecks -- GAME_ROOT; live-checks.log | PASS; actual three presets + synthetic transaction fixtures clearly separated |
| Construction aggregate/lifecycle | dotnet run --project tests/Stellaris.ConstructionChecks -- --game-root GAME_ROOT; construction-checks.log | 53 PASS, 0 FAIL, 1 SKIP (symlink capability) |
| Session | dotnet run --project tests/Stellaris.M3SessionChecks -- --game-root GAME_ROOT; session-checks.log | exit 0; 15 legacy suites + source startup/economy/live construction/extended paid hydroponics PASS |
| Wire/runtime clock | dotnet run --project tests/Stellaris.M3ServerChecks; server-wire-checks.log | 310 PASS; real transport SKIP, отсутствует назначенный порт |
| Дополнительно разрешённый root source transport | dotnet run --project tests/Stellaris.M3ServerChecks -- --game-root GAME_ROOT; server-source-transport-checks.log | **FAIL**, exit -532462766: WebSocket reset при first.StateAsync(), Program.cs:458 после workforce funding второго игрока; server clean shutdown PASS. Нужен review/fix, интеграция baseline не разрешена |
| Neutral economy | dotnet run --project tests/Stellaris.Checks; economy-checks.log | 5 suites PASS |
| C#/JS asset parity/security/source closure | dotnet run --project tests/Stellaris.AssetChecks -- --game-root GAME_ROOT; asset-checks.log | 10 suites, 0 FAIL, 1 SKIP; Windows junction guards PASS |
| JS source assets/manifest/profile/source QA | STELLARIS_GAME_ROOT=GAME_ROOT node --test tools/asset-probe/{m3-planet-assets,m3-profile,m3-manifest,qa-visual-reference}.test.mjs; js-source-checks.log | 24 PASS, 0 FAIL, 0 SKIP |
| Existing client/panel/images/pipeline regression | node --test tools/asset-probe/m3-planet-images.test.mjs tools/asset-probe/m3-worker-pipeline.test.mjs tests/client/m3-client.test.mjs tools/asset-probe/m3-ui-planet-panel.test.mjs; client-regressions.log | 161 PASS, 0 FAIL, 1 real-source SKIP |
| Real worker pipeline separately | STELLARIS_GAME_ROOT=GAME_ROOT node --test tools/asset-probe/m3-worker-pipeline.test.mjs; worker-source-checks.log | 7 PASS, 0 FAIL, 0 SKIP; closes real-source case above |
| Server Release | dotnet build src/Stellaris.Server/Stellaris.Server.csproj -c Release; release-build.log | exit 0, 0 warnings, 0 errors |
| Whitespace | git diff --check HEAD | PASS |

Исправленные промежуточные провалы не выданы за PASS: cost ResourceMultiplier classification; joint long session достиг 4096 receipt guard, extended fixture вынесен в отдельную настоящую session без ослабления guard; source provenance gate leaks sanitised без ослабления assertion; source QA missing three решён permitted read-only junction, old 95-key fixture расширен exact authored set. Самостоятельный browser/live QA и real transport не выполнялись; user server 5192/39372/5852 не менялся. Stable/main и другие checkout не записывались.

Дополнение после code freeze: по новому прямому разрешению root source transport test запустил собственный ephemeral-port harness и завершил его clean shutdown. HTTP/manifest/CSP/socket-limit/source Workforce команды прошли до сбоя. В log перед reset есть ConnectionAbortedException и background InvalidDataException; точная причина ещё не доказана, snapshot/backpressure/heartbeat — гипотезы для reviewer, не утверждение. Frozen code/hash manifest не менялись. Эта новая проверка отменяет прежнюю формулировку «real transport не выполнялся» выше; ни 310 wire PASS, ни SessionChecks не заменяют её провал.

Read-only local probe после freeze: `content-local/catalog-runtime/transport-size-probe.log` измеряет raw server JSON и вызывает настоящий M3WireCodec для обоих игроков после каждого source Workforce шага: 894 609–894 960 bytes при bound 1 048 576, codec OK. Runtime jobs 24 из 31; семь source job recipes адресно gated. `transport-exception-trace.log` воспроизводит reset на Program.cs:458 через reflection entrypoint того же frozen server test assembly и AppDomain FirstChanceException, без production edits. Непосредственный abort: **ConnectionAbortedException: The connection or stream was aborted because a write operation was aborted with a CancellationToken** (20:33:10 UTC). SendAsync имеет deadline 3 секунды (M3Server.cs:348), publisher 200 мс; test KeepJoinedPeersAlive читает second/observer, а first во время второго funding loop не читается. Причина write cancellation подтверждена, backpressure из-за нечитанного first остаётся обоснованным объяснением для focused finding/fix. Frame overflow в измеренных состояниях не воспроизводится. Background InvalidDataException из первого run в trace повторно не возник; нельзя утверждать, что это oversized frame. Ранние trace InvalidDataException относятся к обязательным negative wire tests и intentional caught source gates; они не новая ошибка запуска.

Research/upgrade hook proposal (не реализован в baseline): authoritative country researched-set нужно передавать immutable snapshot в существующий M3ConstructionEvaluation.Context. Текущие owned стыки: M3ConstructionContent.Context (сейчас template.StartingTechnologies), M3GameSession.cs Project:310, Execute:584, AdvanceDay:704. Предлагается optional researchedTechnologies input в Context и Session helper ConstructionEvaluationFor(country candidate), вызываемый на quote/project/enqueue/pending recheck; research owner атомарно обновляет настоящий country set при monthly acquisition вместе с day revision. Adapter уже принимает evaluation во всех этих операциях. Никакого all-unlock, QA preset, обхода tech_alloys_1 draw conditions или переписывания трёх source presets. Source colony_shelter→capital может использовать starting tech_planetary_government и sapient_pop_amount≥1000, но текущий homeworld уже capital. Foundry→2 требует отсутствующий tech_alloys_1; реальное получение технологии принадлежит отдельному owner по новому решению пользователя, не этому frozen diff.

## Freeze evidence / actual ownership

### PLANET-CATALOG-TRANSPORT-FIX-002 — отдельный test-only follow-up

По новому назначению root и независимо подтверждённому finding изменён **только tests/Stellaris.M3ServerChecks/Program.cs** (+120/−24). Production baseline остаётся frozen: проверка всех hashes freeze-files.json дала drift 0. Новый diff не включён задним числом в исходный baseline patch/hash manifest.

Peer теперь запускает один постоянный reader сразу после welcome для каждого connected клиента. Входящий frame ограничен прежним M3WireCodec.MAX_OUTGOING_BYTES; хранится ровно один latest snapshot, не более 64 ожидаемых ACK по commandId и один pong. StateAsync ждёт следующий snapshot с minimumSequence, поэтому старый cache не удовлетворяет проверку свежего состояния. Send сериализован; await ACK/pong/state сохраняет deadline 5 секунд. Ошибки reader доставляются ожиданиям; unexpected/duplicate ACK/pong явно отклоняются. Dispose/Abort прекращают reader. Никаких production send/receive timeouts, auth, rate/memory limits или assertions не ослаблено.

Новые behavioral assertions одновременно отправляют accepted pause и invalid speed и проверяют отдельные commandId/результаты: чужой ACK не может удовлетворить другую команду. Все прежние wire assertions сохранены. Причина прежнего сбоя: test first peer не читал крупный snapshot stream при работе со second; непрерывный drain устранил write cancellation без увеличения лимитов.

Финальная команда: `dotnet run --project tests/Stellaris.M3ServerChecks -- --game-root GAME_ROOT` → **exit 0, PASS: 486, server clean shutdown PASS**, real HTTP/WebSocket/source world/month funding/observer/reconnect выполнены. Log `content-local/catalog-runtime/server-source-transport-fixed.log`, SHA-256 `aa60f462bc6fc920456b4e3583c2c85b45ce8537af386aa9180918bc81542531`. Это новый успешный run; исторический failing log сохранён отдельно и не переименован в PASS.

Финальный test file SHA-256 **7a92cf18507e37915dabf6af81ad143688913ebe73abf5a03a7047f2352b80d3**. Изолированный patch `content-local/catalog-runtime/transport-fix-002.patch`, SHA-256 **524f3ac54877418f6f7c48abd4295a4c7aa4db4ff4f1bf8a8718f2dbf1845b41**. Независимый reviewer root должен перечитать именно этот test diff; автор его не утверждает и не коммитит.

Read-only медицинский стык для последующего upgrade: medical_1 требует tech_basic_health и имеет conditional organic_pop_bonus_workforce_mult через has_purity_heightened_attributes_tradition (07_amenity_buildings.txt:807–815), сейчас это адресный gate, а не false. Medical_2 требует tech_frontier_health; source potential/destroy в 826–895, unconditional planet_doctors_upkeep_mult=−0.10 в 901, cost exotic_gases=@b2_rare_cost и условный recurring exotic_gases=@b2_rare_upkeep при num_zones(zone_payback_enlightenment)=0 в 962–985. Поэтому только unlock технологии не делает medical2 ready: будущий owned этап должен закрыть doctor upkeep modifier и настоящий strategic-resource funding/production. Эти source dependencies не исправлялись молча в frozen baseline. Альтернатива capital→major / tech_colonial_centralization передана root research owner; фальшивые researched flags/QA preset не добавлены.

RuntimeGateEvidence: 117 definitions (buildings 99, zones 9); полный ID→reason/source evidence в live-checks.log. Это dependency gates, не число всех unavailable entries: eligibility/upgrade/slot gates применяются отдельно.

Authored-code diff snapshot (без этого отчёта): content-local/catalog-runtime/baseline-freeze.patch, SHA-256 `a84c9ebe19487d038d24226e295c46e8adf2b94bb20972fa8c69f1d49612a14d`. File manifest: freeze-files.json, SHA-256 `316a472733b0d4d42ad5b1896f711ff32b0aea8e78ad40a349bb19aeb9e917b3`. Report исключён из собственного рекурсивного hash manifest. Все actual owned paths ниже; уже принятые каталожные изменения также перечислены, foreign changes в исходном clean checkout отсутствовали.

| Путь | SHA-256 | Bytes |
|---|---|---:|
| `docs/M3-WORK-OWNERSHIP.md` | `cf0c2dccc2ff00624797c0b60f32c7d60fb22d81874aad72994563b2fcc44383` | 16717 |
| `docs/PLANET-BASIC-ECONOMY.md` | `fb05787c5ccc6a37c1a2ab28d6b936d9cfd77d562bc0a5910b25e394f4e340fd` | 15290 |
| `docs/PLANET-ORDINARY-CATALOG-COVERAGE.md` | `3a2b5a338e9d84686a91919b10db7242017323ee457212c4837fc834663e3f95` | 180617 |
| `src/Stellaris.Core/Assets/M3AssetManifestBuilder.cs` | `5b8efa217187207f9b960b129d77fd5456ce362c03ff4a067c49028d9713d52f` | 12731 |
| `src/Stellaris.Core/Assets/PlanetAssetProfile.cs` | `16c5128b3924a3f70f0f58feae3d77551e3ab01de3c41c2b91342412893c4e95` | 67615 |
| `src/Stellaris.Core/Construction/ConstructionLiveContent.cs` | `c4a12bcaed4bbe25798467921237cf67931d95d0c18fe49986a8b51e3fcd27c8` | 7703 |
| `src/Stellaris.Core/Content/Construction/ConstructionDefinitionImporter.cs` | `c17339fd5ea3320f08b75756c82fb8522be906c1e9560644ba7702d8bce6adcd` | 48723 |
| `src/Stellaris.Core/Content/PlanetEconomy/PlanetEconomyImporter.cs` | `c13feaf4214000743d4cb053d386cbde7acc82c005f20fb440953f37f03f699b` | 81536 |
| `src/Stellaris.Core/Content/PlanetEconomy/PlanetEconomyTypes.cs` | `ba335374650c9634b4880f079162379b75512c18ea74fdd85a1fc91467ffa86c` | 10960 |
| `src/Stellaris.Server/Stellaris.Server.csproj` | `9098c1cc8dd51210a1e5650735a60c55a3fa7a2dec80c0c2ddbc8bb3d5dd44ab` | 2897 |
| `src/Stellaris.Session/M3/Construction/M3ConstructionAdapter.cs` | `c631ae35771df953a503830d34f4ba2b977bacda843daadeb58c66612d269999` | 16075 |
| `src/Stellaris.Session/M3/Construction/M3ConstructionContent.cs` | `3df3f0f7193ddb1f03f7cd73d09ec1a9cbf635696530e0db0a0278ea413eab09` | 14727 |
| `src/Stellaris.Session/M3/M3BaselineEconomyProfile.cs` | `981eb6e425386cc3ea482ba774569673aca1823833f5e747ad6bdf87bf573bc4` | 33922 |
| `src/Stellaris.Session/M3/M3DevelopmentProjection.cs` | `deb2785d314f13cc8f699b6536840f48d393843ae86c63c87505a11c3f24b37c` | 9313 |
| `src/Stellaris.Session/M3/M3DevelopmentProtocol.cs` | `ea88ed191bd39a43e450b5985926d873580da5cd0ac82d533f63d92e50477915` | 5474 |
| `src/Stellaris.Session/M3/M3GameSession.cs` | `7063e54287ed5eafffab3ad11bf8316790cb48650910625b65c4a863447c1354` | 59483 |
| `tests/Stellaris.AssetChecks/Program.cs` | `c8a8532fa560e2492c69d6b9fcba87b02666321428dfb5c33cb8329bd22351e0` | 28938 |
| `tests/Stellaris.ConstructionChecks/AssessmentChecks.cs` | `276916074b27a6ff8c4d677cac9bd9bfe1d5df3154791ecca51f1d67a822f239` | 38311 |
| `tests/Stellaris.M3SessionChecks/Program.cs` | `b93270cc799b3900377009c20266db856a9f6db6406132714c63f100ad2063e5` | 87121 |
| `tests/Stellaris.PlanetConstructionLiveChecks/Program.cs` | `71ea93dbf55e1ab6b4bdd5068ce2aab493554efa84da8cf000bb885c0a477a69` | 29270 |
| `tests/Stellaris.PlanetEconomyChecks/Program.cs` | `0ba5af34ccec9f855aa874331974ac3b1ca63e118e3137e9e3a4741ac05ef89a` | 48541 |
| `tools/asset-probe/generate-ordinary-planet-assets.mjs` | `f9d7fed17f3605c7c455c97621c69c4f5abc44841bf41b975f8c928bad98391f` | 8357 |
| `tools/asset-probe/m3-manifest.test.mjs` | `a12a8ebc17b952e76460eb7d6e8361cfc0fa3dd892349699c44b09143a053887` | 5532 |
| `tools/asset-probe/m3-ordinary-planet-assets.mjs` | `3005bdb914d1731b136a2326f978491297edd6477853245d44fd55654f82b27c` | 125793 |
| `tools/asset-probe/m3-planet-assets.mjs` | `3d9cd8c1bb656dc1ea49ba4f8269fc571b85d6ca9d40f4bf7480140afbd0eab3` | 15942 |
| `tools/asset-probe/m3-planet-assets.test.mjs` | `be8351991810e3b1e4ed4a9fb43aef6ea2f5009dbd2f022015319ba7c4abb470` | 16820 |
| `tools/asset-probe/m3-profile.mjs` | `3c6c5c1214ffc2dd9a81136790beed9f293e557690433d8f41d553bd0b8f08fd` | 30372 |
| `tools/asset-probe/m3-profile.test.mjs` | `7a561be8a874999f0287d9c2e664499042ef15b870ff236179e5412f690b29a2` | 34022 |
| `tools/asset-probe/qa-visual-reference.test.mjs` | `2840373b71a3d8892ca0a13cf0341489642e23257fe4e277c37f7f4d130a7504` | 20643 |
