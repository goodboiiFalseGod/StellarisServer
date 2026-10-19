# Generated world: локальные native assets

14.10.2026, GENERATED-WORLD-VISUALS-001. Контракт bounded renderer/import среза.
Кандидат до независимого ревью и настоящей GPU QA не считается готовым RC.
Session/lobby, Galaxy producer, colony UI и исходный стабильный сервер сюда не входят.

## API и wire

`new GeneratedWorldVisualRequirements(world.Bodies.Select(b => b.ClassId))` создаёт
immutable sorted unique набор из 1..128 корректных `pc_*`. Duplicate входа
нормализуется; wire уже обязан быть sorted unique. Версия 1, policy
`SourceFirstNumberedEntityV1`. `requirements.VisualIdFor(classId)` возвращает
`world.` + точный class ID; отсутствующий requested class — typed отказ.

`M3AssetManifestBuilder.BuildAsync(gameRoot, requirements, cancellationToken)`
строит source closure и дважды проверяет длины/SHA-256. Metadata, использованная
для graph resolve, также сравнивается с первым hash pass. Reparse points,
traversal, неоднозначность, изменение файла/версии и cancellation отклоняют результат.
Старый overload и v10 manifest без requirements сохраняют прежний wire.

Generated manifest добавляет только `generatedRequirements` с полями
`version/policy/classIds`; importerVersion=9, materialProfileVersion=5,
schemaVersion=1. Profile ID:

```text
m3-static-452-v10-gw1-<sha256 требований>
```

Canonical UTF-8 без BOM, LF, включая последний LF:

```text
generated-world-visuals-v1
SourceFirstNumberedEntityV1
<classId ordinal, по одному на строку>
```

Manifest digest сохраняет canonical header/file-record алгоритм M3; header profileId
связывает его с точным набором требований. Visuals/dependencies/materialProfile
дополнительно строго сверяются со своим шаблоном. SourceMip512V1 указан в
materialProfile каждого visual generated-профиля, включая baseline references;
neutron также раскрывает `NeutronNoSurfaceAtmosphereMixV1`.

Минимальные поля consumer snapshot:

- `body.visualId = requirements.VisualIdFor(body.worldClassId)`;
- `body.worldClassId`: точный `pc_*`; `kind`: `star | planet | moon`;
- `body.parentBodyId`: actual generated parent либо null;
- `system.starClassId`: точный producer `sc_*`, включая binary/trinary;
- существующие id/systemId/localPosition/scale сохраняют свой контракт.

Renderer сверяет все generated тела и lighting keys с импортированным набором
до замены вида, включая galaxy view. Ошибочный class/visual/kind, отсутствующий
source template/light, dangling/cross-system/cyclic parent — отказ. Moon использует
свой source class; orbit context центрируется на actual parent, не на системе.

`GeneratedWorldSystemLighting.ResolveAsync(readOnlySourceReader, ct)` возвращает
immutable `sc_* → worldgfx.world → system_light → RGB/intensity/position/radius/falloff`.
Server consumer обязан проверить actual system StarClassId до world publish.
Это source map, а не восстановленная native CPU light conversion.

## Source selection и поддержанный срез

Planet class читается из `common/planet_classes/00_planet_classes.txt`. Выбирается
реально объявленный exact entity, иначе единственный реально объявленный
`<source entity token>_01_entity`. Отсутствие/неоднозначность — gate; имена URL
или entity из class ID не угадываются. Это явная prototype variant policy,
не native RNG. Mesh definition, native binary material channels, overrides,
shader, skin/bind pose, referenced DDS и их source paths проверяются локально.

Покрыты все 31 возможных класса принятого ordinary/home pool:

```text
pc_a_star pc_alpine pc_arctic pc_arid pc_asteroid pc_b_star pc_barren
pc_barren_cold pc_continental pc_desert pc_f_star pc_frozen pc_g_star
pc_gaia pc_gas_giant pc_ice_asteroid pc_k_star pc_m_giant_star pc_m_star
pc_molten pc_neutron_star pc_nuked pc_ocean pc_pulsar pc_rare_crystal_asteroid
pc_savannah pc_t_star pc_toxic pc_tropical pc_tundra pc_volcanic
```

Проверенные 8 actual worlds содержат 29 из этих классов, 1600 систем и 18124 тела.
Оставшиеся rare-crystal asteroid/volcanic проверяются настоящими source assets,
но их появление в этих восьми worlds не заявляется. Black hole и неизвестные
дополнительные classes/attachments/shaders отклоняются, не заменяются G/continental
или видимым generic primitive. Galaxy system markers остаются прежней абстракцией
систем, а системные тела используют native buffers.

Neutron/pulsar сохраняют native atmosphere/surface и все mesh layers attached
outbursts. Anchor `planet` вычисляется из проверенного inverse global bind;
source entity/mesh scales сохраняются. Частицы/звук/анимация не исполняются.
Полная native WorldMatrix-композиция class entity_scale/planet_size/zoom остаётся
неизвестной: существующая M3 display calibration применяется как общий reference
factor по semantic type, сохраняя относительный source entity scale. Native
абсолютные размеры или их совпадение с оригиналом не заявляются.

Lighting импортирует только 9 base worldgfx metadata и `gfx/lights/star_lights.asset`,
без дополнительных DDS, LUT, environment maps и DLC. Один system light выбирается
по actual `system.starClassId`. Unknown mapping явно блокирует publication;
hardcoded G-light для generated мира не используется. Raw source RGB/intensity/
position в Three — раскрытая `SourceLightThreeCalibrationV1` approximation.

## Локальный import и память

Manifest paths не являются полномочием читать произвольный файл. Worker сначала
проверяет metadata/identity/digest и известные definition paths, затем независимо
разрешает native mesh paths из локальных definitions, parses binary materials и
вычисляет точную DDS closure. До чтения DDS множество server paths должно точно
совпасть с local graph. Лишний server path не открывается. Общие distant-stars
texture basename aliases разрешаются только среди проверенных локальных source
кандидатов, с последующей exact closure/hash проверкой.

Сохраняются лимиты file≤64MiB, DDS dimension≤8192, 512MiB old+staged budget;
definitions суммарно≤16MiB, bounded AST tokens/depth. Generated 2D world textures
берут только существующий stored mip≤512 (`SourceMip512V1`), noise cubemap остаётся
native1024. Полный DDS payload/mip chain проверяется до allocations. BGRA8 mip
chain требует explicit opt-in и tightly packed rows; прежний reader default
по-прежнему отклоняет BGRA8 с несколькими mip. Resampling/подмена textures отсутствуют.

Production worker освобождает verified raw DDS buffers по мере декодирования
(`ReleaseVerifiedDdsAfterDecodeV1`), затем независимо перечитывает/хеширует источник.
ДО чтения/mesh parse проверяется combined encoded source + 4×mesh-byte reserve:
два baseline/generated parse, каждый не больше source numeric arrays + index copy.
Тот же combined guard действует в direct generated profile API до typed allocations.
Затем отдельно проверяется весь прогноз source+geometry+selected decoded peak до
DDS allocations. Generated API по умолчанию освобождает raw DDS; explicit retain
тоже обязан уложиться в полный peak, а не только final decoded payload.
Результат передаётся с unique transferable buffers; persistent cache/upload/server
asset delivery отсутствуют. Flat File snapshots разрешены только при уникальных
basename; иначе нужен read-only DirectoryHandle.

Renderer до publication проверяет combined CPU old+staged и GPU reservation
old+staged. Generated GPU staging инициализирует textures и рисует все native
templates в offscreen 1×1 target; shader/context/allocation/draw error оставляет
старые profile/view. Reservation включает RGBA/mip storage и native geometry
attributes; это не измерение RSS/driver VRAM. UI image conversions учитываются
существующим client budget отдельно. Повтор полного импорта может безопасно
отказаться по памяти; прежний набор сохраняется.

## Rendering approximations

`PdxMeshPlanetEmissive` использует source normal.B emission mask и существующий
Phong/RRxG/spec adapter. Gas giant/molten сохраняют source diffuse/flow/specular,
но flow/UV time фиксирован в 0; flow map не ошибочно трактуется как normal map
или универсальная emission mask. Их emission mix не реализован в согласованном
static flow/spec adapter и не объявляется native shader parity.
Native HDR/environment/city lights и полное shader parity не заявляются.
Additive attached meshes используют свои native textures и frozen UV0.
Planet/corona tint берётся из source HSV, если он объявлен; существующий rim
adapter остаётся approximation.

`pc_neutron_star` не объявляет atmosphere color/intensity/width. В default/worldgfx
и transitive FX definitions значения не найдены; shader содержит только CPU uniform
declarations. По решению RC-координатора применяется
`NeutronNoSurfaceAtmosphereMixV1`: неизвестный дополнительный surface mix отключён
интенсивностью 0, native atmosphere mesh сохранён, corona tint берётся из source
lavaBright HSV. Ноль не объявляется native default. Policy доступна в manifest
materialProfile и `profile.generatedBindings.get(id).approximationPolicy`; UI owner
обязан раскрыть её вместе с `profile.generatedResourcePolicy`.

Generated worker errors сохраняют known `{code,classId}` через
`generatedVisualError`; importer не доверяет неизвестным codes, extra keys и
некорректным class IDs. Legacy worker errors сохраняют прежнее поведение.

## Проверка и интеграция

Сводка evidence — [отчёт](reports/generated-world-visuals-001.md). Actual source
parse/metadata, CPU Three wiring и GPU visual QA считаются разными результатами.
Нужны independent actual-diff review и настоящий browser GPU stage/осмотр до ready.
Общий experimental RC объединяется до пользовательской ручной приёмки; stable/main
не обновляется до неё и отдельного явного разрешения пользователя.
