# Строительство после M3 — независимая сверка

12 октября 2026. Read-only исследование локальной 4.5.2, не контракт реализации. Установка не изменялась; игра не запускалась. База кода — `907e9fa`; контент прочитан через Git из `afe3458`, прежнее предложение/аудит — из `c4af677`. Чужие ветки и памятки не объединялись.

## Изменение основания

S17B, S18–S20A и S23 приняты в контентной ветке; исправленный S22 также принят. Поэтому прежнее утверждение предложения `c4af677`, что S18 ещё не выбран, устарело. Повторно утверждать whitelist первой партии не требуется. Остаётся выбрать границу ближайшей поставки внутри него, не выдавая её за весь M4. Номера D45–D54 контентной ветки не идентичны одноимённым номерам нашей DECISIONS; ссылки ниже привязаны к commit/path/S-ID.

Три принятых обычных шаблона используют Functional Architecture; «нейтральная колония без civic/state effects» не заменяет их незаметно. Полный S18 содержит также районы, zones и Voidfarers: наличие hydroponics в списке не разрешает считать эти системы реализованными.

## Доказательная матрица

Пути относительно установки. Строки относятся к проверенному снимку; определения/комментарии/локализация не равнозначны проверке native runtime.

| Узел | Подтверждено текстом | Что не доказано / что нужно |
|---|---|---|
| Hydroponics | `common/buildings/03_resource_buildings.txt:5–67`: set hydroponics, prerequisite tech_hydroponics, base_buildtime b1_time, category planet_buildings, energy upkeep 2, jobs inline и nomadic cost switch | Конечная цена/длительность, момент оплаты, состояние нашего fixture |
| Обычная ветка | scripted variables b1_time=360/b1_minerals=400; inline jobs/farmers_add и building_static_jobs=200 | Capacity 200 не создаёт 200 pops/Workforce. Нормализация и распределение должны быть отдельным проверяемым адаптером |
| Potential | Нужен owner; wilderness/pc_machine/resort исключены. Условие «менее двух» имеет OR с is_ai=no | Это не лимит двух ферм игроку. Не подставлять true вместо активных условий |
| Placement | `common/zones/00_zones.txt:1098–1110`, inline `zones/shared_food_zone.txt`: included hydroponics и zone_building_slots_add=3, свои potential/unlock/resources | Не доказано, что zone_food уже есть на стартовой планете. show_in_tech не заменяет unlock. Число 3 не задаёт резервирование места очередью |
| Общий предел | `common/defines/00_defines.txt:1638`: DEFAULT_MAX_PLANET_BUILDINGS_PER_ZONE=3 | Порядок сочетания с добавками/занятыми/заказанными местами нужен runtime-эталон; нельзя молча сложить 3+3 |
| Civic | `common/governments/civics/00_civics.txt:1527–1529`: structures cost −0.15, building build speed +0.15 | Не доказывает формулу speed, округление дня или динамическое обновление уже начатого заказа |
| Категории | `common/economic_categories/00_common_categories.txt:867–878`: planet_buildings → planet_structures; генерируются cost/upkeep modifiers | Наследование категорий видно, итоговый native-порядок additive/multiplicative и clamps не измерен |
| Отмена | `localisation/english/main_1_l_english.yml:1290–1291`: подсказка отмены сообщает полный возврат | Сильное основание для проверки, но не доказательство всех стадий, округления, storage cap, смены owner/условий. Не переносить ship-refit refund .75/.90 на здания |
| Списание | `common/buildings/00_example.txt:191–198`: hooks queued/unqueued имеют local_spent_<resource>; `common/on_actions/00_on_actions.txt:2970–2983` содержит queue/unqueued hooks | Наличие spent-переменных не доказывает момент debit и порядок возврата |
| Очереди | `common/planet_classes/00_planet_classes.txt:60–62`: single_build_queue default no, объединяет colony/carrier/starbase; `06_planet_classes_nomads.txt:107`: pc_ark yes | Не доказывает параллельность зданий обычной планеты, лимит и порядок. Arkship нельзя приравнять к обычной колонии |
| Фазы | В GUI есть очередь, progress и cancel controls | GUI не задаёт порядок daily completion/monthly economy, revalidation и перенос остатка дня |

Дополнительная сверка placement: hydroponics не ограничена `zone_food`. `zone_default` имеет max_buildings=6, `zone_urban` — 3; обе используют include=all и исключают farming, но не hydroponics (`common/zones/00_zones.txt:5–118`). Документация `common/zones/99_HOW_TO_ZONE.txt` явно определяет max_buildings как число мест, included/excluded sets как допуск/запрет, show_in_tech как подпись карточки, не unlock. `slot_food` (`common/zone_slots/00_zone_slots.txt:101–132`) допускает `has_tech_or_functional_civic`; определение trigger (`common/scripted_triggers/00_scripted_triggers.txt:5595–5607`) разрешает Functional Architecture вместо tech_food_processing_1. Это не создаёт зону и не доказывает её присутствие на старте.

Дополнительные текстовые свидетельства очереди: PLANET_BUILDQUEUE_UP/DOWN (`localisation/english/megacorp_l_english.yml:15–16`) ссылаются на перестановку очереди; MOD_PLANET_BUILDING_CAPACITY_ADD (`modifiers_1_l_english.yml:484`) называется Build Queue Capacity, но не доказывает, означает ли capacity длину либо параллельность. Тексты смены зоны (`main_3_l_english.yml:4455–4466`) запрещают некоторые конфликты со строительством и различают совместимые/несовместимые здания. Они требуют runtime-проверки актуального использования: локализация также содержит legacy tile/spaceport ключи.

## Контрольные SHA-256

Это контроль исследованного поднабора, не полный import manifest. Перед реализацией нужно замыкание активных переменных/inline/triggers/startup/tech/modifiers и проверка стабильности источника.

| Путь | SHA-256 |
|---|---|
| common/buildings/03_resource_buildings.txt | 1D0AB107CEDB5BE2C0DE9BFADD9A2B44490BCDEE7DC2AF184DF1580DC02592DC |
| common/defines/00_defines.txt | 82EEBD807EB54AF347025B061AF2991FC40A1B1059E39C2CB8EE135DCA02B7CC |
| common/planet_classes/00_planet_classes.txt | 24C5387795E22B3FA6EA5BC3F83B63B61D4990B77EC2DDB8E3E00483156672F5 |
| common/inline_scripts/zones/shared_food_zone.txt | 176FFC400ABF4D659185F95ED93EB9413F4646D587ECF1C30E3F2A8EDFA520AC |
| common/economic_categories/00_common_categories.txt | B3DA494D1889BF79A361840F4E9705995E8CD2BBEE09D65629D7E182CF46F01B |
| common/governments/civics/00_civics.txt | 3B7869AFCF0A9C3FBDF3DA062CD8009D551645487A0F0C36462498D6926269C7 |

## Реальный startup обычных шаблонов

`events/game_start.txt:1726,1777–1779`: game_start.12 вызывает generate_start_buildings_and_districts, generate_start_pops и generate_home_planet_final_pass. Construction effect (`common/scripted_effects/01_start_of_game_effects.txt:1593–1692`) применяет initialization/authority/species/ethics/origin/civics, размещает rural districts и запускает validate_planet_buildings_and_districts.

`common/inline_scripts/game_start/authority_regular.txt:23–43`: building_capital, два district_city, zone_research_unity/zone_industrial, factory. Authoritarian добавляет precinct house; Materialist — research lab, Militarist — stronghold (соответствующие ethic inline scripts). Government slot по `zone_slots/00_zone_slots.txt:5–22` начинает как zone_default. Значит hydroponics можно исследовать в уже предусмотренной government zone, но число **свободных** мест после engine validation ещё не измерено.

Initializer задаёт rural запрос 2 energy/2 mining/3 farming; origin_default добавляет energy+1 и для food species farming+1. Rural placement ограничивает запрос доступными modifiers/deposits. Итог 3/2/4 нельзя записывать как неизменный fixture. Старт прямо создаёт capital, не colony_shelter; shelter и capital имеют разные tiers/jobs/housing/amenities и не взаимозаменяемы. Полный startup требует эффектов factory/lab/precinct/stronghold; тест одной фермы его не воспроизводит.

Документация `common/economic_categories/99_README_ECONOMIC_CATEGORIES.txt:15,30–32` прямо подтверждает применение parent/child `_mult`, но запрещает наследование `_add`; арифметику сочетания `_mult` и округления этим не устанавливает. Nomadic cost switcher использует from?; raw40 motes при ratio=.1 — отдельная условная ветка, не обычная цена.

## Минимальный пакет уточнений и маршрут

### Последующие прямые ответы пользователя

Проверены через read_thread документаторского чата `01a12d6b-8e8f-7723-a719-e75c3b77eb14`, turn `01a12f66-5930-7661-a772-4314db8a5f57`: стоимость списывается при заказе, место резервируется сразу, отмена разрешена в любой момент строительства. При смене владельца заказ отменяется с возвратом оригинальному владельцу; уничтожение просто отменяет, утрата условий отменяет с возвратом. Это основание контракта **проекта**, не независимо доказанный native-алгоритм. Формула возврата/округление/cap, базовая capacity и точный дневной порядок этими ответами не установлены. Ответ о двух вариантах очереди требует учёта capacity modifiers, не разрешает выбрать один произвольно.

Справочный чат завершил независимую сверку; итог прочитан через read_thread (turn `01a12f68-75d4-7fb1-ad5c-bf8b50c35e08`). Native-пробелы подтверждены. Найдены дополнительно `allow` и `abort_trigger/abort_construction_trigger` в документации buildings/00_example.txt:60–69: последние явно отменяют стройку, но не определяют refund. Hydroponics имеет destroy_trigger, не специальные abort triggers; нельзя вывести из него универсальную revalidation. `tr_prosperity_sct` в common/traditions/00_prosperity.txt:66–69 добавляет cost −.10/speed +.25, но не раскрывает формулу сочетания. Пять конкретных сценариев очереди/возврата/скорости/месячной границы/revalidation переданы в отложенную ручную приёмку, не требуют немедленного ответа пользователя.

По последующему прямому решению пользователя механические неизвестные сначала направлены в справочный чат `01a12f66-8bb2-7ee3-aced-76f5d06d2338`; если первоисточник их не раскрывает, сценарии передаются в отложенную ручную приёмку. Немедленных ответов/тестов пользователя не требуется. Продуктовые границы ниже пока предложения, не запрос изменить принятый whitelist.

1. Ближайшая поставка: узкий цикл hydroponics на реальной совместимой зоне обычной колонии или весь уже принятый S18 с районами/zones? Предпочтение узкому циклу — предложение о порядке, не урезание первой партии.
2. Для оставшихся native-неизвестных: отложенный эталонный тест 4.5.2 (base capacity/параллельность, формула возврата/округление/cap, civic price/time и live modifiers, месячная граница). Оплата при заказе/резерв места/допустимость отмены уже определены прямым ответом выше, не спрашиваются повторно.
3. Превращаем ли следующую поставку сразу в полноценную колонию с принятыми housing/amenities/happiness/stability или разрешаем отдельный явно лабораторный строительный тест до неё? Откладывание реальных эффектов требует явного ограниченного согласия; setJobs не должен перезаписывать building capacity.
4. Подключать сохранение/восстановление уже в этой поставке или отдельно продлить исключение «перезапуск теряет мир»? Для статики: сохранить замороженную очередь или отменять; при отмене нужна отдельная политика возврата. Эти решения не следуют из обычного AFK.

Обязательные технические пункты будущего контракта: авторитетные цены/места/IDs, atomic enqueue/cancel/completion, commandId/revision, приватная очередь/observer, daily/month boundary, unsupported diagnostics и регрессии M1–M3. Это инженерные требования, не разрешение реализовать неизвестную механику.

Независимая подготовка импорта: [ограниченное замыкание hydroponics](hydroponics-import-dependencies-452.md), 22 проверенных SHA-256, явные условные ветви и gaps текущего NeutralEconomyImporter. Не является полным замыканием startup/всех эффектов farmer или контрактом исполнения очереди. Capacity-аудит дополнительно направлен справочному чату с точной цитатой пользователя «есть оба варианта, смотри эффекты добавляющие build queue capacity»; до его результата не объявлять все capacity-правила исключительно native.

### Итог конкретного capacity-аудита

Справочный turn `01a12f6c-fdfe-7552-bc39-05a7937ae64f` проверил 2234 txt в common/events. Найдены шесть записей planet_building_capacity_add: origin_wilderness +5 (`common/governments/civics/00_origins.txt:651–653`), arkship_engineering_bay/beastport по +1 (`common/starbase_modules/00_arkship_utility_modules.txt:52–54,121–123`), arkship_capital_shipyard +4 (`00_arkship_spinal_modules.txt:86–89`), irassian yards +2 либо +3 по archaeoengineers (`common/starbase_buildings/00_arkship_colony_buildings.txt:1182–1189,1198–1203`). У выбранных regular templates нет этих источников; Voidfarers bootstrap engineering bay даёт +1, что соответствует initialize_arkship_starbase_effect (`common/scripted_effects/nomads_effects.txt:3405–3418`).

Отдельный starbase_shipyard_capacity_add документация прямо описывает как parallel ship build slots; starbase_building_capacity_add — места зданий станции, не параллельность (`common/starbase_modules/00_example.txt:379,440–441`). single_build_queue — объединение, не численный executor count. Пользователь подтвердил оба режима; remaining gap теперь узкий: база обычной колонии, формула/выбор одновременно продвигаемых заказов и реакция на уменьшение capacity. Скриптовые источники добавок **найдены**, их нельзя обозначать отсутствующими или целиком native. Дополнительный отложенный сценарий: по три заказа regular/Arkship T1, сравнить суточный прогресс и capacity; не расширять whitelist ради динамического эксперимента.
