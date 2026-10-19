# Редкие звёзды и Trappist: MISC-01

База `db41c2b`. Продолжение разрешённой генерации, не полный story interpreter.
Один code writer, independent review; браузерные тесты исключены пользователем.

## Исполняемый pool

К прежним11 ordinary definitions добавить только `special_init_01` (black hole),
`special_init_08` (neutron), `special_init_09` (pulsar), `trappist_initializer`.
Источник: special_system_initializers.txt:5/440/469/1039. Остальные misc definitions
не считать выполненными: country/fleet/anomaly/habitat/deposit dependencies остаются
явными gates. Полная VanillaComplete не объявляется. Coverage text обновить.

Source pool фильтруется по explicit supported IDs в database/source порядке,
без произвольного hash-order. Новая `SourceMiscAdmissionV1` заменяет прежний
ordinary weighted selector; все homes и их spatial effects завершаются до ordinary
admission и при GuaranteedColonies0. Это сознательно меняет прежний результат seed:
новый pool, порядок и дополнительные spawn draws. Детерминизм внутри этой версии
сохраняется; побитовое совпадение с vanilla не gate (GALAXY-GENERATION.md).
GUARANTEED-01 совместимость0 относилась к его изолированному результату `db41c2b`;
число цепочек/миров и отсутствие новых колоний остаются неизменными.

## Admission

Reverse addendum §15.1, parser-gap handoff §5 и M20 NativeGenerationRules:

- Сначала исключить exhausted finite definitions (`used>=effectiveMax`). Missing
  max_instances — unlimited, не хранить finite counter. Effective max source integer
  умножается на setting factor и округляется half away from zero. Missing factor1.
- Вычислить usage_odds в контексте candidate system; negative исключить, zero
  сохранить. Один Content draw на выбор: positive total — weighted half-open
  intervals, zero total — `draw%eligibleCount`. Half-open policy не даёт нулевому
  весу шанса при положительной сумме; точная native tie-boundary не заявляется.
- Отдельный Content draw на spawn (`draw%100<chance`), даже при chance0/100.
  Missing spawn_chance100; scaled chance/effective factor через восстановленный M20.
  Failure расходует один finite use и повторяет admission. Success расходует ровно
  один use до executor; не увеличивать повторно после создания bodies.
- Пустой eligible set — typed fatal, не basic fallback. Unlimited zero-progress
  configuration отклонить явно вместо скрытого retry cap. Checked arithmetic,
  unknown active fields/setting/condition — source-aware typed failure.
- Source weights literal или expanded block base/modifier/complex_trigger_modifier;
  поддержать только grammar активного pool. Не подменять весь usage_odds константой.
  Хранить immutable use/attempt counters и stage/policy в metadata/fingerprint,
  не раскрывать полный admission trace через player wire.

## Trappist и effects

- Семь planets и star, классы/орбиты/размеры/имя брать из source initializer.
  max_instances1 и scaled_spawn_chance10 реально работают; не гарантировать
  присутствие Trappist на каждом seed и не делать особый unconditional spawn.
- Inline initializer_modifiers_habitable_world_systems_mult: factor0 при
  is_fe_cluster/is_marauder_cluster/empire_cluster. Первые две family выключены
  этим профилем; empire_cluster — реальные source flags всех homes/neighbors,
  а не константаfalse. `complex_trigger_modifier` умножает weight на
  `request.HabitableMultiplier * VALUE`, здесь VALUE.4 из expanded source.
  Документация common/script_values/00_script_values.txt:13,41–48 разрешает все
  численные операции mode, называет factor умножением и описывает предварительное
  масштабирование результата trigger вложенным mult. Вместе с mode=factor самого
  inline script это документированный вывод; независимый reference owner подтвердил
  его 16 октября, буквального примера mode=factor в комментариях нет. Arithmetic policy:
  checked fixed5, source order, truncation product к raw fixed после каждой операции;
  неизвестное native intermediate rounding не выдаётся за доказанное.
- Узко исполнить `prevent_anomaly=yes` для broken planet special_init01 и
  два `add_modifier={modifier=terraforming_candidate days=-1}` Trappist.
  Reuse/extract существующий bounded spatial/body helper, без общего VM и без
  повторного исполнения home economy. Только распознанные effects снять с Deferred.
- Authoritative body metadata хранит modifier ID и бессрочный duration−1;
  unknown/positive timed modifier effects вне активного pool не симулировать молча.
  Optional непустой modifier DTO передавать лишь для уже разрешённых detailed bodies.
  Информация о выбранной планете показывает локализованное имя модификатора.
  Терраформирование как действие этим срезом не реализуется.
- Использовать действующие names/sizes/rings/belts и generated visual pipeline.
  Реальные new class closures, включая pc_broken/black hole/neutron/pulsar,
  должны пройти строгую C#/JS source validation; никаких исходных assets в Git.
- Для `pc_black_hole` разрешён bounded `StaticBlackHoleAttachmentsV1`: exact root
  black_hole_entity/black_hole_new_mesh, единственный locator planet и три source
  attachments black_hole_rings_entity, black_hole_grav_lens_entity,
  black_hole_grav_animation_entity из black_hole_new.asset/.gfx. Проверить real
  meshes/textures/scale/attachment transforms одинаково в C#/JS; новые формы — typed
  failure. Root pSphereShape2/PdxMeshBlackHole получает отдельный статический
  MeshPhong adapter с source diffuse/normal/specular black_hole_new; pSphereShape3
  использует существующий адаптер PdxMeshAtmosphereStar. Native distortion,
  анимация событий и точное color grading не заявляются. Static rings используют имеющийся additive adapter; source shader
  PdxMeshBlackHoleBillboard получает собственный статический texture/billboard
  adapter без native scene distortion, событий, анимации и UV motion. Это явное
  visual deviation в разрешённом статическом браузерном профиле, не silent fallback
  и не gravitational lensing parity. Root surface radius для labels/calibration
  отделить от outer FX bounds; все ресурсы учитывать в memory/disposal.
  Освещение sc_black_hole брать из source star_black_hole.txt и согласовать C#/JS
  registry; не подменять неизвестный stellar profile обычной звездой.
  Исходная diffuse использует legacy RGB24 DDS: разрешён bounded decode этого
  формата с явной проверкой header/masks/pitch, размеров и длины данных, alpha=255.
  Неизвестные layouts по-прежнему отклоняются; проверить реальные source bytes
  и небольшой synthetic pixel fixture, не расширять decoder на другие форматы.
- Для pc_broken разрешён StaticBrokenPlanetRestGeometryV1: только source binding
  broken_planet_01_entity → barren_planet_01_destroyed_mesh → одноимённый .mesh,
  четыре skinned layers PdxMeshPlanet/PdxMeshClouds/PdxMeshTerra×2. Использовать
  исходную rest geometry без разрушения/вращения/particle events. Проверять hierarchy,
  inverse-bind matrices, bone indices, finite nonnegative normalized vertex weights
  в клиентском binary validator; точную форму asset closure согласовать в C#/JS.
  PdxMeshTerra получает явный статический texture adapter, без native shader parity.
  Bind×inverseBind=Identity не требует повторного
  применения tx; multi-influence разрешается только этой явной policy, общий rigid
  validator не ослабляется. Неизвестная структура — typed failure, не static fallback.
  В source pc_broken отсутствует chance_of_ring. Узкая MissingBrokenRingChanceV1
  задаёт для этого класса effective chance0 с одним Script0 draw обычной probability
  branch; explicit has_ring сохраняет приоритет без draw. Это собственная default
  policy, native constructor default не доказан. Duplicate chance и отсутствие
  chance у остальных классов остаются ошибкой.

## Приёмка

Один compact admission fixture: finite failure/use-once/exhaustion, negative/zero
weights, fallback, spawn draws, scaling/overflow/unknown gates и Trappist contexts.
Один genuine tiny runtime smoke после guarantees (число систем/домов, provenance,
source stellar classes, effects, privacy). Отдельно в том же focused процессе
исполнить genuine Trappist definition, если tiny seed не выбрал его: не искать
редкий seed множеством генераций ради проверки.
Один targeted Node check new class/manifest closure и modifier card; одна итоговая
Server Release сборка, independent actual-diff review. При правках повторять только
затронутое. Никакого браузера, статистических/полных suites и нового huge budget
без реального роста detailed набора; новые modifiers затрагивают лишь две planets.
