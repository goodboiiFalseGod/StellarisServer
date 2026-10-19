# M3 базовый планетарный runtime

Контракт PLANET-BASIC-RUNTIME-001, 13 октября 2026. Цель — заменить только приёмке планет M3 лабораторные 600 pop/три job и запустить три обычных пресета через принятый источник `a61172b` и startup-граф `a7e1185`. UI и construction lifecycle не входят. Расширенный catalog и обратный реверс не являются зависимостями.

## Контекст и запуск

Server bootstrap обязателен импортирует baseline `PlanetEconomyCatalog` и startup source profile из локальной Stellaris 4.5.2. Любая отсутствующая/неверная версия, required definition/variable/script, неоднозначное значение или неподдержанное активное условие останавливает запуск с явной ошибкой; fallback на нейтральную лабораторию запрещён. Старые конструкторы M3 остаются только для явных legacy/test сценариев.

Для трёх пресетов строится стартовая колония из выбранного immutable startup plan. Job capacity — сумма raw source additions для фактических instances district/zone/building; уровни и multiplicity применяются один раз. Согласованный профиль D=100 из [контракта Workforce](15-workforce-and-job-upkeep.md) переводит raw Workforce в число job units делением на 100; дробные единицы сохраняются. Начальные pop и запасы импортируются по `generate_start_pops`, `origin_default`, `give_starting_resources_effect` и достигнутым defines/inline/scripted triggers. Три пресета — обычные organic `origin_default`: начальная популяция 2800 из regular branch плюс source group 400 origin; food и consumer goods выбираются проверенными `country_uses_*_game_start` триггерами. Пользовательское правило: 1 organic pop даёт 1 базовую обычную Workforce; trait bonus добавляется отдельно. Population upkeep, species rights и полная модель занятости не реализуются.

`PlanetEconomyCatalog` задаёт jobs, production/upkeep terms, typed stock policy и genuine IDs. Планировщик суммирует job capacity и активные структурные расходы раздельно по source instances: `JobUpkeep`, `BuildingUpkeep`, `DistrictUpkeep`, `ZoneUpkeep`, общий `Upkeep`, `Produced`, `Net` и stock. Никакого двойного вычета. Неразрешённые активные multiplier/modifier выражения или нужные traits не приравниваются к нулю: соответствующий source profile не запускается с dependency/coverage error.

Отдельные leader/governor-dependent additions `soldier` из `on_all_capital_buildings` явно исключены из этого bounded профиля и публикуются owner как typed coverage omission с source provenance. Базовые soldier jobs из установленного stronghold и остальные source capacities сохраняются; это ограничение не закрывает колонию и не задаёт нулевой native modifier.

Отсутствующая definition — неизвестное состояние и ошибка. Единственное bounded исключение — `zone_default`: startup importer сохраняет immutable proof только после разбора reached 4.5.2 definition и `nomadic_cost_switcher` inline closure, структурной проверки, что там нет recurring production/upkeep/job terms, и связывания обоих hashes с планом. Тогда именно recurring economy равна source-proven zero; installed zone остаётся в графе/DTO. Отсутствие proof, изменение source shape или новый recurring term закрывает Start.

Категории storage явные: source-capped с source maximum или `UnlimitedReserve` без числового псевдолимита. Baseline job output, upkeep и структура применяются к stock один раз за успешный месяц; science reserves копятся отдельно без research sink, unity также без cap, trade ограничен source cap. Минусовой stock — отказ месяца без частичных изменений; source-capped stock ограничивается только после суммирования чистого потока. Недостающий, отрицательный либо неподдержанный flow — явная ошибка, не ноль.

Рабочее распределение остаётся отмеченным прототипным профилем, а не утверждением о native job-priority. Каждый owner/job имеет `capacity`, `limit`, `assigned`, `bonus`, `effective`; `assigned ≤ limit ≤ capacity`, остаток Workforce — unemployment. Месячный отчёт обновляется только после успешного commit и до следующего успеха сохраняет прошлое значение.

## Команды и изоляция

`adjustWorkforceLimit` принимает только owner `ColonyId`, `Job`, ожидаемую `Revision` и направление `Count ∈ {-1,+1}`; один шаг — 5% текущей принятой maximum capacity, clamp `[0,max]`. `setWorkforceLimitMin` и `setWorkforceLimitMax` задают крайние значения. `setWorkforceLimit` принимает raw-WF абсолютный limit. Неизвестный job/колония, отрицательная/избыточная цель, нецелое направление, неподдержанный preset, observer, draft, чужой owner и stale revision отклоняются без изменения мира. Повторный CommandId с тем же телом идемпотентен; с другим телом — conflict.

Start заранее собирает source plan, stocks, populations, capacities, upkeep и economy candidate для обеих колоний; ошибка не оставляет частичный running world. Month готовит все отчёты и все счета до commit; deficit/overflow/source dependency оставляет предыдущие счета, отчёты, дату и tick согласованными. DTO приватен для владельца и выбранной perspective; general observer не получает колониальную экономику, observer-команды не меняют state, неподтверждённая draft не считается применённой. Нет full-population employment AI, research UI, lifecycle стройки или клиентских изменений.

## Проверки

### Интеграция live construction — PLANET-LIVE-INTEGRATION-001

Владелец общих hooks с 13 октября 2026 — `/root/planet_live_integration`; root координирует, не пишет runtime. Область интеграции: `Session/M3/{M3GameSession,M3BaselineEconomyProfile,M3DevelopmentProjection,M3DevelopmentProtocol}`, `Server/M3Server`, `tests/M3SessionChecks/Program.cs` и непосредственно затронутые проверки сервера; при необходимости новый Session/M3 helper. Core Construction, исходные importers/catalog, UI, assets и расширенный catalog принадлежат другим этапам и здесь не меняются.

При успешном Start каждая фактическая source colony получает immutable construction runtime из startup graph и contribution map baseline economy; optional `from` получает отдельную доверенную привязку к authenticated acting country и известному preset `IsNomadic`. Неизвестный native caller остаётся за пределами прототипа и не получает fallback. Конструкция, cancel/refund и дневное продвижение строятся как runtime/bank candidates; экономика и все колонии fork-ятся под общей session lock, стартовые contributions устанавливаются из текущего графа ровно один раз, а публикация проходит целиком только после успешной проверки всех кандидатов и месячного settlement. Завершение дня применяется до settlement. Economy и construction используют одну colony revision; повтор/конфликт, stale, чужой owner, observer, неготовые assets и недостаток/ошибка средств не мутируют состояние.

После каждого изменения графа source economy capacities и building/district/zone upkeep пересчитываются из текущих установленных definitions и levels. Zone per-level terms умножаются только на текущие attached district levels; static zone costs/slots считаются один раз на установленную зону. Startup buildings остаются каноническими occurrences с точной привязкой к runtime slot; скрытые features не становятся активными. При изменении slots безопасно сохраняются или отклоняются затронутые orders/occurrences/receipts; неизвестное reconciliation закрывает зависимую цель. Нулевые Workforce limits сохраняются, стартовые максимумы следуют обновлённой capacity. Последний успешный месячный flow report держится до следующего успешного settlement.

Acceptance: реальные три ordinary presets; exact slots/quotes и paid/reserved enqueue; unchanged rejection cases; cancel refund с capped/unlimited storage; genuine day-314 completion и live economy refresh; city 3→4 с zone-scaled jobs/free capacity/static cost; нулевой Workforce limit; monthly/day boundary и cross-colony/month atomicity; sanitized owner/observer wire; сохранение legacy 15 suites. Обязательные команды для этой поставки перечислены в handoff; расширенный catalog, reverse/galaxy и full VM audit не являются зависимостями.

- Реальная установленная 4.5.2: три поддержанных source графа дают импортированные populations, stocks, job capacities и структуру; capacities учитывают повторные districts/zones/buildings ровно один раз.
- Реальные `foundry`, `artisan`, research jobs и source upkeep видны в месячном typed report; ресурсы не получают ложное нулевое значение при неизвестном активном терме.
- Unity и три science reserves накапливаются без cap, capped stock соблюдает source max; производство, расходы, net и balances согласованы.
- Workforce команды проверяют 5% шага, min/max, clamp, zero/max/overcapacity, stale/repeated/out-of-scope/observer и отсутствие мутаций при отказе.
- Start/month atomicity, report timing, privacy и старые M3 legacy regressions проходят. Release build и M3SessionChecks запускаются по командам root.

## Зафиксированные источники для импортёра

`common/scripted_effects/01_start_of_game_effects.txt` (`generate_start_pops`, regular cap 2800, `origin_default` +400 и `give_starting_resources_effect`); `common/inline_scripts/game_start/origin_default.txt`; `common/scripted_triggers/00_scripted_triggers_ai.txt` (`country_uses_food_game_start`, `country_uses_consumer_goods_game_start`); `common/defines/00_defines.txt` (`POP_BULK_UNIT_SIZE=100`). Это первоисточники, значения runtime должны поступать из bounded importer и иметь provenance. Установка игры read-only.
