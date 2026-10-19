# Гарантированные пригодные миры: GUARANTEED-01

Статус: реализован, focused проверки и независимое actual-diff review пройдены.
Результаты — [общий отчёт](reports/galaxy-content-validation-2026-10-16.md#гарантированные-миры-guaranteed-01).

База `dceb67f`, отдельная ветка генерации. Прямое поручение пользователя разрешает
доделать генерацию последовательными игровыми срезами. Этот срез добавляет
source neighbor chains к generic homes; strategic AI, special encounters и
заселение новых планет сюда не входят. Browser тесты исключены по позднему указанию.

## Источники и поведение

`common/solar_system_initializers/empire_initializers.txt`: generic homes01..06,
`@jumps=3`, четыре определения neighbor_t1/neighbor_t1_first_colony/neighbor_t2/
neighbor_t2_second_colony. Reverse addendum §15.2 и parser-gap handoff §6 задают
batch порядок, inclusive spawn gate и ближайший existing eligible system.
Требование exact seed/native allocator не является gate: GALAXY-GENERATION.md.

- Host выбирает `GuaranteedColonies=0/1/2`, default0 сохраняет прежний M3 запрос.
  Поле optional в команде/settings; старый caller получает0. Другие игроки видят
  подтверждённое значение. Host-only/lobby-only, ready/revision/invalidation,
  candidate fingerprint/ACK/start работают как для других galaxy settings.
  Отрицательные, дробные и значения>2 отклоняются атомарно.
- Core разрешает только эту ранее отключённую family вместе с туманностями.
  При0 нет neighbor draws/allocations; прежние порядок выбора initializers,
  Content/Names draws, geometry и body values сохраняются. Source spatial flags
  home effects применяются и при0 без новых draws: прежде они были Deferred,
  поэтому metadata flags/fingerprint могут измениться даже при0.
  При1 home→t1_first_colony→t2. При2 дополнительно home→t1→t2_second_colony.
  Итого2/4 neighbor systems на игрока, пригодных миров1/2. Не путать число систем
  с числом миров; второй мир может быть до6 hops от столицы.
- При ненулевой настройке сначала инициализировать все homes текущим batch,
  применить поддержанные spatial effects всего batch, затем найти neighbors
  и выполнить их следующим batch. Ordinary fill — после опустошения очереди.
  Каждый child наследует ideal class и participant context своей root home.
  Не выбирать explicit neighbor через ordinary weights/max counts.
- Читать AST/ranges/variables из source, не подменять definitions фиксированными
  layout. Узкий compiler охватывает транзитивное generic-home дерево: initializer,
  hyperlane_jumps, trigger num_guaranteed_colonies и spawn_chance/default100.
  Неизвестное активное поле/оператор/initializer даёт typed failure с source.
- Каждый descriptor тратит local Content draw на `draw%100 <= spawn_chance`,
  даже при100. Для каждого candidate с trigger тратится отдельный seed draw.
  Candidates в nearest Euclidean squared order, ties по site ordinal; distance
  считать без floating approximation, hops BFS по финальным hyperlanes включительно.
  `PristineNeighborAvailabilityV1`: existing, uninitialized, не reserved home,
  не already scheduled. Это bounded policy pristine world, не универсальный
  native runtime predicate. Existing match не добавляет гиперлинию.
- Для этого source дерева нет mandatory fallback. Если активную гарантированную
  цепочку разместить нельзя, вернуть `neighbors/neighbor-underfill` с root/parent/
  initializer до публикации. Не молча выдавать меньше миров и не добавлять систему.

## Результат и границы

- Применить source spatial flags `every_neighbor_system`/`set_star_flag` для
  empire_cluster (включая вложенный обход), body `set_planet_flag=prescripted_ideal`
  и `prevent_anomaly=yes` в этих neighbor definitions. Хранить фактический результат
  в immutable metadata; не выдавать Deferred за исполнение. Не исполнять повторно
  существующие home startup economy effects и не снимать иные deferred gates.
- Добавить immutable allocation/provenance: root participant/home, parent system,
  child system/initializer, hops, guaranteed body при наличии. Проверить unique
  allocation, число активных source-веток, ideal class и source size ranges12..18/
  14..24. Данные входят в world fingerprint/stage/policy. Не раскрывать полный
  серверный allocation list через wire без потребности UI.
- Гарантированные миры остаются обычными незаселёнными planets. Только home создаёт
  стартовую колонию/экономику/станцию/флот. Их имена, размеры, кольца, пояса и
  разрешённый detailed view проходят уже существующий pipeline.
- Сохранить стили/локализацию/навигацию. UI показывает «Гарантированные миры»0/1/2
  в текущем лобби. Прочий special content продолжает иметь явный unsupported gate.

## Приёмка

Один компактный graph/source fixture для0/1/2, двух ideal contexts, nearest/ties,
hops, no duplicate/reserved sites и underfill. Один реальный tiny на2 с Session
settings/projection/ownership; одна focused Node проверка lobby binding/validation;
одна Server Release сборка. Independent actual-diff review. Browser не запускать,
широкие и статистические suites не повторять. Только изменённые проверки повторять
после findings; результаты и ограничения в существующем validation report.
