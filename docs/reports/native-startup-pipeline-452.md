# Script-driven startup districts and buildings — Stellaris 4.5.2

Статус: source reconstruction завершён в bounded scope. Текущий кодовый owner `/root/script_startup_runtime`; root — независимое review/integration. По последнему прямому указанию пользователя приоритет — фактические стартовые здания/районы ordinary, hive, machine и source-defined civic/ethic swaps. Fixed handwritten final layout запрещён. Solar initializer homeworld class/size — отдельный отложенный atom; новый подходящий day0 save полезен как проверка, но не gate для script-derived ветвей.

## Подтверждённая цепочка

`common/on_actions/00_on_actions.txt:4055–4063` вызывает `game_start.12` в colony scope. `events/game_start.txt:1724–1779` для обычного planet-carrier вызывает по порядку `generate_start_deposits_and_blockers` → `generate_start_buildings_and_districts` → `generate_start_pops` → `generate_home_planet_final_pass`. Другие carrier/origin paths отдельны.

`common/scripted_effects/01_start_of_game_effects.txt:1593–1693` упорядочивает `rural_districts_initializer`, authority regular/hive/machine (и nomad-варианты), species, ethics, origin hooks, civics, tankbound civic, planet-class setup, `rural_districts_placement`, стартовые ресурсы и validation. Отключённую ветвь нельзя случайно исполнять либо молча отбрасывать: она определяется её source triggers.

Для обычного профиля `common/inline_scripts/game_start/authority_regular.txt:1–43` задаёт capital, city districts, research/unity zone slot 1, industrial zone slot 2 и factory в industrial zone. `authority_hive.txt` и `authority_machine.txt` задают соответствующие authority branches/swaps; их эффекты и civic/ethic additions нужно брать из script order/условий, а не превращать в предзаданные списки.

`rural_districts_initializer.txt:6–35` задаёт energy/mining/farming counts `2/2/3`. `origin_default.txt:12–57` меняет energy на 3 и при `country_uses_food_game_start=yes` farming на 4, иначе mining на 3. Это requested amounts, не итог: `rural_districts_placement.txt:25–296` маршрутизирует district type, проверяет modifier caps и вызывает helper. Этические/civic/species/origin effects идут до placement и могут менять mutable state.

## Helper capacity: source contract и точное неизвестное

`common/scripted_effects/01_start_of_game_effects.txt:4624–4649` (`add_district_and_planet_size_if_needed_effect_amount`) повторяет `$amount$` раз и на каждой итерации проверяет **текущее** состояние:

- если `free_district_slots >= 1`, добавить требуемый district с `ignore_cap=yes`;
- иначе, если `free_district_slots = 0` и `planet_size < 25`, увеличить текущий size на 1 и добавить district с `ignore_cap=yes`;
- иначе add не происходит.

Следовательно, helper получает mutable current planet size, актуальные установленые district groups/levels, выбранный class/district set и capacity evaluation на каждую операцию. Source-депозиты/features могут менять district-cap modifiers и должны быть учтены до расчёта. Для continental `origin_default` выше известны исходные counts, но дальнейшие authority/species/ethic/origin/civic additions и active cap effects обязаны применяться в source order.

Пользователь напрямую уточнил capacity semantics: у планеты размер `X` даёт вместимость `X` районов; применимые общие capacity modifiers увеличивают/уменьшают её; `free_district_slots = (planet_size + applicable capacity modifiers) − сумма занятых district levels`. Это человеческое уточнение контракта, не вывод из скриптов. Сохранять отдельно type-specific eligibility/max/add/mult limits: они определяют допустимые/uncapped типы и shared pools, но не заменяют общую вместимость. `common/districts/00_DOCUMENTATION.txt:5–10` документирует `default_starting_district`, `is_uncapped`, `shared_capacity_modifier`; rural placement (`:25–296`) читает type-specific `district_generator/mining/farming_max_add`. Не выдумывать другие имена/формулы модификаторов: учитывать только реально импортированные applicable capacity modifiers. После каждого district add и/или `planet_size++` пересчитать общую вместимость, occupied levels и остаток; затем выполнить следующую итерацию helper.

Typed input для каждого вызова helper: class + district set, mutable size, occurrences grouped by district definition and levels, active deposits/features + evaluated district cap modifiers, selected authority/species/origin/civic/ethic flags, requested district type and amount, capacity result immediately before add. Recompute capacity as state changes; не вычислять один раз для всего цикла.

## Occurrence graph / сохранения

Accepted server representation: definition type → grouped district occurrence with levels → nested zone slots → zone → building instances; own occurrence IDs допустимы по D06. Source selectors указывают definition type и slot, не native instance ID. `common/districts/00_urban_districts.txt:4–15`, `common/zone_slots/00_zone_slots.txt:5–50`, `common/zone_slots/99_HOW_TO_ZONE.txt:3–8` определяют city slots и `slot_city_government` с `start=zone_default`. Capital definitions используют government building set; `zone_default` включает building sets; значит после materialization графа разрешён source-constrained **unique** placement в единственный совместимый government zone. Если target ambiguous, ошибка, не choose-first. Подтверждённый nested save graph не устанавливает исходный level count.

Save review: day0 `2200.01.01.sav` (`SHA-256 ad3f3ecc…`) не содержит eligible ordinary `origin_default + continental`: player country 4 nomadic; country7 `origin_default`, но nomadic. Month6 country0 `origin_necrophage`: capital→planet8 (`pc_continental`, size18)→colony0→district refs `[0,1,2,3]`; district0 `district_city`, level3, zones `[0,1,2]`; zone0 `zone_default`, buildings `[0,3,4]`; building0 `building_capital`, position0. Это доказывает nested association/final graph, не startup initial level/count. Queue refs — незавершённые targets, не installed instances. Подходящий native day0 save — дополнительная проверка; не блокирует script implementation.

## Deferred initializer note

Полный `usage=empire_init` pool содержит шесть `random_empire_init_01..06` (`common/solar_system_initializers/empire_initializers.txt:27,153,287,431,535,676`), все с `ideal_planet_class` и size bounds 18–21 (`:7–15` и homeworld blocks). `usage_odds` отсутствует у всех; README не даёт числового default. Этот отдельный class/size selection atom отложен и не является prerequisite текущего source startup-building task. Старое RNG/initializer заключение не означает fixed initializer или fixed layout.
