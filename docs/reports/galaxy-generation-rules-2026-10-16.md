# Генерация галактики 4.5.2: планеты и специальный контент

16 октября 2026. Исследование по запросу пользователя; изменения runtime не входят в задачу.
Это конспект доказательств, а не новый контракт реализации или согласование отклонений.

## Источники и границы

Основной источник — пакет `planet-generation-reverse-452/outputs` от 13 октября:
`reproduction-package-index-452.md` устанавливает приоритет поздних дополнений над ранними отчётами.
Ниже **A** означает `native-generation-reverse-addendum-452.md`, **D** —
`initializer-descriptor-defaults-handoff-452.md`, **R** — `initializer-range-parser-handoff-452.md`.
Номера разделов относятся к этим документам. Игровые пути указаны относительно установки.
Оригинальные игровые файлы и дизассемблирование в репозиторий не копируются.

Проверено: launcher сообщает Cygnus v4.5.2 (9776); SHA-256 текущего executable совпал с исследованным:
`400DF27C82DDC845AA9DCE79BD468D81F18F060D299CD263E3AFA93AEF4F7A83`.
SHA-256 прочитанного A: `2AD39F747DEE73C7128DC1D9FCA2D77A64CBDD70295DF4E048ED179142D4C5D3`,
совпадает с индексом пакета. Совпадение executable не доказывает неизменность всех scripts;
приведённые ниже значения дополнительно сверены с текущими локальными файлами.
Нативные алгоритмы здесь проверены по результатам статического исследования; игра не запускалась.

## Обитаемые планеты

Основа — A §15.4, dispatcher `0x140506C10`, selector `0x140D383D0`.
Обычный initializer задаёт слоты и классы тел; отсутствующий `class` означает `random`.
При случайном выборе классов учитываются тип запроса, допустимый интервал орбиты,
класс звезды, свойства класса и положительный вес. Классы обходятся в порядке базы.

Для допустимого класса сначала вычисляется
`w = trunc0(classSpawnOdds × starClassFactor / 100000)` в fixed-point.
Если класс colonizable, затем `w = trunc0(w × habitableMultiplier / 100000)`.
Отсутствующий star-class override означает множитель 1, нулевой исключает класс.
Один local RNG draw выбирает по сумме весов; при пустом/нулевом общем пуле обычный random
возвращает sentinel без draw. Invalid/none класс не создаёт видимое тело, но расходует
предписанные count/distance draws и меняет орбитальный accumulator.

Следствия:

- Habitable Worlds меняет относительные веса, а не задаёт квоту планет или простой процент.
- Удвоение настройки не гарантирует удвоения числа миров: меняется нормированное распределение.
- Explicit `pc_*`, контекстный `ideal_planet_class` и гарантированные соседи идут своими ветвями;
  множитель случайного пула не является универсальным запретом их создания.
- Некоторые специальные системы также масштабируют `usage_odds` по `habitable_worlds_scale`:
  inline scripts `solar_system_initializers/initializer_modifiers_habitable_world_systems*.txt`;
  например Trappist использует дополнительный множитель 0.4.

Текущие стандартные параметры (`common/scripted_variables/00_scripted_variables.txt:179–186`):
обычные обитаемые классы имеют base weight 0.3 и интервал расстояния 60–100;
volcanic использует отдельный вес 0.2. Это не общая таблица всех colonizable классов.
Для `sc_b` стандартные обитаемые классы получают factor 0.4, то есть вес 0.12 до lobby factor
(`common/star_classes/00_star_classes.txt:23–39`). Для `sc_m` factor 0.3.

Гарантированные миры создаются через `neighbor_system` и специальные initializers,
с проверками настроек/происхождения. Это отдельный путь от обычных случайных классов.
Для generic empire chain default настройки — 2, диапазон 0–2
(`common/defines/00_defines.txt:477–479`). При значении ≥1 запрашивается
`neighbor_t1_first_colony` с `ideal_design_class` и размером 12–18; при ≥2 добавляется
ветвь `neighbor_t1 → neighbor_t2_second_colony` с ideal world размера 14–24.
Каждый поиск этой цепочки ограничен 1–3 гиперпрыжками от своего parent;
оба мира не обязаны напрямую соседствовать со столицей. Источники:
`common/solar_system_initializers/empire_initializers.txt:8,133–142,787–825,860–872,943–989`.
Специальные origins могут задавать другие цепочки.
Поиск рассматривает подходящие ещё не инициализированные системы от ближайшей;
mandatory fallback может создать новую систему и reciprocal hyperlane.
По A §15.2 fallback делает до 15 попыток и при полном провале принимает последнюю координату
с диагностикой. Neighbor chance имеет особую проверку `draw % 100 <= spawn_chance`;
её нельзя подменять обычным strict `<` gate.

## Размеры, число тел и орбиты

Основа — A §15.3, D и R; executor `0x140870DC0`.

Приоритет размера: explicit descriptor `size`, иначе `moon_size` класса для луны,
иначе `planet_size` класса. Integer range выбирается одним draw:
`base + sign(span) × (draw % (abs(span)+1))`; обе границы включены.
Даже фиксированные `count` и `size` расходуют draw. Нет единого размера для всех классов.

| Пример | Обычная планета | Луна |
|---|---:|---:|
| Стандартные обитаемые классы | 12–25 | 10–15 |
| Gas giant, default класса | 20–30 | 8–15 |

Источники: `common/planet_classes/00_planet_classes.txt:233–265,468–495`,
`common/scripted_variables/00_scripted_variables.txt:179–185`.
Индивидуальные home/special initializers и effects могут переопределять размеры.
Например обычный `random_empire_init_01` явно задаёт homeworld 18–21
(`common/solar_system_initializers/empire_initializers.txt:14–15,76–88`).

Количество тел задаёт initializer: например `basic_init_01` создаёт 2–10 обычных
планетных slots и по 0–1 moon slots на каждую, плюс звезду. Это число slots;
неудачный random class может уменьшить число созданных тел.
Источник: `common/solar_system_initializers/misc_system_initializers.txt:3–35`.

Нативный порядок одного descriptor: count → class → orbit distance → size → allocation/attach
→ orbit angle → post-create content → вложенные тела. Class resolver получает текущий
орбитальный контекст до добавления distance этого instance. Порядок нельзя выводить
из визуальной записи полей в script. `orbit_distance` прибавляется к accumulator;
`change_orbit` — невидимый descriptor `class=none`. Вложенные planet/moon/change_orbit
сохраняют единый порядок; отдельные orbital lines исполняются после них.
Fixed-point орбитальные ranges используют другой, half-open sampler; integer правило
для размеров к ним неприменимо.

## Туманности

Основа — A §18.5: ранний native call `0x14098DD2F → 0x140977780`,
после появления систем, до позднего наполнения галактики.
Scenario задаёт число, радиус и минимальное расстояние центров.
Например `map/setup_scenarios/medium.txt:52–54`: 6 туманностей, радиус 60, separation 200.

Для каждой туманности local RNG выбирает центр среди существующих систем в generation order.
Центр отклоняется, если расстояние до предыдущего центра строго меньше minimum.
После принятия global RNG выбирает имя без повторения (swap-with-last).
Системы присоединяются, если ещё не принадлежат туманности и distance² строго меньше radius²;
граница круга исключена. При перекрытии уже присоединённые системы остаются за первой туманностью.
После 10000 подряд неудачных кандидатов расходуется draw для 10001-го,
но фаза завершается до его принятия. Поэтому запрошенное число не безусловная гарантия.

## Уникальные системы и соседи

Основа — A §12.2, §15.1–15.2. Уникальность обычно выражена `max_instances`,
а не отдельным генератором «unique systems». Используются usage pools, scripted weights,
spawn gate, DLC/контекстные условия, explicit calls и цепочки соседей.
Отсутствие `usage` означает отсутствие в общем случайном пуле.

Порядок обычного отбора: исключить исчерпанные finite definitions → выбрать по `usage_odds`
→ отдельный spawn draw → исполнить либо учесть неудачную попытку и повторить выбор.
Отрицательные веса исключены, нулевые остаются; при нулевой общей сумме native использует
равномерный fallback среди eligible entries. Это особенность, которую нельзя трактовать
как общее разрешение всех скриптовых блокировок: нужно учитывать полный admission context.
Неудачный spawn roll тоже расходует `max_instances`; лимит 1 не гарантирует появление.

Если `scaled_spawn_chance > 0`, `base = roundAway(scaled × totalSystems / 100)`,
иначе `base = spawn_chance`. Далее `chance = roundAway(base × settingFactor)`;
при отсутствии scaled setting factor=1. Roll успешен при `draw % 100 < chance`.
Clamp evaluator не делает: chance≥100 всегда проходит, chance≤0 никогда.
Finite `max_instances` также масштабируется settingFactor с округлением от нуля.

Например Trappist: `max_instances=1`, `scaled_spawn_chance=10`;
для 600 систем gate равен 60%, но это условный gate после выбора кандидата,
а не доказанная итоговая вероятность Trappist на карте.
Источник: `common/solar_system_initializers/special_system_initializers.txt:1039–1052`.
Parent batch полностью исполняет object/system effects до прохода поиска neighbors.
Explicit neighbor execution не проходит обычный admission по max_instances.

## Врата, червоточины, ресурсы и прочее

- **Заброшенные врата:** `abandoned_gateways_01` в
  `common/solar_system_initializers/apocalypse_initializers.txt:4`;
  `max_instances=15`, `scaled_spawn_chance=5`, setting `num_gateways`.
  Scripted weights ограничивают близость, effect создаёт `gateway_ruined`.
  Это generic initializer, отдельного native gateway-tail нет (A §18.5).
- **Червоточины:** post-generator `events/game_start.txt:1922`, `game_start.31`.
  Читает `num_wormhole_pairs` с `scale_by=5`, выбирает пару без существующих wormholes,
  starting systems и empire clusters, требует distance≥500; только после успешного
  выбора второй системы создаёт оба конца и связывает их. Это число попыток цикла,
  фактическое число пар зависит от допустимых кандидатов.
- **Залежи и блокеры:** A §4, §15.5–15.6. Для стандартного colonizable мира размера S:
  `5 + trunc(0.2×S) + randomInclusive(0, 2 + trunc(0.2×S))` первичных попыток.
  Размер 20 даёт 9–15 попыток, затем идут district min/max repair и blocker/unblocked repair;
  это не обещание 9–15 окончательных фич. Источник constants:
  `common/defines/00_defines.txt:680–688`. Selector учитывает potential, scripted weights,
  повтор категории и текущее состояние мира. Resource Abundance меняет вес null-deposit
  на применимых необитаемых классах, поэтому не является общим множителем числа залежей.
- **Модификаторы, кольца, аномалии:** A §6. Random modifiers выбираются внутри factory;
  затем descriptor может удалить/добавить modifiers, подавить random deposits,
  преобразовать blockers, назначить anomaly и ring, flags/name/entity, выполнить planet effect.
  Explicit anomaly в initializer и появление аномалий при дальнейшем обследовании — разные пути;
  полный алгоритм обследования этим исследованием не подтверждён.
- **Resource regions:** A §18.4, `0x14097CAC0`, назначаются по рёбрам Triangle с private
  shuffled slots и scripted potential. **Precursors:** `0x14097D540` — отдельное назначение
  цивилизаций; из найденного адреса не следует доказанное полное распределение/все event chains.
- **Fallen/advanced empires, origins, hostile/DLC systems:** отдельные стадии и script branches;
  каталог и разбор их definitions не доказывают исполнение всей механики.

## Что восстановлено и что реализовано

Ранние `native-generation-algorithm-452.md` и `galaxy-and-planet-generation-native-reverse-452.md`
содержат уже исправленные гипотезы: поздний общий tail для туманностей/врат/червоточин,
неизвестные class tables, ошибочный сокращённый RNG order. Приоритет имеют A, D и R.

Сохранённый producer handoff `stellaris-galaxy-generation/outputs/galaxy-integration-handoff.md`
и `galaxy-runtime-coverage.json` от 13 октября явно сообщают: 360/360 initializers
структурно компилируются, runtime pool — 11 ordinary и 6 generic homes;
special/neighbor/random content/nebula/gateway/wormhole execution deferred или unsupported.
Это характеристика того артефакта, не аудит актуального runtime основной ветки.

Для детерминированной реализации нужно сохранять порядок загрузки/обхода, отдельные
local/global RNG streams, диапазоны и последовательность эффектов. Одного vanilla seed
недостаточно для побитового совпадения без global/script RNG prehistory и native окружения.
Следующий шаг реализации должен опираться на coverage gates, а не считать parse=execute.

Проверки этой задачи: чтение пакета и его индекса, проверка executable/addendum hashes,
сверка выбранных scripts и параметров, независимое исследование планет/спецконтента
и ревью конспекта. Сборка, runtime/gameplay и статистические прогоны генерации не выполнялись:
изменена только документация.
