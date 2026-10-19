# PLANET-MENU-LIVE-001 · attempt1

Основа: `5a67a51`, ветка `feature/planet-menu-runtime`. Только browser UI; backend, оригинальная установка и assets read-only. Без коммитов и вложенной делегации. Перед интеграцией требуется независимое ревью фактического diff.

## Контракт до реализации

Существующий wire DTO: `development.colonies[]` содержит `startupDistricts {instanceId,definitionId,level}`, `startupZones {instanceId,districtInstanceId,slotInstanceId,zoneId,slotNumber}`, `startupBuildings {instanceId,definitionId,slotInstanceId,zoneId,order,imageDefinitionKey,compatibleZoneInstanceIds}`, `startupFeatures {instanceId,definitionId,isBlocker,isHidden,hiddenByBlockerInstanceId,hiddenFeatureInstanceId,order}`, `startupDistrictCaps {modifierId,value}`. Отдельно существуют `slots/catalog/installed/queue/features/jobs`, `planetSize/initialPlanetSize`. Null/отсутствие не равны нулю. `planetSize` не выдаётся за global capacity; raw modifiers показываются отдельно.

Стартовый граф — фактические установленные объекты, даже когда `installed` пуст. Зоны связываются по districtInstanceId; здания — по точному slotInstanceId и compatibleZoneInstanceIds. Не размножать одно здание по всем совместимым зонам. Если runtime installed уже покрывает тот же slot/definition, стартовую копию не показывать повторно. Общая вместимость и type caps не выводятся из количества картинок. Не генерировать пустые места: только полученные slots. Неподдержанный kind остаётся доступен для чтения с объяснением отсутствия операции.

Особенности объединяются по instanceId с приоритетом runtime. `isHidden` исключает активную карточку; связь blocker→deposit показывается только по полученному ID. Никаких искусственных clearance slot IDs.

Изображения: только полученный imageDefinitionKey либо реально существующий ключ импортированного profile, без путей DDS по предположению. Resource keys — `resource.<id>`. Неизвестное определение подписывается своим ID. Сводка закрытого месяца сохраняет Produced/JobUpkeep/BuildingUpkeep/Upkeep/Net; stock отдельно и только если получен.

Уточнение владельца экономики до привязок: `development.colonies[].economy = {profileId,allocationPolicy:"prototype-definition-order",population,baseWorkforce,reportMonth,jobs:[{jobId,categoryId,imageDefinitionKey,capacity,limit,assigned,bonusWorkforce,effectiveWorkforce}],resources:[{resourceId,storagePolicy:"source-capped"|"unlimited-reserve",sourceMaximum?,stock,produced?,jobUpkeep?,buildingUpkeep?,districtUpkeep?,zoneUpkeep?,net?}]}`. Flows nullable до первого успешного месяца. Все WF raw, без масштаба 1/100.

Подтверждённые команды camelCase: `adjustWorkforceLimit {colonyId,expectedColonyRevision,job,count:-1|1}`, `setWorkforceLimitMin/Max {colonyId,expectedColonyRevision,job}`, `setWorkforceLimit {colonyId,expectedColonyRevision,job,count:rawWF}`. CommandId добавляет существующий transport. Revision общая для экономики и строительства. Presentation callback получает действия adjust/min/max/absolute; процент вычисляет сервер. Ctrl не угадывает WF-единицу места. Legacy `setJobs` остаётся только при отсутствии нового economy DTO.

Стройка использует существующие проверенные quote/slot/revision/envelope consumers без optimistic mutation. Сохраняются owner/observer, freshness, pending, dirty draft/focus и навигация. При смене revision/POV/removal старые обработчики не действуют.

## Приёмка и границы

DOM fixtures: ordinary районы/уровни, зоны/здания без дублей, hidden deposits, отсутствие выдуманных slots/caps; ресурсные ключи; raw WF callback ±1/Shift/absolute и Ctrl refusal. Existing client tests: stale/observer/pending/draft/focus и точные construction IDs. Syntax и focused navigation regressions. Browser 16:9 с локальными assets — после сборки проверенного общего backend, без установки пакетов. До этого DOM проверки не считаются визуальной или live simulation приёмкой.

## Передача на независимое ревью

Повтор после независимого review `stellaris-planet-prototype-qa/outputs/planet-menu-live-review.md`: обнаруженный P2 исправлен узко в `submitPlanetJobs`. Detached legacy callback повторно сверяет отсутствие live `development.economy`, revision и contentProfileId. Смена revision больше не требует общего сброса interactionId, поэтому focused raw WF draft при месячном обновлении сохраняется. Добавлены наблюдаемые regression cases legacy→live при той же колонии и изменённые legacy revision/profile: старый callback не отправляет команду, новый legacy callback в актуальном состоянии работает. Требуется повторное независимое review этого исправления; автор не утверждает self-approval.

Изменены `m3-planet-panel.mjs`, `m3-client.mjs`, `m3.css`, `tests/client/m3-client.test.mjs`; добавлен `tests/client/m3-planet-runtime.test.mjs` и этот контракт. `m3.html` не требовал изменения: доступные heading/map/close/tabs остаются существующими.

Проверки: `node --test tests/client/m3-client.test.mjs tests/client/m3-planet-runtime.test.mjs tools/asset-probe/m3-ui-planet-panel.test.mjs tools/asset-probe/m3-ui-contract.test.mjs` — после P2 исправления 160/160 PASS, 0 SKIP; syntax production modules и `git diff --check`. Дополнительный `qa-visual-dom.test.mjs` увеличивает счёт на один и не входил в reviewer invocation: этим объясняются прежние 158 авторских против 157 reviewer на предыдущем diff. Повторное narrow review ещё не выполнено, коммитов нет.

Оставшиеся интеграционные зависимости: реальная публикация подтверждённого economy DTO/команд; authoritative global/type/free district capacity (сейчас planetSize и raw startupDistrictCaps не подменяют её); runtime building/zone slots с точными placement IDs/quote. Существующий constructionOperations сохраняет глобальные unresolved gates; UI не снимает неизвестный gate локальной догадкой. После server closure операции становятся доступны на полученных exact targets. При последующей унификации startup/runtime нужен received instanceId либо exact slot/definition для подавления дублей; совпадение только definitionId недостаточно.

Ссылки на просмотренные оригинальные изображения — REFERENCE-ARCHIVE: инфраструктура `c9de8b75`, зоны `e316916c`, профессии `c5f10397`. Изображения не копировались. Уже установленный Three доступен через локальный junction к `tools/asset-probe/node_modules`; пакеты не устанавливались и junction не изменялся.

Дополнительная браузерная проверка 13 октября: временный локальный synthetic fixture из production `m3-planet-panel.mjs` и `m3.css` на 1280×720 и 390×844. DOM измерения: `document.scrollWidth` равен ширине viewport (1280/390); dialog, Close и все три вкладки внутри экрана. На узком экране длинная инфраструктура прокручивается, вкладки видны. Профессии с raw WF контролами также открываются. Fixture без оригинальных ассетов, поэтому это проверка компоновки и hit area, а не live импорт/симуляция; временные tab и сервер закрыты. Общую live browser QA с проверенным backend проводит ведущий после интеграции.
