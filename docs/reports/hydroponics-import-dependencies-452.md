# Hydroponics 4.5.2 — ограниченное замыкание импорта

12 октября 2026. Исследование перед отдельным контрактом, **не реализация и не runtime-приёмка**. Установка читалась без изменений; игра не запускалась. Основание: [сверка строительства](construction-next-slice-452.md), контентный whitelist `afe3458:docs/28-technology-and-assets-selection.md` (S18 принят). Ниже рассматривается обычная управляемая игроком, не кочевая империя и совместимость с government `zone_default`; это граница исследования, **не назначенное начальное состояние колонии**. Полный S18, startup, все эффекты farmer/civic и строительная очередь этим не замыкаются.

Пути — относительно локальной установки; строки и SHA-256 относятся к исследованной 4.5.2. `launcher-settings.json` сообщает rawVersion `v4.5.2`, но не доказывает runtime checksum или отсутствие модов. В репозиторий оригиналы не копировались.

## Корни, символы и границы

| Узел | Источник и связь | Ограничение |
|---|---|---|
| Здание | `common/buildings/03_resource_buildings.txt:5–67`, `building_hydroponics_farm` | Верхний `category=resource` — не economic category; `resources.category=planet_buildings`. Отдельных `allow`, `planet_limit`, `upgrades`, abort triggers в этом определении нет; это не отменяет общие правила |
| Числа здания | `common/scripted_variables/00_scripted_variables.txt:305–306`: `b1_time=360`, `b1_minerals=400`; `100_scripted_variables_zones.txt:32,38`: `building_static_jobs=200`, `static_jobs_ai_weight_coefficient=.1` | 360 — raw base_buildtime, 400 — raw cost, 200 — raw capacity; не конечное время/цена/число занятых. AI coefficient не исполняется для выбранного игрока. Global variables могут переопределяться локально — комментарий `00_scripted_variables.txt:1–3` |
| Job inline | Здание `:44–47` → `common/inline_scripts/jobs/farmers_add.txt:2–14`, подстановка `AMOUNT=200` → `job_farmer_add` | Regular/non-spiritualist-FE/non-gestalt ветка. FE `:17–25` и gestalt `:28–38` сохраняются как отдельные условные ветви, не превращаются в farmer |
| Символ farmer | `common/pop_jobs/03_worker_jobs.txt:452`, ресурсы `:515–556`, economic category `planet_farmers` | Это ссылка capacity на профессию, не полное замыкание её swaps, eligibility, conditional production, councilor/overlord effects. Текущая численная проекция farmer остаётся отдельно ограниченной M1 |
| Cost inline | Здание `:51–58` → `common/inline_scripts/buildings/nomadic_cost_switcher.txt:1–13`: regular minerals/nomadic volatile_motes, `COST=400` | Условие именно `from?.is_nomadic`, не owner. Наличие/значение from должно быть явным; отсутствие scope нельзя самовольно заменить regular=true |
| Nomadic параметр | `common/scripted_variables/10_scripted_variables_nomads.txt:514`: ratio=.1 | Raw 40 motes только при активной nomadic-ветке, до остальных modifiers. Эта ветка вне рассматриваемого regular исполнения, но её нельзя молча потерять |
| Upkeep | Здание `:59–61`: energy=2 | Самостоятельный BuildingUpkeep, не JobUpkeep; занятость farmer не отменяет его |
| Prerequisite | Здание `:62–64` → `common/technology/00_soc_tech.txt:538–547`, `tech_hydroponics`: society/biology, tier0, cost0, start_tech=yes, non-wilderness potential | Метаданные технологии не создают researched-tech state у нашего owner. Research/AI weight остальной технологии не входит в eligibility-проекцию и не считается выполненным |
| Категории | `common/economic_categories/00_common_categories.txt:6–14,856–885`: `planet_buildings → planet_structures → planets → country` | Buildings и structures генерируют cost/upkeep `_mult`. README `99_README_ECONOMIC_CATEGORIES.txt:15,30–32`: parent+child `_mult` применяются, `_add` детям не наследуются. SUM/PRODUCT, clamps и округления текстом не установлены |
| Принятый civic | `common/governments/civics/00_civics.txt:1501–1533`, `civic_functional_architecture`: potential non-gestalt/non-corporate; `:1528–1529` structures cost −.15, building build speed +.15 | Нужен реальный active/valid civic state. Personality/random/AI weight и shroud attunement этого civic не входят в выбранную строительную численную проекцию; их нельзя обозначать полностью поддержанными |
| Government slot | `common/districts/00_urban_districts.txt:4–15`, `district_city` → `common/zone_slots/00_zone_slots.txt:5–22`, `slot_city_government.start=zone_default`, potential/unlock always=yes | Включённый set слота буквально `zone_default`, а zone объявляет set `default`; не исправлять строки по догадке. `start` — отдельное авторазмещение, не доказательство доступности ручной перестройки по sets |
| Zone default | `common/zones/00_zones.txt:5–55`: potential пустой, unlock не col_resort; max_buildings=6; include=all; excluded sets без hydroponics | Farm принадлежит hydroponics, не farming, поэтому farming-exclude не запрещает её. Это совместимость определения, не шесть свободных мест; нужны существующие объекты/занятость и общие ограничения |
| Метаданные zone | `common/zones/00_zones.txt:7,17–25` → `100_scripted_variables_zones.txt:6–7`: zone_buildtime=360, zone_cost=1000; тот же cost inline, nomadic multiplier=.5 | Строительство/смена zone здесь не реализуется; эти поля не являются ценой/временем hydroponics |
| Свидетельство startup | `common/inline_scripts/game_start/authority_regular.txt:11–43`: regular non-nomadic branch добавляет capital, district_city, research_unity/industrial и factory | Это **не полный startup closure**, не перенос двух districts/свободных мест в fixture. Остальные startup зависимости/engine validation разобраны в связанном отчёте |
| Документация places | `common/zones/99_HOW_TO_ZONE.txt:13–17,51–57,102–110`; `common/zone_slots/99_HOW_TO_ZONE.txt:4–27`; `common/defines/00_defines.txt:1638` | Max_buildings означает число мест, default cap=3; district-based unlock/modifier cap/реальная занятость/резерв требуют отдельного численного контракта. Не считать default cap + modifier автоматически |

## Условия, которые нельзя заменить константой

`potential` здания (`03_resource_buildings.txt:13–29`) требует owner, non-wilderness, не pc_machine, без `resort_colony`; OR country_uses_food/is_ai=no и OR ignore_ai_building_limitations/is_ai=no/num_buildings<2. Для доказанного `is_ai=no` OR не означает лимит двух зданий игроку. `destroy_trigger` (`:31–42`) отдельно проверяет AI без food либо wilderness; его нельзя автоматически превратить в abort/revalidation всей стройки.

Раскрываемые scripted triggers:

- `common/scripted_triggers/00_scripted_triggers.txt:1714–1724`: `is_regular_empire` — перечисление country types плюс отсутствие gestalt ethic; `:1753–1755`: `is_gestalt` по ethic; `:1766–1769`: `is_wilderness_empire` по origin. `:292–298` и `:1666–1668`: spiritualist FE → country type + fanatic-spiritualist ethic. Это проверка owner state, не импорт всех origins/authorities.
- `common/scripted_triggers/00_scripted_triggers_ai.txt:23–50`: `country_uses_food` использует реальные resource_expenses_compare и bio-ships; `common/scripted_triggers/07_scripted_triggers_ships.txt:33–36`: `country_uses_bio_ships` → exists this / uses_ship_category bio_ship. Текст различает ещё не инициализированную экономику; нельзя угадывать её native-данные по нашему балансу food.
- Сравнения `is_ai`, `is_nomadic`, `is_country_type`, `has_ethic`, `has_origin`, `has_modifier`, `has_designation`, `has_carrier_flag`, `num_buildings`, `exists`, scope owner/from и optional `?` нуждаются в явном state/семантике. ID `pc_machine`, `resort_colony`, `col_resort` здесь используются как сравниваемые символы, не как импорт их полных механик.

Food zone и Functional Architecture обход food-slot технологии описаны в связанном отчёте. Они не нужны для доказательства совместимости **уже существующей** zone_default; не добавляются как обязательное место по умолчанию. Полный набор активных cost/speed/upkeep modifiers остаётся внешней зависимостью colony/owner state, не исчерпывается одним civic.

## Граница собственного импортера

Технический gap проекта, не факт vanilla: `src/Stellaris.Core/Content/NeutralEconomyImporter.cs:8,16` содержит `BuildingDefinition` только Id/Upkeep/Modifiers и whitelist из трёх booster buildings. `:106` прямо исключает строительные cost/eligibility/buildtime/inline; `:154–158` пишет excluded Coverage для resources category/cost/inline, `:181–183` — для остальных полей здания. Добавление hydroponics ID в массив не даст construction, capacity, prerequisites или placement и не должно выдаваться за поддержку.

До кода нужен отдельный контракт проекции/state, условных ветвей, unsupported diagnostics и capacity-адаптера; регрессии M1/M2/M3 сохраняются. Файл не назначает queue/payment/fixture defaults. Оплата при заказе, резерв сразу и допустимость отмены уже определены последующими прямыми ответами пользователя в связанном отчёте; повторно неизвестными не считаются. Отложены **арифметика возврата/clamps/округления**, сочетание cost/speed modifiers, live-обновление заказа, реальное свободное место и native daily/month порядок. Их отсутствие не разрешает подставить догадку.

## SHA-256 исследованных файлов

Это ограниченный список исходников/свидетельств, не утверждённый runtime manifest. Hash целого файла не означает исполнение всех его определений. При будущей реализации потребуется проверить overrides/load order и источник до/после чтения, отдельно замкнуть фактически активные ветви.

| Путь | SHA-256 |
|---|---|
| launcher-settings.json | `3477F427C2CA93C8EAE4787A315C67EFF2C741BE318D2EC1C07CCC43C236D559` |
| common/buildings/03_resource_buildings.txt | `1D0AB107CEDB5BE2C0DE9BFADD9A2B44490BCDEE7DC2AF184DF1580DC02592DC` |
| common/scripted_variables/00_scripted_variables.txt | `D920A5A9C98C3C0926CD3852D8B4869E8492A9B211BB48E5CC023B2D416421BE` |
| common/scripted_variables/100_scripted_variables_zones.txt | `F67B8C2D1D53C5B392AE60D0B89D069B581E59DEE52F8102A2A47C430B216155` |
| common/inline_scripts/jobs/farmers_add.txt | `207ED2B695ECF3F31930D66562866357C760901664BC9E8D9C8BE6AFF0D90A13` |
| common/pop_jobs/03_worker_jobs.txt | `A86F096D6AC18DD5EE3A6267D31551C933816300E5CCD279F5E7B6AFB5FE18DF` |
| common/inline_scripts/buildings/nomadic_cost_switcher.txt | `2D63DA2B2AB40279A14F201EF09D87A5ABC76172C452B9523781E25D3FBA3346` |
| common/scripted_variables/10_scripted_variables_nomads.txt | `3C7E83F11AC6016AAC72BBDDBD4B58489D692691BEE6E2447DF9BA0D20FBE748` |
| common/scripted_triggers/00_scripted_triggers.txt | `57B23A9C3875C28B935020DD5C13BD49ED173C424AFE2DA6AFD3BDC0A1757273` |
| common/scripted_triggers/00_scripted_triggers_ai.txt | `97D99A16CF49E360B6829D61516979BD6D0555654DF61B513BAC4B452E8BC103` |
| common/scripted_triggers/07_scripted_triggers_ships.txt | `1EC342D3BBD2BF3E5BEBC91922B0811686D8FEA0D8EB3C79A8771B231720DE69` |
| common/technology/00_soc_tech.txt | `4BFB400930C457280157B5A2900C74F0D43D44F4DABE18825208A63552BBB21C` |
| common/economic_categories/00_common_categories.txt | `B3DA494D1889BF79A361840F4E9705995E8CD2BBEE09D65629D7E182CF46F01B` |
| common/economic_categories/99_README_ECONOMIC_CATEGORIES.txt | `C66CEB1F9CE278941255FB9C410E5127CB8C5915F81935F31A6C6EFB7DB8192B` |
| common/governments/civics/00_civics.txt | `3B7869AFCF0A9C3FBDF3DA062CD8009D551645487A0F0C36462498D6926269C7` |
| common/zones/00_zones.txt | `CE48F6AEA3B5A5A30DABE15B46B5A32AC9149389B3D412D7F0CCCF7F1562DCAA` |
| common/zones/99_HOW_TO_ZONE.txt | `CBAF30A97B0386254FA738871122E235F7DA48F7D2EA9969E46F2D706219A94D` |
| common/zone_slots/00_zone_slots.txt | `0E134E8D4843D4E0CECB835287156480E6C654625726A96E001ED9C54447D15B` |
| common/zone_slots/99_HOW_TO_ZONE.txt | `D8AA20EFE859C61E4DB178A8DAAC374E5448D0B55550F607755558E1A4B4C395` |
| common/districts/00_urban_districts.txt | `4153E3912143DCFA7E6349F7559D4DD71187B4DD0BB4E20C7E298638D6A51737` |
| common/defines/00_defines.txt | `82EEBD807EB54AF347025B061AF2991FC40A1B1059E39C2CB8EE135DCA02B7CC` |
| common/inline_scripts/game_start/authority_regular.txt | `C63771E3EE73FB3585E921297805C3F8503B6170EE62B92E18F0D6BF2567A0A6` |
