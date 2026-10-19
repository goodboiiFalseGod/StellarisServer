# M3 — профиль ассетов и требования к импортерам

Статус: read-only зависимости проверены по локальной 4.5.2; ниже технический профиль для реализации. Ограниченный BGRA8-reader реализован и проверен — [отчёт](M3-IMPORT-VALIDATION.md). Станция, планета и звезда ещё не прошли визуальную приёмку. Это не подтверждение готовности production importer. Исходники не копируются в Git. Общий контракт — [локальный импорт](M3-LOCAL-ASSET-IMPORT.md).

## Точный выбранный набор

Current target profile: `profileId=m3-static-452-v5`, gameVersion=4.5.2, schemaVersion=1, importerVersion=4, materialProfileVersion=4, materialProfile=`m3-static-galaxy-v4`. Preserve the previous 60 paths and four models; add exactly three galaxy textures, for 63 paths. [Galaxy core/dust import](M3-GALAXY-PRESENTATION-IMPORT.md) defines their explicit inferred bindings, formats, API and budgets. The preceding [bounded presentation import](M3-PRESENTATION-IMPORT.md) and historical v3 [star contract](M3-STAR-IMPORT.md) remain applicable except where explicitly superseded. No inferred transitive dependencies or filesystem crawling are allowed; resolve full relative paths and named overrides. Implementation/test status is recorded separately in [validation evidence](reports/m3-star-import-validation.md).

| visualId | Определение | Геометрия |
|---|---|---|
| ship.corvette.mammalian | `gfx/models/ships/mammalian_01/_mammalian_01_ships_entities.asset`: mammalian_01_corvette_S1_entity; `_mammalian_01_ships_meshes.gfx`: mammalian_01_corvette_S1_mesh | `gfx/models/ships/mammalian_01/mammalian_01_corvette_S1.mesh` |
| station.outpost.mammalian | `gfx/models/ships/starbases/_starbase_entities.asset`: mammalian_01_starbase_outpost_entity → mammalian_01_outpost_frame_mesh; mammalian_01_starbase_outpost_section_entity → mammalian_01_outpost_mesh. Определения mesh в `_starbase_meshes.gfx`, scale=1 | `gfx/models/ships/starbases/mammalian_01_outpost_frame.mesh` (locator-only) и `mammalian_01_outpost.mesh` в той же папке (3 render submesh) |
| planet.continental | `gfx/models/planets/_planetary_entities.asset`: continental_planet_01_entity; `_planetary_meshes.gfx`: planet_clouded_mesh | `gfx/models/planets/planet_clouded.mesh` |
| star.g | `gfx/models/planets/_star_entities.asset`: g_star_class_star_entity, scale=1.3; `_star_meshes.gfx`: star_mesh | `gfx/models/planets/base_star.mesh` |

Станция — современный mammalian starbase_outpost, root/frame + единственная core section. Legacy mammalian_01_outpost_station_section_entity с military_station_small не использовать: это другая модель. Контракт сборки — [статический адаптер](M3-STATIC-VISUAL-ADAPTER.md). Конкретный выбор раскрывает согласованный тестовый набор, не добавляет строительство/боевые характеристики.

Обязательные текстуры относительно `gfx/models/`:

- `ships/mammalian_01/mammalian_01_corvette_{diffuse,normal,specular}.dds` — три явных файла.
- `ships/mammalian_01/mammalian_01_stations_{diffuse,normal,specular}.dds` — три файла.
- `ships/mammalian_01/mammalian_01_battleship_{diffuse,normal,specular}.dds` — три файла для третьего submesh outpost, не дополнительный тип корабля.
- `planets/continental_01_diffuse.dds`, `planets/continental_01_normals.dds`, `planets/continental_01_spec.dds`.
- `planets/poles_1_diffuse.dds`, `planets/poles_1_normal.dds`, `planets/poles_1_specular.dds`.
- `planets/clouds_medium_diffuse.dds`, `planets/clouds_medium_normal.dds`, `planets/clouds_medium_specular.dds`, `planets/clouds_medium_shadow_diffuse.dds`.
- `nonormal.dds`, `nospec.dds` — реальные общие placeholder-файлы оригинала, не наша молчаливая замена потерянного ресурса. Звезда ссылается на nospec; собственный material adapter определяет его применимость.

Для записи files[] фигурные скобки раскрываются в явные пути; это не разрешение glob-сканирования установки. Файлы .asset/.gfx из таблицы также обязательны: они определяют связи, масштабы и override. Состояния animation/audio/particles читаются как неподдерживаемые внешние эффекты, не исполняются и не расширяют allowlist на весь граф зависимостей.

Для проверки сборки outpost также обязательны `common/ship_sizes/00_starbases.txt` и `common/section_templates/starbase.txt`; kind=txt (собственные контентные определения, не shader). Используются только starbase_outpost.core→part1 и OUTPOST_STARBASE_SECTION→starbase_outpost_section_entity, без импорта характеристик боя/стоимости. Импорт этих .txt не меняет экономику M2.

Минимальные UI-иконки M3: `gfx/interface/icons/resources/energy.dds`, `minerals.dds`, `food.dds` в той же папке; `gfx/interface/icons/fleet_size_icon.dds`, `gfx/interface/icons/planet.dds`. Метаданные: `interface/resources.gfx`, `interface/icons.gfx`, `interface/galaxy_view.gfx`. Sprite ID соответственно GFX_resource_energy/minerals/food, GFX_fleet_size_icon и GFX_colonies. Alloys/consumer_goods/research не импортировать заранее: M3 не добавляет отсутствующую экономику. UI использует системный шрифт/CSS, перенос игровых font-файлов не входит в набор.

## Пробелы reader, обязательные до готовности набора

1. Корвет: один render mesh + скрытая Collision, без skin, 7156 vertices/6644 triangles; `.gfx` переопределяет PdxMeshTerra → PdxMeshShip. Station frame — locator-only, 88 bytes, part1 с identity-поворотом; section имеет три submesh с одинаковым object name и индексами 0/1/2: 6605/4424, 372/504, 102/110 vertices/triangles. Материалы соответственно stations/corvette/battleship DDS, без skin/Collision. Не схлопывать их по name. DDS DXT1/5; reader DXT1/3/5 не означает поддержку всех DDS игры.
2. Planet: пять слоёв atmosphere/planet/poles/cloudshadow/clouds, четыре со skin+skeleton. На проверенных вершинах одно влияние с weight=1; поверхность/полюса bone2, clouds/shadow bone3. Матрицы skeleton не identity. Star: два слоя, поверхность rigid skin. Требуется исследованный и визуально подтверждённый статический bind-pose adapter. Не применять произвольно bone matrix к уже transformed вершинам и не выбрасывать skin ради успешного parse.
3. Planet-материалы сохраняют поверхность, полюса, облака/тень и атмосферу; shader adapters свои, упрощённые согласно G02. Star — статический material adapter с яркой emissive-поверхностью/атмосферой; g_class_star_particle/звук/idle не воспроизводятся, как и остальные согласованно исключённые VFX. Если без них результат неприемлем, согласовать корректировку, не объявлять совпадение с оригиналом.
4. UI DDS — не FourCC, а BGRA8: pfFlags=0x41, 32bpp, masks R=00ff0000, G=0000ff00, B=000000ff, A=ff000000; mipCount=0 означает один уровень в этих файлах. Ограниченный decoder этого layout и synthetic fixtures для обоих flags, усечения и padding реализованы и проверены; визуальная приёмка остаётся отдельной. У fleet/planet установлен DDSD_LINEARSIZE, поле20 — весь payload, **не row pitch**; у ресурсов — обычный pitch. Другие masks/layouts не подменять этим форматом.

## Manifest и защита

Сервер читает обязательные файлы read-only и создаёт files[{relativePath,byteLength,sha256,kind}]. Хешировать до/после импорта, при изменении источника отклонять профиль; браузер сверяет собственный источник. Поддерживаемые kind: asset, gfx, mesh, dds, txt. Не включать исходные shader-файлы как исполняемый код.

Для digest v1: UTF-8 без BOM, LF; первые строки — profileId, gameVersion, schemaVersion, importerVersion, materialProfileVersion, по одному значению на строку; затем records, сортировка relativePath ordinal, формат `relativePath<TAB>byteLength<TAB>sha256lowercase<LF>`. Запретить TAB/CR/LF и пути с выходом за корень. SHA-256 этой последовательности — profileDigest. Изменение visual mapping/material adapter без изменения файлов требует новой версии профиля/adapter. Шаблон visual mapping фиксируется собственным кодом/конфигурацией, не приходит как произвольный исполняемый shader.

Технические предохранители для данного набора: один исходный файл ≤64 MiB, размер стороны DDS ≤8192, общий staged decoded payload ≤512 MiB. Для cube резерв включает шесть RGBA faces, не одну поверхность; текущий noise — 24 MiB. До выделения проверить произведения размеров/stride/mips, vertex/index bounds и конечность float. Это лимиты входа/распаковки, не обещание измеренной RAM/VRAM браузера. Отклонение лимита — видимая ошибка с относительным именем. GPU mipmaps/драйвер могут занимать больше; отдельный счётчик и измерение обязательны.

В памяти временно сосуществуют старый и staged-набор: лимит относится к общей удерживаемой decoded-памяти, не только к каждому набору отдельно. Не создавать оба огромных набора бесконтрольно. При нехватке сохраняется прежний валидный набор, новый импорт отклоняется. Постоянного кеша/записи файлов нет.

## Gate до интеграции

Первый независимый шаг выполнен: `tools/asset-probe/formats.mjs` расширен без переноса игры в Git (M3-IMPORT-VALIDATION). Ниже сохраняется его контракт. BGRA8 v1 поддерживает только указанные masks/flags=0x41 и один уровень (header mipCount 0 или 1). FourCC должен быть нулевым. DDSD_PITCH: rowPitch≥width×4 и точная длина rowPitch×height; padding пропускается. DDSD_LINEARSIZE: только tightly packed и точный width×height×4; одновременные pitch/linear-size или отсутствие обоих явно отклоняются. Результат — RGBA8, codec=BGRA8, mipCount=1, decodedMip=0; alpha сохраняется без premultiply. Неизвестные masks/bit-depth/mips, усечение и trailing bytes отклоняются до выделения выходного массива. Предыдущие DXT/PDX-контракты и защитные лимиты стенда не ослаблять. Тесты включают synthetic padded rows, alpha, flags/errors и все пять реальных UI-файлов; визуальный gate остаётся отдельным.

Проверить все четыре visualId и пять UI icons: корректная ориентация/масштаб, отсутствие Collision, непотерянные слои/alpha, static pose, named texture overrides. Материальные отличия перечислить. Предварительный осмотр во встроенном браузере выполнен — M3-IMPORT-VALIDATION, обязательный Chrome gate пока **не PASS**. По последующему прямому решению пользователя (DECISIONS) дальнейшая интеграция допускается без ожидания его ручной проверки, с явно pending результатами. Провал bind pose или материала не обходится сменой модели/удалением слоёв без решения пользователя.

Тесты: существующие PDX/DXT, BGRA pitch/linear-size/mips/alpha, rigid skin и неidentity-позы, неправильные веса/индексы, missing/duplicate/changed files, canonical digest, отмена worker, atomic replacement, отсутствие постоянного хранения/сетевых игровых байтов. Hash manifest строится из собственной установки, сами оригинальные файлы и screenshots не коммитятся.
